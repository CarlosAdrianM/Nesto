using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Sugerencia 648 (Paloma, NestoAPI#658): el Excel con lo facturado en la tienda en un periodo (cursos incluidos) para pasar
    /// las cifras de la semana. Lo calcula, lo genera y lo MANDA POR CORREO la API: Nesto corre en el escritorio remoto, donde
    /// Office no tiene licencia, así que el Excel no se abre aquí. Qué tienda puede ver cada uno también lo decide la API.
    /// </summary>
    public interface IServicioFacturacionTienda
    {
        /// <summary>GET api/Informes/FacturacionTienda/Correo: el correo del usuario (su CorreoDefecto); null si no tiene.</summary>
        Task<string> LeerCorreoUsuario();

        /// <summary>
        /// POST api/Informes/FacturacionTienda/Correo. <paramref name="tienda"/> REI o ALC; null: la del usuario (su AlmacénPedidoVta).
        /// <paramref name="correo"/> null o vacío: el del usuario. Devuelve a qué correo se ha mandado y lo que llevaba.
        /// </summary>
        /// <exception cref="FacturacionTiendaException">403 (otra tienda o sin permiso), 400 (periodo, tienda o correo mal), 500 (no se ha podido mandar)…</exception>
        Task<ResultadoCorreoFacturacionTienda> MandarExcelPorCorreo(string tienda, DateTime desde, DateTime hasta, string correo);

        /// <summary>
        /// Nesto#559 (NestoAPI#719): GET api/Informes/FacturacionTienda con tipo Acumulado o Comparativo: de enero a
        /// <paramref name="hasta"/>, mes a mes y, en el comparativo, los <paramref name="anios"/> años anteriores.
        /// </summary>
        /// <exception cref="FacturacionTiendaException">400 (tipo, años o tienda mal) o 403, con el mensaje de la API.</exception>
        Task<FacturacionTiendaAnual> LeerAnual(string tienda, TipoInformeFacturacionTienda tipo, DateTime hasta, int anios);

        /// <summary>
        /// Nesto#559: GET api/Informes/FacturacionTienda/Excel con los mismos parámetros que la pantalla. Solo se descarga para
        /// guardarlo donde diga el usuario: no se abre (en el escritorio remoto Office no tiene licencia).
        /// </summary>
        /// <exception cref="FacturacionTiendaException">400 o 403, con el mensaje de la API.</exception>
        Task<ExcelFacturacionTienda> DescargarExcel(string tienda, TipoInformeFacturacionTienda tipo, DateTime desde, DateTime hasta, int anios);
    }

    /// <summary>Nesto#559: qué informe se pide (el parámetro «tipo» de la API).</summary>
    public enum TipoInformeFacturacionTienda
    {
        Semana,
        Acumulado,
        Comparativo
    }

    /// <summary>Nesto#559: el Excel que devuelve la API y el nombre que propone (Content-Disposition).</summary>
    public class ExcelFacturacionTienda
    {
        public byte[] Contenido { get; set; }
        public string NombreFichero { get; set; }
    }

    /// <summary>Nesto#559 (NestoAPI#719, FacturacionTiendaAnualDTO): lo facturado de enero a Hasta, mes a mes.</summary>
    public class FacturacionTiendaAnual
    {
        public string Tipo { get; set; }
        public string Tienda { get; set; }
        public int Anio { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        /// <summary>Del más reciente al más antiguo (vacío en el acumulado).</summary>
        public List<int> AniosAnteriores { get; set; } = new List<int>();
        public List<MesFacturacionTienda> Meses { get; set; } = new List<MesFacturacionTienda>();
        /// <summary>Mes 0, NombreMes «Total».</summary>
        public MesFacturacionTienda Total { get; set; }
    }

    public class MesFacturacionTienda
    {
        public int Mes { get; set; }
        public string NombreMes { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public int NumeroFacturas { get; set; }
        public decimal BaseImponible { get; set; }
        public decimal BaseImponibleCursos { get; set; }
        public decimal Acumulado { get; set; }
        /// <summary>En el orden de AniosAnteriores.</summary>
        public List<ComparativoMesFacturacionTienda> Anteriores { get; set; } = new List<ComparativoMesFacturacionTienda>();
    }

    public class ComparativoMesFacturacionTienda
    {
        public int Anio { get; set; }
        public int NumeroFacturas { get; set; }
        public decimal BaseImponible { get; set; }
        /// <summary>La base del año del informe menos la de este.</summary>
        public decimal VariacionImporte { get; set; }
        /// <summary>En % con dos decimales; null si la base de este año es 0.</summary>
        public decimal? VariacionPorcentaje { get; set; }
    }

    /// <summary>Lo que contesta la API al mandar el Excel por correo.</summary>
    public class ResultadoCorreoFacturacionTienda
    {
        public string Correo { get; set; }
        public string Tienda { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public int NumeroFacturas { get; set; }
        public decimal TotalBaseImponible { get; set; }
        public decimal Total { get; set; }
    }

    public class FacturacionTiendaException : Exception
    {
        public FacturacionTiendaException(string mensaje, int codigo) : base(mensaje)
        {
            Codigo = codigo;
        }

        public int Codigo { get; }
    }

    public class ServicioFacturacionTienda : IServicioFacturacionTienda
    {
        internal const string MENSAJE_SIN_PERMISO =
            "El informe de facturación de tienda solo lo pueden ver Dirección, Administración, Informática y cada tienda el suyo.";
        internal const string URL_CORREO = "Informes/FacturacionTienda/Correo";
        internal const string URL_INFORME = "Informes/FacturacionTienda";
        internal const string URL_EXCEL = "Informes/FacturacionTienda/Excel";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioFacturacionTienda(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        /// <summary>El cuerpo del POST: las fechas sin hora; tienda y correo, null si no se dicen.</summary>
        internal static string Cuerpo(string tienda, DateTime desde, DateTime hasta, string correo)
        {
            return JsonConvert.SerializeObject(new
            {
                Tienda = string.IsNullOrWhiteSpace(tienda) ? null : tienda.Trim(),
                Desde = desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Hasta = hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Correo = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim()
            });
        }

        /// <summary>
        /// Nesto#559: la consulta de GET api/Informes/FacturacionTienda[/Excel]. La semana lleva desde y hasta; el acumulado, solo
        /// hasta (la API empieza el 1 de enero); el comparativo, además, los años. Sin tienda: la del usuario (lo decide la API).
        /// </summary>
        internal static string Consulta(string tienda, TipoInformeFacturacionTienda tipo, DateTime desde, DateTime hasta, int anios)
        {
            var partes = new List<string> { "tipo=" + tipo };
            if (!string.IsNullOrWhiteSpace(tienda))
            {
                partes.Add("tienda=" + Uri.EscapeDataString(tienda.Trim()));
            }
            if (tipo == TipoInformeFacturacionTienda.Semana)
            {
                partes.Add("desde=" + desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
            partes.Add("hasta=" + hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (tipo == TipoInformeFacturacionTienda.Comparativo)
            {
                partes.Add("anios=" + anios.ToString(CultureInfo.InvariantCulture));
            }
            return "?" + string.Join("&", partes);
        }

        public async Task<FacturacionTiendaAnual> LeerAnual(string tienda, TipoInformeFacturacionTienda tipo, DateTime hasta, int anios)
        {
            if (tipo == TipoInformeFacturacionTienda.Semana)
            {
                throw new ArgumentException("La semana no tiene informe en pantalla: se manda por correo.", nameof(tipo));
            }
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(URL_INFORME + Consulta(tienda, tipo, hasta, hasta, anios)).ConfigureAwait(false);
                await LanzarSiError(response).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<FacturacionTiendaAnual>(json) ?? new FacturacionTiendaAnual();
            }
        }

        public async Task<ExcelFacturacionTienda> DescargarExcel(string tienda, TipoInformeFacturacionTienda tipo, DateTime desde, DateTime hasta, int anios)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(URL_EXCEL + Consulta(tienda, tipo, desde, hasta, anios)).ConfigureAwait(false);
                await LanzarSiError(response).ConfigureAwait(false);
                byte[] contenido = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                string nombre = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName;
                return new ExcelFacturacionTienda
                {
                    Contenido = contenido,
                    NombreFichero = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim().Trim('"')
                };
            }
        }

        public async Task<string> LeerCorreoUsuario()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(URL_CORREO).ConfigureAwait(false);
                await LanzarSiError(response).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                string correo = JsonConvert.DeserializeObject<ResultadoCorreoFacturacionTienda>(json)?.Correo;
                return string.IsNullOrWhiteSpace(correo) ? null : correo.Trim();
            }
        }

        public async Task<ResultadoCorreoFacturacionTienda> MandarExcelPorCorreo(string tienda, DateTime desde, DateTime hasta, string correo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpContent contenido = new StringContent(Cuerpo(tienda, desde, hasta, correo), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(URL_CORREO, contenido).ConfigureAwait(false);
                await LanzarSiError(response).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<ResultadoCorreoFacturacionTienda>(json);
            }
        }

        private static async Task LanzarSiError(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            int codigo = (int)response.StatusCode;
            string cuerpo = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            string motivo = ProductoService.MotivoDelErrorDeTexto(cuerpo);
            if (string.IsNullOrWhiteSpace(motivo))
            {
                motivo = codigo == 403 ? MENSAJE_SIN_PERMISO : $"El servidor ha contestado con el error {codigo}.";
            }
            throw new FacturacionTiendaException(motivo, codigo);
        }
    }
}
