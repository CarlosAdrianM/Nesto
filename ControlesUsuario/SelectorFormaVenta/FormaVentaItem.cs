namespace ControlesUsuario.Models
{
    /// <summary>
    /// Modelo para las Formas de Venta usadas en el SelectorFormaVenta.
    /// Carlos 04/12/2024: Issue #252 - Creado para el nuevo control SelectorFormaVenta.
    /// </summary>
    public class FormaVentaItem
    {
        /// <summary>
        /// Código de la empresa.
        /// </summary>
        public string Empresa { get; set; }

        /// <summary>
        /// Número/código de la forma de venta (clave primaria junto con Empresa).
        /// </summary>
        public string Numero { get; set; }

        /// <summary>
        /// Descripción de la forma de venta para mostrar al usuario.
        /// </summary>
        public string Descripcion { get; set; }

        /// <summary>
        /// Indica si la forma de venta es visible para comerciales.
        /// </summary>
        public bool VisiblePorComerciales { get; set; }

        /// <summary>
        /// Texto formateado para mostrar en el ComboBox: "Numero - Descripcion".
        /// </summary>
        public string TextoFormateado => $"{Numero} - {Descripcion}";
    }
}
