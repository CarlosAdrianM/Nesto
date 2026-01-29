using Prism.Mvvm;
using System;

namespace ControlesUsuario.Models
{
    /// <summary>
    /// DTO para facturas de cliente en el SelectorFacturas.
    /// Issue #279 - SelectorFacturas
    /// </summary>
    public class FacturaClienteDTO : BindableBase
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public DateTime Fecha { get; set; }
        public string Documento { get; set; }
        public string Concepto { get; set; }
        public decimal Importe { get; set; }
        public string CCC { get; set; }
        public string Vendedor { get; set; }

        private bool _seleccionada;
        /// <summary>
        /// Indica si la factura esta seleccionada (checkbox marcado).
        /// </summary>
        public bool Seleccionada
        {
            get => _seleccionada;
            set => SetProperty(ref _seleccionada, value);
        }

        /// <summary>
        /// Indica si es una factura rectificativa (abono).
        /// Se determina por el prefijo del documento (ej: empieza por "R").
        /// </summary>
        public bool EsRectificativa =>
            !string.IsNullOrEmpty(Documento) &&
            (Documento.TrimStart().StartsWith("R", StringComparison.OrdinalIgnoreCase) ||
             Importe < 0);
    }
}
