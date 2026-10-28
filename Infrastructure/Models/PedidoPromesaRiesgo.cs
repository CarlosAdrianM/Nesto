using System.Collections.Generic;
using System;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Nesto#546 (NestoAPI#693): un pedido cuya fecha de entrega a la agencia prometida al cliente ya no se cumple, tal y como
    /// lo devuelve GET api/PedidosVenta/PromesaEnRiesgo (PedidoPromesaRiesgoDTO). Quién lo ve (el vendedor los suyos, el
    /// jefe los de su equipo, Dirección todos) lo decide la API. Las fechas prometida y nueva son días, sin hora.
    /// </summary>
    public class PedidoPromesaRiesgo
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public int Pedido { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string NombreCliente { get; set; }
        /// <summary>El de la cabecera (estética).</summary>
        public string Vendedor { get; set; }
        /// <summary>El de peluquería del pedido, si lo tiene.</summary>
        public string VendedorPeluqueria { get; set; }
        public string Ruta { get; set; }
        /// <summary>La que se le dio al cliente al crear el pedido.</summary>
        public DateTime FechaPrometida { get; set; }
        /// <summary>La de ahora. Null = sin fecha.</summary>
        public DateTime? FechaNueva { get; set; }
        /// <summary>Qué productos fallan y por qué, de usted (lo escribe la API).</summary>
        public string Motivo { get; set; }
        /// <summary>El evento que lo detectó (o lo empeoró por última vez).</summary>
        public string Evento { get; set; }
        public DateTime Detectado { get; set; }
        public DateTime Actualizado { get; set; }
        public DateTime? AvisadoCampana { get; set; }
        public DateTime? AvisadoCorreo { get; set; }
        /// <summary>Cuándo se resolvió (null = sigue en riesgo). Solo vienen pidiéndolos (resueltos=true).</summary>
        public DateTime? Resuelto { get; set; }
        public string MotivoResolucion { get; set; }

        public bool SinFecha => !FechaNueva.HasValue;
        public bool EstaResuelto => Resuelto.HasValue;
        /// <summary>Días de retraso (null si está sin fecha).</summary>
        public int? DiasRetraso => FechaNueva.HasValue ? (int?)(FechaNueva.Value.Date - FechaPrometida.Date).TotalDays : null;
    }

    /// <summary>La API no ha devuelto la lista: el mensaje es el suyo (403 si no puede ver ese vendedor, 401, 500…).</summary>
    public class PedidosPromesaRiesgoException : Exception
    {
        public PedidosPromesaRiesgoException(string motivo, int codigo) : base(motivo)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP con el que ha contestado la API.</summary>
        public int Codigo { get; }
    }
    /// <summary>
    /// NestoAPI#693 (Carlos, 28/10/26): qué avisos de pedidos en riesgo recibe el usuario (GET/PUT api/PedidosVenta/PromesaEnRiesgo/Avisos):
    /// Todo | SoloCorreo | SoloCampana | Nada, con el texto de cada opción para el desplegable.
    /// </summary>
    public class AvisosPromesaRiesgo
    {
        public string Valor { get; set; }
        public string Texto { get; set; }
        public List<OpcionAvisosPromesaRiesgo> Opciones { get; set; } = new List<OpcionAvisosPromesaRiesgo>();
    }

    public class OpcionAvisosPromesaRiesgo
    {
        public string Valor { get; set; }
        public string Texto { get; set; }
    }
}
