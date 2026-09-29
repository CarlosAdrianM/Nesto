using System;

namespace Nesto.Modulos.Cajas.Models
{
    /// <summary>
    /// Nesto#261: un enlace de pago (NestoPago / TPV virtual) tal como lo devuelve GET api/Pagos/Auditoria:
    /// quién lo creó y cuándo, para qué cliente, por cuánto y adónde se envió.
    /// </summary>
    public class EnlacePagoAuditoriaModel
    {
        public int Id { get; set; }
        /// <summary>El identificador del enlace (p. ej. B9BC22C32366).</summary>
        public string? NumeroOrden { get; set; }
        public string? Tipo { get; set; }
        public string? Estado { get; set; }
        public string? Empresa { get; set; }
        public string? Cliente { get; set; }
        public string? Contacto { get; set; }
        public string? NombreCliente { get; set; }
        public decimal Importe { get; set; }
        public string? Descripcion { get; set; }
        public string? Usuario { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaActualizacion { get; set; }
        /// <summary>Correo al que se envió el enlace (vacío si no se envió por correo).</summary>
        public string? Correo { get; set; }
        /// <summary>Móvil al que se envió por SMS (vacío si no se envió por SMS).</summary>
        public string? Movil { get; set; }
        public string? CodigoRespuesta { get; set; }
        public string? CodigoAutorizacion { get; set; }
        public string? MetodoPago { get; set; }
        public int NumeroEfectos { get; set; }
        public string? Documentos { get; set; }
    }

    /// <summary>Nesto#261: filtros de la consulta (todos opcionales). Con identificador se ignoran las fechas.</summary>
    public class FiltroAuditoriaEnlacesPago
    {
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string? Cliente { get; set; }
        public string? Usuario { get; set; }
        public string? Estado { get; set; }
        public string? NumeroOrden { get; set; }
    }
}
