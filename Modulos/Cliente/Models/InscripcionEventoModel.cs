using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>
    /// Nesto#517 (NestoAPI#611): una inscripción a un evento (InscripcionEventoDTO de api/Eventos/{id}/Inscripciones).
    /// No es un pedido: Reservada → Pagada → Facturada → Asistio / NoAsistio, o Caducada / Anulada. Los estados y las reglas
    /// viven en la API; aquí solo se pintan y se decide qué botones tienen sentido.
    /// </summary>
    public class InscripcionEventoModel : ObservableObject
    {
        public const string RESERVADA = "Reservada";
        public const string PAGADA = "Pagada";
        public const string FACTURADA = "Facturada";
        public const string ASISTIO = "Asistio";
        public const string NO_ASISTIO = "NoAsistio";
        public const string CADUCADA = "Caducada";
        public const string ANULADA = "Anulada";
        /// <summary>NestoAPI#690: la gratuita nace así; no caduca, no paga y entra directamente en la lista.</summary>
        public const string INSCRITA = "Inscrita";

        /// <summary>Las que entran en la lista de asistencia (las pagadas y las que ya tienen lista, para corregirla).</summary>
        public static readonly IReadOnlyCollection<string> EN_LISTA_ASISTENCIA = new[] { PAGADA, FACTURADA, INSCRITA, ASISTIO, NO_ASISTIO };

        public int Id { get; set; }
        public int EventoId { get; set; }
        public string Evento { get; set; }
        public DateTime FechaEvento { get; set; }
        public string Almacen { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        /// <summary>Nesto, NestoApp, TNV, PrestaShop, Telefono o Transferencia.</summary>
        public string Origen { get; set; }
        /// <summary>Reservada, Pagada, Facturada, Asistio, NoAsistio, Caducada o Anulada.</summary>
        public string Estado { get; set; }
        /// <summary>«Asistió», «No asistió»… (con tildes, para pintar).</summary>
        public string EstadoTexto { get; set; }
        /// <summary>Hasta ese día (incluido) se puede pagar.</summary>
        public DateTime? FechaLimitePago { get; set; }
        /// <summary>Tarjeta, Transferencia o Efectivo (mientras está Reservada, la prevista).</summary>
        public string FormaPago { get; set; }
        public decimal? ImportePagado { get; set; }
        public DateTime? FechaPago { get; set; }
        public string UsuarioPago { get; set; }
        public int? PagoTPVId { get; set; }
        /// <summary>La página del enlace de pago, solo mientras se puede pagar (Reservada o Caducada).</summary>
        public string UrlPago { get; set; }
        public int? EventoSenalId { get; set; }
        public string UsuarioCreacion { get; set; }
        public DateTime FechaCreacion { get; set; }
        /// <summary>NestoAPI#612: la facturación del curso. Null si no ha empezado.</summary>
        public FacturacionCursoModel Facturacion { get; set; }

        // NestoAPI#690 (contrato del 22/10/26): modalidad, confirmación de asistencia y base imponible del día.

        /// <summary>Reembolsable, NoReembolsable o Gratuita. Las de antes salen como Reembolsable (null con la API de antes).</summary>
        public string Modalidad { get; set; }
        /// <summary>Lo que se cobra por esta inscripción según su modalidad (0 si es gratuita).</summary>
        public decimal? ImporteSenal { get; set; }
        /// <summary>Solo la Reembolsable emite cupón.</summary>
        public bool ConCupon { get; set; }
        /// <summary>La respuesta al recordatorio (o la que apunta la tienda). Null = todavía no ha contestado.</summary>
        public AsistenciaConfirmadaModel AsistenciaConfirmada { get; set; }
        /// <summary>Pendiente, Confirmada, NoPodraIr, Asistio o NoAsistio.</summary>
        public string EstadoAsistencia { get; set; }
        /// <summary>«Pendiente», «Confirmada», «No podrá ir», «Asistió», «No asistió».</summary>
        public string EstadoAsistenciaTexto { get; set; }
        public DateTime? RecordatorioEnviado { get; set; }
        /// <summary>
        /// Base imponible de los pedidos del cliente del día del evento en Reina o Alcobendas (sin presupuestos ni el curso).
        /// Solo en los inscritos del evento y en la respuesta de la lista; null en los demás.
        /// </summary>
        public decimal? BaseImponibleDia { get; set; }
        /// <summary>
        /// Sugerencia 776: avisos (no errores) de la API al inscribir, p. ej. que a la ficha le falta el teléfono o el correo.
        /// Solo en la respuesta de inscribir; null en el resto y con la API de antes.
        /// </summary>
        public List<string> Avisos { get; set; }

        [JsonIgnore]
        public bool EsGratuita => Modalidad == InscribirEventoModel.MODALIDAD_GRATUITA || Estado == INSCRITA;

        [JsonIgnore]
        public string TextoModalidad => Modalidad switch
        {
            InscribirEventoModel.MODALIDAD_NO_REEMBOLSABLE => "No reembolsable",
            InscribirEventoModel.MODALIDAD_GRATUITA => "Gratuita",
            _ => "Reembolsable"
        };

        /// <summary>Lo que se ve en la columna «Asistencia»: el texto de la API o, con la de antes, Pendiente / Asistió / No asistió.</summary>
        [JsonIgnore]
        public string TextoAsistencia
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(EstadoAsistenciaTexto))
                {
                    return EstadoAsistenciaTexto;
                }
                switch (Estado)
                {
                    case ASISTIO: return "Asistió";
                    case NO_ASISTIO: return "No asistió";
                }
                switch (AsistenciaConfirmada?.Respuesta)
                {
                    case AsistenciaConfirmadaModel.CONFIRMA: return "Confirmada";
                    case AsistenciaConfirmadaModel.NO_PODRA_IR: return "No podrá ir";
                    default: return "Pendiente";
                }
            }
        }

        /// <summary>
        /// La tienda puede apuntar la respuesta (Reservada, Pagada, Facturada o Inscrita) hasta el día del evento incluido.
        /// </summary>
        [JsonIgnore]
        public bool SePuedeConfirmarAsistencia => (Estado == RESERVADA || Estado == PAGADA || Estado == FACTURADA || Estado == INSCRITA)
            && FechaEvento.Date >= DateTime.Today;

        [JsonIgnore]
        public string ClienteContacto => $"{Cliente?.Trim()}/{Contacto?.Trim()}";

        [JsonIgnore]
        public string TextoOrigen => Origen == "Telefono" ? "Teléfono" : Origen;

        [JsonIgnore]
        public bool EstaReservada => Estado == RESERVADA;

        /// <summary>Se anula como una Reservada; NestoAPI#690: también la Inscrita (gratuita).</summary>
        [JsonIgnore]
        public bool SePuedeAnular => Estado == RESERVADA || Estado == INSCRITA;

        /// <summary>Se puede marcar como pagada a mano: Reservada o Caducada (paga tarde).</summary>
        [JsonIgnore]
        public bool SePuedeMarcarPagada => !EsGratuita && (Estado == RESERVADA || Estado == CADUCADA);

        [JsonIgnore]
        public bool EntraEnListaAsistencia => Estado != null && (((ICollection<string>)EN_LISTA_ASISTENCIA).Contains(Estado)
            // NestoAPI#690: la gratuita no paga y pasa directamente a la lista.
            || (EsGratuita && Estado != CADUCADA && Estado != ANULADA));

        /// <summary>Con la facturación en Error o NoAutomatizable (o pagada sin empezar) tiene sentido relanzarla.</summary>
        [JsonIgnore]
        public bool SePuedeFacturar =>
            !EsGratuita && (Estado == PAGADA || Estado == ASISTIO || Estado == NO_ASISTIO)
            && (Facturacion == null || Facturacion.Estado == FacturacionCursoModel.ERROR || Facturacion.Estado == FacturacionCursoModel.NO_AUTOMATIZABLE);

        /// <summary>«CV2600700 (Facturada)», «Error», o vacío (columna de la lista).</summary>
        [JsonIgnore]
        public string TextoFacturacion => Facturacion == null
            ? string.Empty
            : string.IsNullOrWhiteSpace(Facturacion.Factura)
                ? Facturacion.TextoEstado
                : $"{Facturacion.Factura.Trim()} ({Facturacion.TextoEstado})";

        private bool _asistio;
        /// <summary>La casilla de la lista de asistencia (empieza marcada si ya consta que vino).</summary>
        [JsonIgnore]
        public bool Asistio
        {
            get => _asistio;
            set => SetProperty(ref _asistio, value);
        }
    }

    /// <summary>NestoAPI#612: InscripcionEventoDTO.Facturacion (pedido, factura exenta y liquidación del curso).</summary>
    public class FacturacionCursoModel
    {
        public const string EN_CURSO = "EnCurso";
        public const string FACTURADA = "Facturada";
        public const string ERROR = "Error";
        public const string NO_AUTOMATIZABLE = "NoAutomatizable";

        /// <summary>EnCurso, Facturada, Error o NoAutomatizable.</summary>
        public string Estado { get; set; }
        public string EmpresaPedido { get; set; }
        public int? Pedido { get; set; }
        public string EmpresaFactura { get; set; }
        public string Factura { get; set; }
        /// <summary>Pendiente, Liquidada o SinCobro.</summary>
        public string EstadoLiquidacion { get; set; }
        /// <summary>El motivo del error, de que no se automatice o de que no haya nada que liquidar.</summary>
        public string Mensaje { get; set; }
        public int Intentos { get; set; }
        public DateTime FechaModificacion { get; set; }

        [JsonIgnore]
        public string TextoEstado
        {
            get
            {
                switch (Estado)
                {
                    case EN_CURSO: return "En curso";
                    case NO_AUTOMATIZABLE: return "No automatizable";
                    default: return Estado;
                }
            }
        }
    }

    /// <summary>Nesto#517: cuerpo de POST api/Eventos/{id}/Inscripciones.</summary>
    public class InscribirEventoModel
    {
        public const string ORIGEN_NESTO = "Nesto";
        public const string ORIGEN_TELEFONO = "Telefono";
        public const string MODALIDAD_REEMBOLSABLE = "Reembolsable";
        public const string MODALIDAD_NO_REEMBOLSABLE = "NoReembolsable";
        public const string MODALIDAD_GRATUITA = "Gratuita";

        public string Empresa { get; set; }
        public string Cliente { get; set; }
        /// <summary>Si no viene, el contacto principal del cliente.</summary>
        public string Contacto { get; set; }
        /// <summary>Obligatorio: Nesto o Telefono.</summary>
        public string Origen { get; set; }
        /// <summary>Tarjeta, Transferencia o Efectivo. Opcional.</summary>
        public string FormaPago { get; set; }
        /// <summary>Solo con Tarjeta (o sin forma de pago) se genera el enlace.</summary>
        public bool GenerarEnlacePago { get; set; } = true;
        /// <summary>Si viene, la API manda el enlace por correo al cliente.</summary>
        public string Correo { get; set; }
        /// <summary>NestoAPI#690 (provisional): Reembolsable, NoReembolsable o Gratuita. Null = no se manda (API de antes).</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Modalidad { get; set; }
    }

    /// <summary>Nesto#517: cuerpo de POST api/Eventos/Inscripciones/{id}/Pago (marcar pagada a mano).</summary>
    public class MarcarPagoInscripcionModel
    {
        public const string TARJETA = "Tarjeta";
        public const string TRANSFERENCIA = "Transferencia";
        public const string EFECTIVO = "Efectivo";

        public static readonly IReadOnlyList<string> FORMAS_PAGO = new[] { TARJETA, TRANSFERENCIA, EFECTIVO };

        /// <summary>Tarjeta, Transferencia o Efectivo.</summary>
        public string FormaPago { get; set; }
        /// <summary>Si no viene, el importe de la señal del evento.</summary>
        public decimal? Importe { get; set; }
        /// <summary>Si no viene, ahora. No puede ser futura.</summary>
        public DateTime? Fecha { get; set; }
    }

    /// <summary>Nesto#517: cuerpo de POST api/Eventos/{id}/Asistencia (los Id de las inscripciones que vinieron).</summary>
    public class AsistenciaEventoModel
    {
        public List<int> Asistentes { get; set; } = new List<int>();
    }

    /// <summary>Nesto#517: lo que se enseña de la referencia del curso (ProductoPlantillaDTO de GET api/Productos).</summary>
    public class ProductoCursoModel
    {
        [JsonProperty("producto")]
        public string Producto { get; set; }
        [JsonProperty("nombre")]
        public string Nombre { get; set; }
        [JsonProperty("precio")]
        public decimal Precio { get; set; }
        /// <summary>El IVA de la ficha: los cursos son EX.</summary>
        [JsonProperty("iva")]
        public string Iva { get; set; }
        /// <summary>El grupo de la ficha (GET api/Productos con fichaCompleta=false): los cursos son CUR. Null si no se sabe.</summary>
        [JsonProperty("Grupo")]
        public string Grupo { get; set; }

        public const string GRUPO_CURSOS = "CUR";
    }

    /// <summary>Nesto#517: cuerpo de POST api/Eventos/{id}/ProductoCurso.</summary>
    public class CrearProductoCursoModel
    {
        /// <summary>Reembolsable o NoReembolsable.</summary>
        public string Modalidad { get; set; }
    }

    /// <summary>Nesto#517: respuesta de POST api/Eventos/{id}/ProductoCurso.</summary>
    public class ProductoCursoCreadoModel
    {
        /// <summary>«EVENTO00123» (reembolsable) o «EVENTO00123NR» (no reembolsable).</summary>
        public string Producto { get; set; }
        public string Nombre { get; set; }
        /// <summary>False si ya existía (no se ha tocado).</summary>
        public bool Creado { get; set; }
    }

    /// <summary>Nesto#517: una opción de un combo (lo que se ve y lo que se manda a la API).</summary>
    public class OpcionEvento
    {
        public OpcionEvento(string valor, string texto)
        {
            Valor = valor;
            Texto = texto;
        }

        public string Valor { get; }
        public string Texto { get; }

        public override string ToString() => Texto;
    }

    /// <summary>NestoAPI#690: InscripcionEventoDTO.AsistenciaConfirmada.</summary>
    public class AsistenciaConfirmadaModel
    {
        public const string CONFIRMA = "Confirma";
        public const string NO_PODRA_IR = "NoPodraIr";

        /// <summary>Confirma o NoPodraIr.</summary>
        public string Respuesta { get; set; }
        public DateTime? Fecha { get; set; }
        public string Usuario { get; set; }
        /// <summary>Cliente o Tienda.</summary>
        public string Origen { get; set; }
    }

    /// <summary>NestoAPI#690: cuerpo de POST api/Eventos/Inscripciones/{id}/ConfirmarAsistencia.</summary>
    public class ConfirmarAsistenciaModel
    {
        public string Respuesta { get; set; }
    }
}
