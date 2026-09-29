using System;

namespace Nesto.Modulos.Cajas.Models
{
    /// <summary>
    /// NestoAPI#522: una factura que Verifactu todavía no da por buena (GET api/Verifactu/FacturasPendientes):
    /// sin registrar o marcada por la AEAT como incorrecta o rechazada, con el motivo y qué hacer.
    /// </summary>
    public class FacturaPendienteVerifactuModel
    {
        public string? Empresa { get; set; }
        public string? Numero { get; set; }
        public string? Serie { get; set; }
        public DateTime Fecha { get; set; }
        public string? Cliente { get; set; }
        public string? Contacto { get; set; }
        public string? Nombre { get; set; }
        public string? Situacion { get; set; }
        public string? Estado { get; set; }
        public string? Motivo { get; set; }
        public string? QueHacer { get; set; }
        public DateTime? UltimoIntento { get; set; }
        public bool PuedeReintentar { get; set; }
        /// <summary>NestoAPI#392: marcada para declararse como simplificada (F2; si es rectificativa, R5).</summary>
        public bool DeclararSimplificada { get; set; }
        /// <summary>NestoAPI#392: el problema es el NIF y se puede ofrecer «Declarar como simplificada».</summary>
        public bool PuedeDeclararSimplificada { get; set; }
    }

    /// <summary>NestoAPI#522: respuesta de POST api/Verifactu/ReintentarFactura.</summary>
    public class ResultadoReintentoVerifactuModel
    {
        public bool Exitoso { get; set; }
        public string? Mensaje { get; set; }
        /// <summary>La factura tal y como queda si sigue pendiente; null si ya no lo está.</summary>
        public FacturaPendienteVerifactuModel? Factura { get; set; }
    }
}
