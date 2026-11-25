using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>Nesto#652 (NestoAPI#903): un centro DIR3 de la comprobación (01 oficina contable, 02 órgano gestor, 03 unidad tramitadora, 04 órgano proponente).</summary>
    public class CentroFacturaeModel
    {
        public string Rol { get; set; }
        public string Descripcion { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
    }

    /// <summary>Nesto#652: un tipo de IVA (y recargo) de la comprobación.</summary>
    public class ImpuestoFacturaeModel
    {
        public decimal PorcentajeIva { get; set; }
        public decimal BaseImponible { get; set; }
        public decimal CuotaIva { get; set; }
        public decimal PorcentajeRecargo { get; set; }
        public decimal CuotaRecargo { get; set; }
    }

    /// <summary>Nesto#652: un vencimiento de la comprobación.</summary>
    public class VencimientoFacturaeModel
    {
        public DateTime Fecha { get; set; }
        public decimal Importe { get; set; }
        public string FormaPago { get; set; }
        public string MedioPago { get; set; }
        public string Iban { get; set; }
    }

    /// <summary>Nesto#652: lo que contesta GET api/Facturas/Facturae?comprobar=true.</summary>
    public class ComprobacionFacturaeModel
    {
        public string Empresa { get; set; }
        public string Factura { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public bool Listo { get; set; }
        public List<string> Faltas { get; set; } = new List<string>();
        public List<string> Avisos { get; set; } = new List<string>();
        public string NombreFichero { get; set; }
        public bool EsRectificativa { get; set; }
        public string FacturaRectificada { get; set; }
        public string NifEmisor { get; set; }
        public string NifReceptor { get; set; }
        public string NombreReceptor { get; set; }
        /// <summary>«Ficha», «Llamada», «Ficha y llamada» o null.</summary>
        public string OrigenDir3 { get; set; }
        public List<CentroFacturaeModel> Centros { get; set; } = new List<CentroFacturaeModel>();
        public string SuPedido { get; set; }
        /// <summary>«Parametro», «Factura», «NuestroPedido» o null (la API vieja no lo manda).</summary>
        public string OrigenSuPedido { get; set; }
        public string Expediente { get; set; }
        public string Contrato { get; set; }
        public decimal BaseImponible { get; set; }
        public List<ImpuestoFacturaeModel> Impuestos { get; set; } = new List<ImpuestoFacturaeModel>();
        public decimal TotalImpuestos { get; set; }
        public decimal Total { get; set; }
        public int Lineas { get; set; }
        public List<VencimientoFacturaeModel> Vencimientos { get; set; } = new List<VencimientoFacturaeModel>();
    }

    /// <summary>
    /// Nesto#652: lo que se manda en la llamada y gana sobre lo guardado (vacío = no se manda: lo de la ficha o de la factura).
    /// </summary>
    public class ReferenciasFacturae
    {
        public string OficinaContable { get; set; }
        public string OrganoGestor { get; set; }
        public string UnidadTramitadora { get; set; }
        public string OrganoProponente { get; set; }
        public string SuPedido { get; set; }
        public string Expediente { get; set; }
        public string Contrato { get; set; }
    }

    /// <summary>Nesto#652: el fichero Facturae descargado.</summary>
    public class FicheroFacturae
    {
        public string NombreFichero { get; set; }
        public byte[] Contenido { get; set; }
    }

    /// <summary>Nesto#652: GET/PUT api/Clientes/FacturaElectronica (los DIR3 del organismo, por cliente y contacto).</summary>
    public class Dir3ClienteModel
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string OficinaContable { get; set; }
        public string OrganoGestor { get; set; }
        public string UnidadTramitadora { get; set; }
        public string OrganoProponente { get; set; }
        public string NombreOficinaContable { get; set; }
        public string NombreOrganoGestor { get; set; }
        public string NombreUnidadTramitadora { get; set; }
        public string NombreOrganoProponente { get; set; }
        public string Usuario { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public bool Guardado { get; set; }
        public bool TablaCreada { get; set; }
    }

    /// <summary>
    /// Nesto#652 (NestoAPI#903): factura electrónica para organismos públicos. La API genera el Facturae 3.2.2 SIN firmar de una
    /// factura emitida; B2Brouter la firma y la manda a FACe. Todos los errores llegan con el texto de la API (de usted).
    /// </summary>
    public interface IServicioFacturae
    {
        Task<ComprobacionFacturaeModel> Comprobar(string empresa, string factura, ReferenciasFacturae referencias);
        Task<FicheroFacturae> Descargar(string empresa, string factura, ReferenciasFacturae referencias);
        Task<Dir3ClienteModel> LeerDir3(string empresa, string cliente, string contacto);
        Task<Dir3ClienteModel> GuardarDir3(Dir3ClienteModel datos);
    }

    public class ServicioFacturae : IServicioFacturae
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioFacturae(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public async Task<ComprobacionFacturaeModel> Comprobar(string empresa, string factura, ReferenciasFacturae referencias)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (HttpResponseMessage respuesta = await client.GetAsync(ReglasFacturae.UrlFacturae(empresa, factura, true, referencias)).ConfigureAwait(false))
            {
                string cuerpo = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(ServicioEnvioCorreoDocumentos.MensajeDeError(cuerpo));
                }
                return JsonConvert.DeserializeObject<ComprobacionFacturaeModel>(cuerpo) ?? new ComprobacionFacturaeModel();
            }
        }

        public async Task<FicheroFacturae> Descargar(string empresa, string factura, ReferenciasFacturae referencias)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (HttpResponseMessage respuesta = await client.GetAsync(ReglasFacturae.UrlFacturae(empresa, factura, false, referencias)).ConfigureAwait(false))
            {
                if (!respuesta.IsSuccessStatusCode)
                {
                    string error = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(ServicioEnvioCorreoDocumentos.MensajeDeError(error));
                }
                byte[] contenido = await respuesta.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var disposicion = respuesta.Content.Headers.ContentDisposition;
                return new FicheroFacturae
                {
                    NombreFichero = ReglasFacturae.NombreFichero(disposicion?.FileNameStar ?? disposicion?.FileName, factura),
                    Contenido = contenido
                };
            }
        }

        public async Task<Dir3ClienteModel> LeerDir3(string empresa, string cliente, string contacto)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (HttpResponseMessage respuesta = await client.GetAsync(ReglasFacturae.UrlDir3(empresa, cliente, contacto)).ConfigureAwait(false))
            {
                string cuerpo = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(ServicioEnvioCorreoDocumentos.MensajeDeError(cuerpo));
                }
                return JsonConvert.DeserializeObject<Dir3ClienteModel>(cuerpo) ?? new Dir3ClienteModel();
            }
        }

        public async Task<Dir3ClienteModel> GuardarDir3(Dir3ClienteModel datos)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (var contenido = new StringContent(JsonConvert.SerializeObject(datos ?? new Dir3ClienteModel()), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage respuesta = await client.PutAsync("Clientes/FacturaElectronica", contenido).ConfigureAwait(false))
            {
                string cuerpo = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(ServicioEnvioCorreoDocumentos.MensajeDeError(cuerpo));
                }
                return JsonConvert.DeserializeObject<Dir3ClienteModel>(cuerpo) ?? datos;
            }
        }
    }

    /// <summary>Nesto#652: reglas puras de la descarga para FACe (URLs, códigos DIR3, textos).</summary>
    public static class ReglasFacturae
    {
        public const int LONGITUD_DIR3 = 9;
        public const int LONGITUD_MAXIMA_REFERENCIA = 20;

        public const string ROL_OFICINA_CONTABLE = "01";
        public const string ROL_ORGANO_GESTOR = "02";
        public const string ROL_UNIDAD_TRAMITADORA = "03";
        public const string ROL_ORGANO_PROPONENTE = "04";

        public const string TITULO = "Descargar para FACe (Facturae)";

        public const string AYUDA_B2BROUTER =
            "Cómo mandarla a FACe: entre en b2brouter.net › Ingresos, pulse la flecha que hay junto a «Crear factura» y elija «Importar». " +
            "Elija el fichero descargado (sin marcar ninguna casilla) y, cuando la enseñe, pulse «Emitir y enviar». " +
            "B2Brouter la firma y la manda a FACe: aquí no hace falta ningún certificado.";

        private static string Url(string texto) => Uri.EscapeDataString(texto?.Trim() ?? string.Empty);

        /// <summary>GET api/Facturas/Facturae con los parámetros que vengan (los vacíos no se mandan).</summary>
        public static string UrlFacturae(string empresa, string factura, bool comprobar, ReferenciasFacturae referencias)
        {
            var url = new StringBuilder("Facturas/Facturae?empresa=")
                .Append(Url(string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa))
                .Append("&factura=").Append(Url(factura));
            if (comprobar)
            {
                url.Append("&comprobar=true");
            }
            if (referencias != null)
            {
                Anadir(url, "oficinaContable", LimpiarDir3(referencias.OficinaContable));
                Anadir(url, "organoGestor", LimpiarDir3(referencias.OrganoGestor));
                Anadir(url, "unidadTramitadora", LimpiarDir3(referencias.UnidadTramitadora));
                Anadir(url, "organoProponente", LimpiarDir3(referencias.OrganoProponente));
                Anadir(url, "suPedido", referencias.SuPedido?.Trim());
                Anadir(url, "expediente", referencias.Expediente?.Trim());
                Anadir(url, "contrato", referencias.Contrato?.Trim());
            }
            return url.ToString();
        }

        private static void Anadir(StringBuilder url, string nombre, string valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                url.Append('&').Append(nombre).Append('=').Append(Url(valor));
            }
        }

        public static string UrlDir3(string empresa, string cliente, string contacto)
            => $"Clientes/FacturaElectronica?empresa={Url(string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa)}" +
               $"&cliente={Url(cliente)}&contacto={Url(contacto)}";

        /// <summary>El nombre que da la API (Content-Disposition, sin comillas) o, si no viene, «Facturae_{factura}.xml».</summary>
        public static string NombreFichero(string deLaApi, string factura)
        {
            string nombre = deLaApi?.Trim().Trim('"').Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                nombre = $"Facturae_{factura?.Trim()}.xml";
            }
            foreach (char invalido in Path.GetInvalidFileNameChars())
            {
                nombre = nombre.Replace(invalido, '_');
            }
            return nombre;
        }

        /// <summary>Como la API: sin espacios, guiones ni puntos y en mayúsculas.</summary>
        public static string LimpiarDir3(string codigo)
            => Regex.Replace(codigo ?? string.Empty, @"[\s\-\.]", string.Empty).ToUpperInvariant();

        /// <summary>Null si el código vale o está vacío (lo vacío lo decide quien llama); si no, el motivo de usted (como la API).</summary>
        public static string MotivoDir3NoValido(string codigo, string que)
        {
            string limpio = LimpiarDir3(codigo);
            if (limpio.Length == 0)
            {
                return null;
            }
            if (limpio.Length != LONGITUD_DIR3 || !Regex.IsMatch(limpio, "^[A-Z][A-Z0-9]{8}$") || limpio.Count(char.IsDigit) < 5)
            {
                return $"El código DIR3 {DeQue(que)} «{codigo?.Trim()}» no es válido: son 9 caracteres, una letra y cifras (por ejemplo, L01280796).";
            }
            return null;
        }

        /// <summary>Null si cabe; si no, el motivo de usted.</summary>
        public static string MotivoReferenciaLarga(string texto, string que)
        {
            string limpio = texto?.Trim() ?? string.Empty;
            return limpio.Length > LONGITUD_MAXIMA_REFERENCIA
                ? $"{que} no puede tener más de {LONGITUD_MAXIMA_REFERENCIA} caracteres (tiene {limpio.Length})."
                : null;
        }

        /// <summary>
        /// Lo que falta o no vale de los DIR3 tecleados para guardarlos en la ficha: los tres primeros obligatorios (salvo que
        /// estén los cuatro vacíos, que es quitarlos) y cada código con su formato. Lista vacía = se puede guardar.
        /// </summary>
        public static List<string> MotivosNoSePuedeGuardarDir3(string oficinaContable, string organoGestor, string unidadTramitadora, string organoProponente)
        {
            var motivos = new List<string>();
            if (SonTodosVacios(oficinaContable, organoGestor, unidadTramitadora, organoProponente))
            {
                return motivos;
            }
            AnadirObligatorio(motivos, oficinaContable, "Oficina contable");
            AnadirObligatorio(motivos, organoGestor, "Órgano gestor");
            AnadirObligatorio(motivos, unidadTramitadora, "Unidad tramitadora");
            string proponente = MotivoDir3NoValido(organoProponente, "Órgano proponente");
            if (proponente != null)
            {
                motivos.Add(proponente);
            }
            return motivos;
        }

        private static void AnadirObligatorio(List<string> motivos, string codigo, string que)
        {
            if (LimpiarDir3(codigo).Length == 0)
            {
                motivos.Add($"Falta el código DIR3 {DeQue(que)}.");
                return;
            }
            string motivo = MotivoDir3NoValido(codigo, que);
            if (motivo != null)
            {
                motivos.Add(motivo);
            }
        }

        public static bool SonTodosVacios(params string[] codigos) => codigos.All(c => LimpiarDir3(c).Length == 0);

        /// <summary>«Oficina contable» → «de la oficina contable»; «Órgano gestor» → «del órgano gestor».</summary>
        private static string DeQue(string que)
        {
            if (string.IsNullOrEmpty(que))
            {
                return "del centro";
            }
            string minuscula = char.ToLowerInvariant(que[0]) + que.Substring(1);
            return (minuscula.StartsWith("órgano", StringComparison.Ordinal) ? "del " : "de la ") + minuscula;
        }

        /// <summary>El código del centro con ese rol en la comprobación (vacío si no lo hay).</summary>
        public static string CodigoDelRol(ComprobacionFacturaeModel comprobacion, string rol)
            => comprobacion?.Centros?.FirstOrDefault(c => c?.Rol?.Trim() == rol)?.Codigo?.Trim() ?? string.Empty;

        /// <summary>De dónde sale «Su pedido», para que se entienda por qué va ese número.</summary>
        public static string TextoOrigenSuPedido(string origen)
        {
            switch (origen?.Trim())
            {
                case "Parametro":
                    return "el que ha escrito aquí";
                case "Factura":
                    return "el P.O. de la factura";
                case "NuestroPedido":
                    return "no hay P.O. del organismo: va nuestro número de pedido";
                default:
                    return null;
            }
        }

        /// <summary>«Ficha», «Llamada», «Ficha y llamada» → texto para el diálogo.</summary>
        public static string TextoOrigenDir3(string origen)
        {
            switch (origen?.Trim())
            {
                case "Ficha":
                    return "Los códigos DIR3 salen de la ficha del cliente.";
                case "Llamada":
                    return "Los códigos DIR3 son los escritos aquí (la ficha no tiene).";
                case "Ficha y llamada":
                    return "Los códigos DIR3 escritos aquí ganan sobre los de la ficha del cliente.";
                default:
                    return null;
            }
        }

        public static string Euros(decimal importe) => importe.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-ES")) + " €";

        public static string TextoImpuesto(ImpuestoFacturaeModel impuesto)
        {
            if (impuesto == null)
            {
                return string.Empty;
            }
            string texto = $"IVA {impuesto.PorcentajeIva:0.##} % sobre {Euros(impuesto.BaseImponible)}: {Euros(impuesto.CuotaIva)}";
            if (impuesto.PorcentajeRecargo != 0 || impuesto.CuotaRecargo != 0)
            {
                // El porcentaje de recargo puede venir en tanto por uno (0,052) o por ciento (5,2)
                decimal porcentaje = impuesto.PorcentajeRecargo < 1 ? impuesto.PorcentajeRecargo * 100 : impuesto.PorcentajeRecargo;
                texto += $" · recargo {porcentaje:0.##} %: {Euros(impuesto.CuotaRecargo)}";
            }
            return texto;
        }

        public static string TextoVencimiento(VencimientoFacturaeModel vencimiento)
        {
            if (vencimiento == null)
            {
                return string.Empty;
            }
            string texto = $"{vencimiento.Fecha:dd/MM/yyyy}: {Euros(vencimiento.Importe)}";
            if (!string.IsNullOrWhiteSpace(vencimiento.FormaPago))
            {
                texto += $" ({vencimiento.FormaPago.Trim()})";
            }
            if (!string.IsNullOrWhiteSpace(vencimiento.Iban))
            {
                texto += $" · IBAN {vencimiento.Iban.Trim()}";
            }
            return texto;
        }

        public static string TextoCentro(CentroFacturaeModel centro)
        {
            if (centro == null)
            {
                return string.Empty;
            }
            string texto = $"{centro.Descripcion?.Trim()}: {centro.Codigo?.Trim()}";
            if (!string.IsNullOrWhiteSpace(centro.Nombre))
            {
                texto += $" ({centro.Nombre.Trim()})";
            }
            return texto;
        }

        /// <summary>La carpeta Descargas del usuario (en el escritorio remoto, la suya del servidor).</summary>
        public static string CarpetaDescargas()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    /// <summary>
    /// Nesto#652: elegir dónde guardar un fichero que manda la API, guardarlo y enseñarlo en su carpeta. Detrás de una interfaz
    /// para que los ViewModels no abran nada en las pruebas.
    /// </summary>
    public interface IGuardadorFicherosDescarga
    {
        /// <summary>La ruta elegida, o null si el usuario cancela.</summary>
        string ElegirDondeGuardar(string titulo, string nombrePropuesto, string carpetaInicial, string filtro, string extension);
        void Guardar(string ruta, byte[] contenido);
        /// <summary>Abre el Explorador en la carpeta con el fichero seleccionado. Nunca lanza.</summary>
        void EnsenarEnCarpeta(string ruta);
    }

    public sealed class GuardadorFicherosDescarga : IGuardadorFicherosDescarga
    {
        public string ElegirDondeGuardar(string titulo, string nombrePropuesto, string carpetaInicial, string filtro, string extension)
        {
            var dialogo = new Microsoft.Win32.SaveFileDialog
            {
                Title = titulo,
                FileName = nombrePropuesto,
                DefaultExt = extension,
                Filter = filtro,
                AddExtension = true,
                OverwritePrompt = true
            };
            if (!string.IsNullOrWhiteSpace(carpetaInicial) && Directory.Exists(carpetaInicial))
            {
                dialogo.InitialDirectory = carpetaInicial;
            }
            return dialogo.ShowDialog() == true ? dialogo.FileName : null;
        }

        public void Guardar(string ruta, byte[] contenido) => File.WriteAllBytes(ruta, contenido ?? Array.Empty<byte>());

        public void EnsenarEnCarpeta(string ruta)
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{ruta}\"") { UseShellExecute = true });
            }
            catch (Exception)
            {
                // Solo es una comodidad: el fichero ya está guardado
            }
        }
    }
}
