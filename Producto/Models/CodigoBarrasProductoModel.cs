using System;
using System.Collections.Generic;

namespace Nesto.Modules.Producto.Models
{
    /// <summary>
    /// NestoAPI#605: uno de los códigos de barras de un producto (tabla ProductosCodigosBarras).
    /// Un producto puede tener varios (el proveedor cambia el código por lote, la caja de 100 tiene
    /// el suyo...). El principal es el que sigue en Productos.CodBarras, y lo mantiene la API.
    /// </summary>
    public class CodigoBarrasProductoModel
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        /// <summary>Unidades que representa el código: 1 la unidad, 100 la caja de 100.</summary>
        public int Cantidad { get; set; } = 1;
        public string Proveedor { get; set; }
        public bool Principal { get; set; }
        /// <summary>"Ficha", "Almacen" o "Proveedor": quién lo dio de alta.</summary>
        public string Origen { get; set; }
        public string Usuario { get; set; }
        public DateTime? Fecha { get; set; }
        public bool Activo { get; set; } = true;

        /// <summary>Cómo se enseña el origen en la ficha (la API manda "Almacen", sin tilde).</summary>
        public string OrigenTexto => Origen?.Trim() switch
        {
            "Almacen" => "Almacén",
            null or "" => string.Empty,
            string otro => otro
        };
    }

    /// <summary>NestoAPI#605: producto al que ya pertenece un código (cuerpo del 409 al añadirlo).</summary>
    public class ProductoDelCodigoBarrasModel
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
    }

    public enum ResultadoAnnadirCodigoBarras
    {
        /// <summary>201: el código es nuevo en este producto.</summary>
        Creado,
        /// <summary>200: el código ya era de este producto; no ha cambiado nada.</summary>
        YaEraDelProducto,
        /// <summary>409: el código está activo en otro producto; hay que confirmar que se comparte.</summary>
        EnOtroProducto
    }

    /// <summary>NestoAPI#605: lo que contesta la API al añadir un código a un producto.</summary>
    public class RespuestaAnnadirCodigoBarras
    {
        public ResultadoAnnadirCodigoBarras Resultado { get; set; }
        public string Mensaje { get; set; }
        /// <summary>Con <see cref="ResultadoAnnadirCodigoBarras.EnOtroProducto"/>, los productos que ya lo tienen.</summary>
        public List<ProductoDelCodigoBarrasModel> Productos { get; set; } = new();
    }
}
