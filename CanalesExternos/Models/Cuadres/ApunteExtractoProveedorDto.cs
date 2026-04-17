using System;

namespace Nesto.Modulos.CanalesExternos.Models.Cuadres
{
    /// <summary>
    /// Deserialización del payload devuelto por <c>GET api/Informes/ExtractoProveedor</c>.
    /// Convención del signo en <see cref="Importe"/>:
    /// positivo → HABER (factura pendiente), negativo → DEBE (pago realizado).
    /// </summary>
    public class ApunteExtractoProveedorDto
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string Documento { get; set; }

        /// <summary>Identificador del documento del proveedor (p. ej. Amazon InvoiceId o FinancialEventGroupId).</summary>
        public string DocumentoProveedor { get; set; }

        public string Concepto { get; set; }
        public decimal Importe { get; set; }
        public decimal ImportePendiente { get; set; }
        public string TipoApunte { get; set; }
        public string FormaPago { get; set; }
        public string Efecto { get; set; }
        public string Delegacion { get; set; }
        public string FormaVenta { get; set; }
    }
}
