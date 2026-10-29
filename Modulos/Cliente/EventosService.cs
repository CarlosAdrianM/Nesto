using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#591: eventos y señales por API (patrón CodigosPostalesService del mismo módulo).</summary>
    public class EventosService : IEventosService
    {
        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public EventosService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<EventoModel>> LeerEventos(bool soloActivos)
        {
            string body = await Enviar(c => c.GetAsync($"Eventos?empresa={Constantes.Empresas.EMPRESA_DEFECTO}&soloActivos={soloActivos.ToString().ToLowerInvariant()}"),
                "No se pudieron cargar los eventos");
            return JsonConvert.DeserializeObject<List<EventoModel>>(body) ?? new List<EventoModel>();
        }

        public async Task<EventoModel> GuardarEvento(EventoModel evento)
        {
            evento.Empresa ??= Constantes.Empresas.EMPRESA_DEFECTO;
            string body = await Enviar(c => evento.Id > 0
                    ? c.PutAsync($"Eventos/{evento.Id}", Json(evento))
                    : c.PostAsync("Eventos", Json(evento)),
                "No se pudo guardar el evento");
            return JsonConvert.DeserializeObject<EventoModel>(body);
        }

        public async Task<List<SenalEventoModel>> LeerSenales(string estado, int? eventoId)
        {
            string url = $"Eventos/Senales?empresa={Constantes.Empresas.EMPRESA_DEFECTO}";
            if (!string.IsNullOrWhiteSpace(estado))
            {
                url += $"&estado={Uri.EscapeDataString(estado)}";
            }
            if (eventoId.HasValue)
            {
                url += $"&eventoId={eventoId.Value}";
            }
            string body = await Enviar(c => c.GetAsync(url), "No se pudieron cargar las señales de los eventos");
            return JsonConvert.DeserializeObject<List<SenalEventoModel>>(body) ?? new List<SenalEventoModel>();
        }

        public async Task<List<SenalEventoModel>> LeerSenalesCliente(string cliente)
        {
            string body = await Enviar(c => c.GetAsync(
                    $"Eventos/Senales/Cliente?empresa={Constantes.Empresas.EMPRESA_DEFECTO}&cliente={Uri.EscapeDataString(cliente?.Trim() ?? string.Empty)}"),
                "No se pudieron cargar las señales del cliente");
            return JsonConvert.DeserializeObject<List<SenalEventoModel>>(body) ?? new List<SenalEventoModel>();
        }

        public async Task<SenalEventoModel> MarcarSenal(int eventoId, MarcarSenalEventoModel peticion)
        {
            string body = await Enviar(c => c.PostAsync($"Eventos/{eventoId}/Senales", Json(peticion)), "No se pudo marcar la señal");
            return JsonConvert.DeserializeObject<SenalEventoModel>(body);
        }

        public async Task QuitarSenal(int id)
        {
            _ = await Enviar(c => c.DeleteAsync($"Eventos/Senales/{id}"), "No se pudo quitar la señal");
        }

        // ---------------- Nesto#517: inscripciones ----------------

        public async Task<List<InscripcionEventoModel>> LeerInscripciones(int eventoId)
        {
            string body = await Enviar(c => c.GetAsync($"Eventos/{eventoId}/Inscripciones"),
                "No se pudieron cargar los inscritos del evento", vacioSiApiVieja: true);
            return Lista<InscripcionEventoModel>(body);
        }

        public async Task<List<InscripcionEventoModel>> LeerInscripcionesCliente(string empresa, string cliente)
        {
            string body = await Enviar(c => c.GetAsync(UrlInscripcionesCliente(empresa, cliente)),
                "No se pudieron cargar las inscripciones del cliente", vacioSiApiVieja: true);
            return Lista<InscripcionEventoModel>(body);
        }

        /// <summary>«Clientes/1/15191/Inscripciones» (sin empresa, la de por defecto).</summary>
        public static string UrlInscripcionesCliente(string empresa, string cliente)
        {
            string empresaUrl = Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim());
            string clienteUrl = Uri.EscapeDataString(cliente?.Trim() ?? string.Empty);
            return $"Clientes/{empresaUrl}/{clienteUrl}/Inscripciones";
        }

        public async Task<InscripcionEventoModel> Inscribir(int eventoId, InscribirEventoModel peticion)
        {
            peticion.Empresa ??= Constantes.Empresas.EMPRESA_DEFECTO;
            string body = await Enviar(c => c.PostAsync($"Eventos/{eventoId}/Inscripciones", Json(peticion)), "No se pudo inscribir al cliente");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        public async Task<InscripcionEventoModel> AnularInscripcion(int id)
        {
            string body = await Enviar(c => c.DeleteAsync($"Eventos/Inscripciones/{id}"), "No se pudo anular la inscripción");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        public async Task<InscripcionEventoModel> MarcarPagada(int id, MarcarPagoInscripcionModel peticion)
        {
            string body = await Enviar(c => c.PostAsync($"Eventos/Inscripciones/{id}/Pago", Json(peticion)),
                "No se pudo marcar la inscripción como pagada");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        public async Task<List<InscripcionEventoModel>> PasarLista(int eventoId, IEnumerable<int> asistentes)
        {
            var peticion = new AsistenciaEventoModel { Asistentes = new List<int>(asistentes ?? Array.Empty<int>()) };
            string body = await Enviar(c => c.PostAsync($"Eventos/{eventoId}/Asistencia", Json(peticion)), "No se pudo guardar la lista de asistencia");
            return Lista<InscripcionEventoModel>(body);
        }

        public async Task<InscripcionEventoModel> FacturarCurso(int id)
        {
            string body = await Enviar(c => c.PostAsync($"Eventos/Inscripciones/{id}/Facturar", Json(new { })), "No se pudo facturar el curso");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        public async Task<ProductoCursoModel> LeerProductoCurso(string producto)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return null;
            }
            string body = await Enviar(c => c.GetAsync(
                    $"Productos?empresa={Constantes.Empresas.EMPRESA_DEFECTO}&id={Uri.EscapeDataString(producto.Trim())}"),
                "No se pudo leer la referencia del curso", vacioSiNoExiste: true);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }
            ProductoCursoModel leido = JsonConvert.DeserializeObject<ProductoCursoModel>(body);
            // El grupo no viene en el DTO de la plantilla: se lee de la ficha (sin la parte cara de fichaCompleta).
            string ficha = await Enviar(c => c.GetAsync(
                    $"Productos?empresa={Constantes.Empresas.EMPRESA_DEFECTO}&id={Uri.EscapeDataString(producto.Trim())}&fichaCompleta=false"),
                "No se pudo leer el grupo de la referencia del curso", vacioSiNoExiste: true);
            if (!string.IsNullOrWhiteSpace(ficha))
            {
                leido.Grupo = Newtonsoft.Json.Linq.JObject.Parse(ficha)["Grupo"]?.ToString()?.Trim();
            }
            return leido;
        }

        public async Task<ProductoCursoCreadoModel> CrearProductoCurso(int eventoId, string modalidad)
        {
            string body = await Enviar(c => c.PostAsync($"Eventos/{eventoId}/ProductoCurso",
                Json(new CrearProductoCursoModel { Modalidad = modalidad })), "No se pudo crear la referencia del curso");
            return JsonConvert.DeserializeObject<ProductoCursoCreadoModel>(body);
        }

        // ---------------- NestoAPI#690 ----------------

        public async Task<InscripcionEventoModel> ConfirmarAsistencia(int id, string respuesta)
        {
            string body = await Enviar(c => c.PostAsync($"Eventos/Inscripciones/{id}/ConfirmarAsistencia",
                Json(new ConfirmarAsistenciaModel { Respuesta = respuesta })), "No se pudo apuntar la respuesta del cliente");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        public async Task<EventoModel> SubirImagen(int eventoId, string ruta)
        {
            byte[] bytes = await File.ReadAllBytesAsync(ruta);
            string body = await Enviar(c =>
            {
                var contenido = new MultipartFormDataContent();
                var fichero = new ByteArrayContent(bytes);
                fichero.Headers.ContentType = new MediaTypeHeaderValue(TipoImagen(ruta));
                contenido.Add(fichero, "fichero", Path.GetFileName(ruta));
                return c.PostAsync($"Eventos/{eventoId}/Imagen", contenido);
            }, "No se pudo subir la imagen del evento");
            return JsonConvert.DeserializeObject<EventoModel>(body);
        }

        public async Task BorrarImagen(int eventoId)
        {
            _ = await Enviar(c => c.DeleteAsync($"Eventos/{eventoId}/Imagen"), "No se pudo quitar la imagen del evento");
        }

        /// <summary>El tipo MIME por la extensión (la API acepta PNG, JPEG, GIF y WebP).</summary>
        public static string TipoImagen(string ruta)
        {
            switch (Path.GetExtension(ruta ?? string.Empty).ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                default: return "application/octet-stream";
            }
        }

        // ---------------- Nesto#545 (NestoAPI#692) ----------------

        public async Task<List<string>> LeerReclamos(int eventoId)
        {
            string body = await Enviar(c => c.GetAsync($"Eventos/{eventoId}/Reclamos"),
                "No se pudieron cargar los reclamos para anunciar el evento", vacioSiApiVieja: true);
            return Lista<string>(body);
        }

        public async Task<AnuncioEventoModel> Anunciar(int eventoId, bool otraVez, string reclamo)
        {
            string body = await Enviar(c => c.PostAsync(UrlAnunciar(eventoId, otraVez),
                    Json(new AnunciarEventoModel { Reclamo = string.IsNullOrWhiteSpace(reclamo) ? null : reclamo.Trim() })),
                "No se pudo anunciar el evento");
            return JsonConvert.DeserializeObject<AnuncioEventoModel>(body);
        }

        /// <summary>«Eventos/12/Anunciar» o «Eventos/12/Anunciar?otraVez=true».</summary>
        public static string UrlAnunciar(int eventoId, bool otraVez) =>
            $"Eventos/{eventoId}/Anunciar" + (otraVez ? "?otraVez=true" : string.Empty);

        // ---------------- NestoAPI#615 ----------------

        public async Task<List<PropuestaSenalModel>> LeerPropuestas()
        {
            string body = await Enviar(c => c.GetAsync($"Eventos/Senales/Propuestas?empresa={Constantes.Empresas.EMPRESA_DEFECTO}"),
                "No se pudieron cargar las señales propuestas", vacioSiApiVieja: true);
            return body.Length == 0 ? null : Lista<PropuestaSenalModel>(body);
        }

        public async Task<List<PropuestaSenalModel>> BuscarPropuestas(int? dias)
        {
            string body = await Enviar(c => c.PostAsync("Eventos/Senales/Propuestas/Buscar",
                    Json(new { Empresa = Constantes.Empresas.EMPRESA_DEFECTO, Dias = dias })),
                "No se pudieron buscar las señales", vacioSiApiVieja: true);
            return body.Length == 0 ? null : Lista<PropuestaSenalModel>(body);
        }

        public async Task<SenalEventoModel> ConfirmarPropuesta(int id)
        {
            string body = await Enviar(c => c.PostAsync($"Eventos/Senales/Propuestas/{id}/Confirmar", Json(new { })), "No se pudo confirmar la señal");
            return JsonConvert.DeserializeObject<SenalEventoModel>(body);
        }

        public async Task DescartarPropuesta(int id)
        {
            _ = await Enviar(c => c.PostAsync($"Eventos/Senales/Propuestas/{id}/Descartar", Json(new { })), "No se pudo descartar la propuesta");
        }

        // ---------------- Nesto#566 (NestoAPI#731) ----------------

        public async Task<List<PersonaContactoEventoModel>> LeerPersonasContacto(string empresa, string cliente, string contacto)
        {
            string body = await Enviar(c => c.GetAsync(UrlPersonasContacto(empresa, cliente, contacto)),
                "No se pudieron cargar las personas de contacto del cliente", vacioSiApiVieja: true);
            return Lista<PersonaContactoEventoModel>(body);
        }

        /// <summary>«Eventos/PersonasContacto?empresa=1&amp;cliente=15191&amp;contacto=0» (sin contacto, las de todas las direcciones).</summary>
        public static string UrlPersonasContacto(string empresa, string cliente, string contacto)
        {
            string empresaUrl = Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim());
            string url = $"Eventos/PersonasContacto?empresa={empresaUrl}&cliente={Uri.EscapeDataString(cliente?.Trim() ?? string.Empty)}";
            if (!string.IsNullOrWhiteSpace(contacto))
            {
                url += $"&contacto={Uri.EscapeDataString(contacto.Trim())}";
            }
            return url;
        }

        public async Task<InscripcionEventoModel> CambiarPersonaContacto(int id, string numeroPersona)
        {
            string body = await Enviar(c => c.PutAsync($"Eventos/Inscripciones/{id}/PersonaContacto",
                    Json(new CambiarPersonaContactoModel { PersonaContacto = string.IsNullOrWhiteSpace(numeroPersona) ? null : numeroPersona.Trim() })),
                "No se pudo cambiar la persona que asiste");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        private static List<T> Lista<T>(string body) =>
            string.IsNullOrWhiteSpace(body) ? new List<T>() : JsonConvert.DeserializeObject<List<T>>(body) ?? new List<T>();

        /// <param name="vacioSiApiVieja">Nesto#517: con una API sin la ruta (el 404 de Web API) devuelve "" en vez de fallar.</param>
        /// <param name="vacioSiNoExiste">Cualquier 404 devuelve "" (lo que se busca no existe).</param>
        private async Task<string> Enviar(Func<HttpClient, Task<HttpResponseMessage>> llamada, string mensajeError,
            bool vacioSiApiVieja = false, bool vacioSiNoExiste = false)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(configuracion.servidorAPI);
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            HttpResponseMessage response = await llamada(client);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                int estado = (int)response.StatusCode;
                bool apiVieja = EsApiSinLaRuta(estado, body);
                if (estado == 404 && (vacioSiNoExiste || (vacioSiApiVieja && apiVieja)))
                {
                    return string.Empty;
                }
                if (apiVieja)
                {
                    throw new Exception($"{mensajeError}: la API todavía no tiene las inscripciones a eventos. Avise a Informática.");
                }
                throw new Exception(MensajeError(estado, body, mensajeError));
            }
            return body;
        }

        /// <summary>
        /// El texto del error de la API. NestoAPI#741: un 403 (p. ej. «Solo puede inscribir a sus clientes…») se enseña tal
        /// cual, que ya dice de usted qué pasa y qué hacer; el resto, con delante qué no se pudo hacer.
        /// </summary>
        internal static string MensajeError(int estado, string body, string mensajeError)
        {
            string mensaje = string.IsNullOrWhiteSpace(body) ? null : ExtraerMensaje(body)?.Trim();
            if (estado == 403)
            {
                return string.IsNullOrEmpty(mensaje) ? $"{mensajeError}: no tiene permiso." : mensaje;
            }
            return $"{mensajeError}: {mensaje}";
        }

        /// <summary>
        /// Nesto#517: ¿es el 404 de Web API de una ruta que no existe (una API anterior a NestoAPI#611)? Un 404 de negocio
        /// («No existe el evento 5») trae su mensaje y no cuenta.
        /// </summary>
        public static bool EsApiSinLaRuta(int estado, string body)
        {
            if (estado != 404)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(body))
            {
                return true;
            }
            return body.IndexOf("No HTTP resource was found", StringComparison.OrdinalIgnoreCase) >= 0
                || body.IndexOf("No se encontró ningún recurso HTTP", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static StringContent Json(object objeto)
            => new(JsonConvert.SerializeObject(objeto), Encoding.UTF8, "application/json");

        // Errores de negocio: {"error":{"message":"..."}}; 403 y BadRequest de Web API: {"Message":"..."}.
        internal static string ExtraerMensaje(string body)
        {
            try
            {
                JObject json = JsonConvert.DeserializeObject<JObject>(body);
                string mensaje = (json?["error"] as JObject)?["message"]?.ToString() ?? json?["Message"]?.ToString();
                if (!string.IsNullOrWhiteSpace(mensaje))
                {
                    return mensaje;
                }
            }
            catch
            {
            }
            return body;
        }
    }
}
