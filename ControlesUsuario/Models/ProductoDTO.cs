namespace ControlesUsuario.Models
{
    /// <summary>
    /// DTO para transferir datos de productos desde el servicio.
    /// Issue #258 - Carlos 11/12/25
    /// </summary>
    public class ProductoDTO
    {
        /// <summary>
        /// Código del producto.
        /// </summary>
        public string Producto { get; set; }

        /// <summary>
        /// Nombre o descripción del producto.
        /// </summary>
        public string Nombre { get; set; }

        /// <summary>
        /// Precio unitario del producto para el cliente/contacto.
        /// </summary>
        public decimal Precio { get; set; }

        /// <summary>
        /// Indica si se debe aplicar descuento al producto.
        /// </summary>
        public bool AplicarDescuento { get; set; }

        /// <summary>
        /// Porcentaje de descuento aplicable.
        /// </summary>
        public decimal Descuento { get; set; }

        /// <summary>
        /// Código de IVA del producto.
        /// </summary>
        public string Iva { get; set; }

        /// <summary>
        /// Stock disponible.
        /// </summary>
        public int Stock { get; set; }

        /// <summary>
        /// Cantidad reservada.
        /// </summary>
        public int CantidadReservada { get; set; }

        /// <summary>
        /// Cantidad disponible (Stock - CantidadReservada).
        /// </summary>
        public int CantidadDisponible { get; set; }
    }
}
