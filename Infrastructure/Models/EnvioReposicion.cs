using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Infrastructure.Models
{
    // NestoAPI#553: la tienda prepara la reposición que manda a Algete y la termina (api/Reposiciones).
    // Espejo de los DTO de NestoAPI/Infraestructure/Reposiciones/PreparacionReposicion.cs.

    public class LineaReposicionEnPreparacion
    {
        /// <summary>La línea en el diario de salida: es la que se cambia con PUT …/EnPreparacion/Lineas/{NumeroOrden}.</summary>
        public int NumeroOrden { get; set; }
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string CodigoBarras { get; set; }
        /// <summary>
        /// NestoAPI#762: todos los códigos activos del producto (el principal el primero; también el interno 290…). Vacía si el
        /// producto no tiene ninguno: el origen tiene que etiquetarlo. Null con una API anterior: solo se sabe el principal.
        /// </summary>
        public List<string> CodigosBarras { get; set; }
        public int Cantidad { get; set; }
        /// <summary>Stock en la tienda (el almacén de origen).</summary>
        public int StockOrigen { get; set; }
    }

    /// <summary>GET api/Reposiciones/EnPreparacion (y lo que contestan POST y PUT).</summary>
    public class ReposicionEnPreparacion
    {
        public string Empresa { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string Diario { get; set; }
        public DateTime Fecha { get; set; }
        public string Usuario { get; set; }
        public List<LineaReposicionEnPreparacion> Lineas { get; set; } = new List<LineaReposicionEnPreparacion>();
        public int Unidades => (Lineas ?? new List<LineaReposicionEnPreparacion>()).Sum(l => l.Cantidad);
    }

    public class LineaCrearReposicion
    {
        public string Producto { get; set; }
        public int Cantidad { get; set; }
    }

    /// <summary>POST api/Reposiciones. Sin líneas, el servidor calcula la propuesta (el «Rellenar» de Nesto viejo).</summary>
    public class CrearReposicion
    {
        public string Empresa { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public DateTime? Fecha { get; set; }
        public List<LineaCrearReposicion> Lineas { get; set; }
        /// <summary>NestoAPI#577: quién la crea ('Nesto' | 'Ariadna' | 'Automatico'). Nesto manda siempre «Nesto».</summary>
        public string Herramienta { get; set; }
    }

    /// <summary>
    /// GET api/Reposiciones/AvisosAutomatica (NestoAPI#622): lo que ha hecho hoy el job con una reposición de la tienda que hay
    /// que decirle arriba: que no se ha rellenado (y por qué) o que se ha sumado a una que tenía sin enviar.
    /// Espejo de AvisoReposicionAutomaticaDTO de NestoAPI.
    /// </summary>
    public class AvisoReposicionAutomatica
    {
        public const string OMITIDA = "Omitida";
        public const string COMPLETADA = "Completada";

        public string Origen { get; set; }
        public string Destino { get; set; }
        public DateTime? Corte { get; set; }
        /// <summary><see cref="OMITIDA"/> o <see cref="COMPLETADA"/>.</summary>
        public string Tipo { get; set; }
        public string Texto { get; set; }
    }

    /// <summary>
    /// GET api/Reposiciones/ProximaLlegada (NestoAPI#577): la próxima reposición de una ruta según el calendario.
    /// Espejo de ProximaReposicionDTO de NestoAPI.
    /// </summary>
    public class ProximaReposicion
    {
        public string Origen { get; set; }
        public string Destino { get; set; }
        /// <summary>Día y hora de cierre: a esa hora la API rellena sola la reposición.</summary>
        public DateTime CierraEl { get; set; }
        public DateTime LlegaEl { get; set; }
        public DateTime PedidoSaleEl { get; set; }
        public int DiasHastaSalida { get; set; }
    }

    public class LineaTraspasoTerminado
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public int Cantidad { get; set; }
    }

    /// <summary>POST api/Reposiciones/EnPreparacion/Terminar.</summary>
    public class ResultadoTerminarReposicion
    {
        public int NumTraspaso { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string DiarioSalida { get; set; }
        public string DiarioEntrada { get; set; }
        public List<LineaTraspasoTerminado> Lineas { get; set; } = new List<LineaTraspasoTerminado>();
        public int Unidades => (Lineas ?? new List<LineaTraspasoTerminado>()).Sum(l => l.Cantidad);
    }

    /// <summary>
    /// Sugerencia 581: GET api/Reposiciones/Puntual/Producto, lo leído (o tecleado) en la reposición puntual. Espejo de
    /// ProductoReposicionPuntualDTO de NestoAPI.
    /// </summary>
    public class ProductoReposicionPuntual
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string CodigoBarras { get; set; }
        /// <summary>Las unidades que vale una lectura de ese código (la caja de 6 vale 6); 1 por la referencia.</summary>
        public int UnidadesPorLectura { get; set; } = 1;
        /// <summary>Stock en la tienda.</summary>
        public int StockOrigen { get; set; }
        /// <summary>
        /// Sugerencia 581 (Carlos, 13/10/26): lo que necesitan los pedidos pendientes de la tienda (y lo ya salido sin
        /// contabilizar): eso no se manda.
        /// </summary>
        public int Comprometido { get; set; }
        /// <summary>Sugerencia 581: lo que se puede mandar (stock − comprometido). Null con una API anterior: el stock.</summary>
        public int? Disponible { get; set; }
    }

    /// <summary>
    /// Sugerencia 581 (Carlos, 13/10/26): GET api/Reposiciones/Puntual/Estado: si hoy se puede hacer la reposición puntual y,
    /// si no, por qué; y si el repartidor ya se ha llevado la de hoy. Espejo de EstadoReposicionPuntualDTO de NestoAPI.
    /// </summary>
    public class EstadoReposicionPuntual
    {
        public const string SIN_VIAJE = "SinViaje";
        public const string SIN_RELLENAR = "SinRellenar";
        public const string SIN_ENVIAR = "SinEnviar";
        public const string RECOGIDA = "Recogida";
        public const string ABIERTA = "Abierta";

        public string Origen { get; set; }
        public string Destino { get; set; }
        /// <summary>SinViaje, SinRellenar, SinEnviar, Recogida o Abierta.</summary>
        public string Situacion { get; set; }
        public bool SePuede { get; set; }
        /// <summary>
        /// Por qué no se puede, tal cual («Hoy no hay viaje de Alcobendas a Algete.», «Todavía no se ha rellenado la de hoy (se
        /// rellena sola a las 9:00).», «Primero envía la de hoy.», «El repartidor ya se ha llevado…»). Null si se puede.
        /// </summary>
        public string Motivo { get; set; }
        /// <summary>Cuándo se rellena (o se rellenó) sola la de hoy. Null sin viaje hoy.</summary>
        public DateTime? CierreHoy { get; set; }
        /// <summary>Cuándo se marcó que el repartidor se la ha llevado. Null si no.</summary>
        public DateTime? FechaRecogida { get; set; }
        /// <summary>Quién lo marcó («NUEVAVISION\Paloma»).</summary>
        public string UsuarioRecogida { get; set; }
        /// <summary>Se puede pulsar «El repartidor ya se la ha llevado» (la de hoy enviada y sin marcar).</summary>
        public bool PuedeMarcarRecogida { get; set; }
        /// <summary>
        /// NestoAPI#772 (Nesto#590): los bultos de las reposiciones del viaje de hoy que el repartidor ya ha leído al recogerlos.
        /// Null con una API anterior.
        /// </summary>
        public int? BultosRecogidos { get; set; }
        /// <summary>NestoAPI#772: los bultos de las reposiciones del viaje de hoy de la ruta. Null con una API anterior.</summary>
        public int? BultosTotal { get; set; }
        /// <summary>NestoAPI#772: el repartidor que los está recogiendo (sin dominio).</summary>
        public string RepartidorRecogida { get; set; }
        /// <summary>NestoAPI#772: cuándo leyó el último bulto.</summary>
        public DateTime? FechaUltimoBultoRecogido { get; set; }
    }

    /// <summary>NestoAPI#772 (Nesto#590): un momento de la historia de una reposición o de uno de sus bultos.</summary>
    public class EventoSeguimiento
    {
        public DateTime? FechaHora { get; set; }
        /// <summary>Creada, Preparada, Recogida, Recibida (del traspaso); Etiquetado, Cargado, Entregado, Recogido, Descargado, Recibido (de un bulto).</summary>
        public string Tipo { get; set; }
        public string Usuario { get; set; }
        /// <summary>Repartidor o Almacen.</summary>
        public string Origen { get; set; }
        public string Detalle { get; set; }
    }

    /// <summary>NestoAPI#772: un bulto de la reposición, con su estado y su historia.</summary>
    public class BultoSeguimiento
    {
        public string Codigo { get; set; }
        public int Bulto { get; set; }
        public int Total { get; set; }
        public string Estado { get; set; }
        public List<EventoSeguimiento> Eventos { get; set; } = new List<EventoSeguimiento>();
    }

    /// <summary>NestoAPI#772: GET api/Reposiciones/{traspaso}/Seguimiento, la historia de una reposición por traspaso y por bulto.</summary>
    public class SeguimientoReposicion
    {
        public string Empresa { get; set; }
        public int Traspaso { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string Herramienta { get; set; }
        public List<EventoSeguimiento> Eventos { get; set; } = new List<EventoSeguimiento>();
        public List<BultoSeguimiento> Bultos { get; set; } = new List<BultoSeguimiento>();
    }

    /// <summary>Sugerencia 581: POST api/Reposiciones/Recogida, el repartidor ya se ha llevado la de hoy.</summary>
    public class MarcarRecogidaReposicion
    {
        public string Empresa { get; set; }
        public string Origen { get; set; }
        /// <summary>Null: Algete.</summary>
        public string Destino { get; set; }
    }

    /// <summary>Nesto#530 (NestoAPI#645): una etiqueta de bulto de la reposición enviada.</summary>
    public class BultoReposicion
    {
        /// <summary>«R81010-2».</summary>
        public string Codigo { get; set; }
        public int Bulto { get; set; }
        public int Total { get; set; }
        /// <summary>«81010 · bulto 2/3 · ALC→ALG».</summary>
        public string Texto { get; set; }
    }

    /// <summary>Nesto#530 (NestoAPI#645): POST api/Reposiciones/{traspaso}/Bultos (y Bultos/Reimprimir).</summary>
    public class BultosReposicion
    {
        public string Traspaso { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        /// <summary>La impresora de etiquetas del usuario (parámetro ImpresoraCodBarras) en la que se han impreso.</summary>
        public string Impresora { get; set; }
        public List<BultoReposicion> Bultos { get; set; } = new List<BultoReposicion>();
        public List<string> Avisos { get; set; } = new List<string>();
    }

    /// <summary>
    /// NestoAPI#685: una reposición que ha salido de la tienda y aún no se ha recibido en el destino (GET
    /// api/Reposiciones/Enviadas). Con ella se reimprimen los bultos y el impreso cuando haga falta.
    /// </summary>
    public class ReposicionEnviada
    {
        public int Traspaso { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        /// <summary>«Algete».</summary>
        public string NombreDestino { get; set; }
        public DateTime? Fecha { get; set; }
        public string Usuario { get; set; }
        public int Lineas { get; set; }
        public int Unidades { get; set; }
        /// <summary>Cuántas etiquetas de bulto tiene; 0 si se mandó sin bultos.</summary>
        public int Bultos { get; set; }
    }

    /// <summary>La API no ha aceptado la petición: el motivo que da (inventario en curso, stock negativo, sin permiso…).</summary>
    public class EnvioReposicionException : Exception
    {
        public EnvioReposicionException(string motivo) : base(motivo) { }

        public EnvioReposicionException(string motivo, int codigo) : base(motivo)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP con el que ha contestado la API (null si no se sabe).</summary>
        public int? Codigo { get; }

        /// <summary>NestoAPI#577: 403, el usuario no puede hacer eso (p. ej. rellenar la reposición a mano).</summary>
        public bool EsSinPermiso => Codigo == 403;
    }
}
