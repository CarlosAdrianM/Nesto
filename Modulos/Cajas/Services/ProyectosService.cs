using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Services
{
    /// <summary>
    /// Un error de api/Proyectos con su <see cref="Codigo"/> (SIN_TABLA, APUNTE_CAMBIADO, PROYECTO_CERRADO…) y su estado HTTP, para
    /// que la pantalla pueda reaccionar (recargar el apunte que ha cambiado) además de enseñar el mensaje.
    /// </summary>
    public class ProyectosApiException : ErrorApiCajasException
    {
        public ProyectosApiException(string mensaje, string? codigo, HttpStatusCode estado) : base(mensaje)
        {
            Codigo = codigo;
            Estado = estado;
        }

        public string? Codigo { get; }
        public HttpStatusCode Estado { get; }
    }

    /// <summary>Proyectos (gemela de NestoAPI#901): api/Proyectos por HTTP (patrón InmovilizadosService).</summary>
    public class ProyectosService : IProyectosService
    {
        public const string CODIGO_SIN_TABLA = "SIN_TABLA";
        public const string CODIGO_APUNTE_CAMBIADO = "APUNTE_CAMBIADO";
        public const string CODIGO_APUNTE_NO_EXISTE = "APUNTE_NO_EXISTE";
        internal const string MENSAJE_SIN_TABLA = "Los proyectos todavía no están activados en el servidor.";
        internal const string MENSAJE_SIN_PERMISO = "Solo Administración, Dirección o Informática pueden ver y cambiar los proyectos.";
        internal const string MENSAJE_NO_EXISTE = "Ese proyecto o ese apunte ya no existe.";
        internal const string MENSAJE_SERVIDOR_ANTIGUO = "Esta opción necesita la versión nueva del servidor.";

        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ProyectosService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public Task<List<ProyectoModel>> Listar(string empresa, bool soloAbiertos, string? tipo, string? filtro)
            => Get<List<ProyectoModel>>(UrlListar(empresa, soloAbiertos, tipo, filtro), "No se han podido leer los proyectos.");

        public Task<ProyectoModel> Crear(ProyectoModel proyecto)
            => Enviar<ProyectoModel>(HttpMethod.Post, "Proyectos", proyecto, "No se ha podido crear el proyecto.");

        public Task<ProyectoModel> Modificar(int id, ProyectoModel proyecto)
            => Enviar<ProyectoModel>(HttpMethod.Put, $"Proyectos/{id}", proyecto, "No se ha podido guardar el proyecto.");

        public Task<ProyectoModel> Cerrar(int id)
            => Enviar<ProyectoModel>(HttpMethod.Post, $"Proyectos/{id}/Cerrar", null, "No se ha podido cerrar el proyecto.");

        public Task<ProyectoModel> Reabrir(int id)
            => Enviar<ProyectoModel>(HttpMethod.Post, $"Proyectos/{id}/Reabrir", null, "No se ha podido reabrir el proyecto.");

        public async Task Borrar(int id)
        {
            using (HttpClient client = await CrearCliente())
            {
                HttpResponseMessage response = await client.DeleteAsync($"Proyectos/{id}");
                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    throw Error(response.StatusCode, body, "No se ha podido borrar el proyecto.");
                }
            }
        }

        public Task<ApuntesPorImputarModel> ApuntesPorImputar(FiltroApuntesProyectos filtro)
            => Get<ApuntesPorImputarModel>(UrlApuntes(filtro), "No se han podido leer los apuntes.");

        public Task<ImputacionesApunteModel> LeerImputaciones(string empresa, int numeroOrden)
            => Get<ImputacionesApunteModel>($"Proyectos/Apuntes/{numeroOrden}/Imputaciones?empresa={Uri.EscapeDataString(empresa ?? string.Empty)}",
                "No se ha podido leer el reparto del apunte.");

        public Task<ImputacionesApunteModel> GuardarImputaciones(int numeroOrden, PeticionImputacionesApunte peticion)
            => Enviar<ImputacionesApunteModel>(HttpMethod.Put, $"Proyectos/Apuntes/{numeroOrden}/Imputaciones", peticion,
                "No se ha podido guardar el reparto del apunte.");

        public Task<CuentaExplotacionProyectoModel> CuentaExplotacion(int id)
            => Get<CuentaExplotacionProyectoModel>($"Proyectos/{id}/CuentaExplotacion", "No se ha podido leer la cuenta de explotación.");

        public async Task<FicheroProyecto> CuentaExplotacionExcel(int id)
        {
            using (HttpClient client = await CrearCliente())
            {
                HttpResponseMessage response = await client.GetAsync($"Proyectos/{id}/CuentaExplotacion/Excel");
                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    throw Error(response.StatusCode, body, "No se ha podido sacar el Excel de la cuenta de explotación.");
                }
                var disposicion = response.Content.Headers.ContentDisposition;
                string? nombre = disposicion?.FileNameStar ?? disposicion?.FileName;
                return new FicheroProyecto
                {
                    Nombre = nombre?.Trim().Trim('"'),
                    Contenido = await response.Content.ReadAsByteArrayAsync()
                };
            }
        }

        public Task<ComparativaProyectosModel> Comparativa(IEnumerable<int>? ids, string? producto, string empresa)
            => Get<ComparativaProyectosModel>(UrlComparativa(ids, producto, empresa), "No se ha podido sacar la comparativa.");

        internal static string UrlListar(string empresa, bool soloAbiertos, string? tipo, string? filtro)
        {
            var url = new StringBuilder($"Proyectos?empresa={Uri.EscapeDataString(empresa ?? string.Empty)}&soloAbiertos={(soloAbiertos ? "true" : "false")}");
            Anadir(url, "tipo", tipo);
            Anadir(url, "filtro", filtro);
            return url.ToString();
        }

        internal static string UrlApuntes(FiltroApuntesProyectos filtro)
        {
            filtro ??= new FiltroApuntesProyectos();
            var url = new StringBuilder($"Proyectos/ApuntesPorImputar?empresa={Uri.EscapeDataString(filtro.Empresa ?? string.Empty)}");
            Anadir(url, "desde", filtro.Desde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Anadir(url, "hasta", filtro.Hasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Anadir(url, "cuenta", filtro.Cuenta);
            Anadir(url, "proveedor", filtro.Proveedor);
            Anadir(url, "documento", filtro.Documento);
            Anadir(url, "texto", filtro.Texto);
            url.Append("&soloPendientes=").Append(filtro.SoloPendientes ? "true" : "false");
            url.Append("&pagina=").Append(filtro.Pagina.ToString(CultureInfo.InvariantCulture));
            if (filtro.Tope.HasValue)
            {
                url.Append("&tope=").Append(filtro.Tope.Value.ToString(CultureInfo.InvariantCulture));
            }
            return url.ToString();
        }

        internal static string UrlComparativa(IEnumerable<int>? ids, string? producto, string empresa)
        {
            var url = new StringBuilder($"Proyectos/Comparativa?empresa={Uri.EscapeDataString(empresa ?? string.Empty)}");
            List<int> lista = ids?.Distinct().ToList() ?? new List<int>();
            if (lista.Count > 0)
            {
                url.Append("&ids=").Append(string.Join(",", lista.Select(i => i.ToString(CultureInfo.InvariantCulture))));
            }
            else
            {
                Anadir(url, "producto", producto);
            }
            return url.ToString();
        }

        private static void Anadir(StringBuilder url, string nombre, string? valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                url.Append('&').Append(nombre).Append('=').Append(Uri.EscapeDataString(valor.Trim()));
            }
        }

        /// <summary>
        /// El error de la API con su código. Mensaje: sin las tablas, «todavía no están activados» y debajo lo que dice la API; si
        /// no, lo que dice la API; si no dice nada legible, el de su estado (403, 404, ruta que no existe) o el de contexto.
        /// </summary>
        internal static ProyectosApiException Error(HttpStatusCode estado, string body, string contexto)
        {
            string? detalle = ErroresApiCajas.Detalle(body);
            string? codigo = LeerCodigo(body);
            if (estado == HttpStatusCode.NotFound && detalle != null && detalle.IndexOf("No HTTP resource", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new ProyectosApiException(MENSAJE_SERVIDOR_ANTIGUO, codigo, estado);
            }
            if (string.Equals(codigo, CODIGO_SIN_TABLA, StringComparison.OrdinalIgnoreCase) || (estado == HttpStatusCode.ServiceUnavailable && codigo == null))
            {
                string mensaje = string.IsNullOrWhiteSpace(detalle) ? MENSAJE_SIN_TABLA : MENSAJE_SIN_TABLA + Environment.NewLine + detalle;
                return new ProyectosApiException(mensaje, CODIGO_SIN_TABLA, estado);
            }
            if (!string.IsNullOrWhiteSpace(detalle))
            {
                return new ProyectosApiException(detalle!, codigo, estado);
            }
            string porEstado = estado switch
            {
                HttpStatusCode.Forbidden => MENSAJE_SIN_PERMISO,
                HttpStatusCode.NotFound => MENSAJE_NO_EXISTE,
                _ => contexto
            };
            return new ProyectosApiException(porEstado, codigo, estado);
        }

        internal static string? LeerCodigo(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }
            try
            {
                JObject? json = JsonConvert.DeserializeObject<JObject>(body);
                string? codigo = (json?["Codigo"] ?? json?["codigo"] ?? json?["error"]?["code"])?.ToString();
                return string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<T> Get<T>(string url, string contexto) where T : new()
        {
            using (HttpClient client = await CrearCliente())
            {
                HttpResponseMessage response = await client.GetAsync(url);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw Error(response.StatusCode, body, contexto);
                }
                return JsonConvert.DeserializeObject<T>(body) ?? new T();
            }
        }

        private async Task<T> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, string contexto) where T : new()
        {
            using (HttpClient client = await CrearCliente())
            using (var peticion = new HttpRequestMessage(metodo, url))
            {
                peticion.Content = new StringContent(cuerpo == null ? "{}" : JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.SendAsync(peticion);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw Error(response.StatusCode, body, contexto);
                }
                return JsonConvert.DeserializeObject<T>(body) ?? new T();
            }
        }

        private async Task<HttpClient> CrearCliente()
        {
            var client = new HttpClient { BaseAddress = new Uri(_configuracion.servidorAPI) };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }
    }
}
