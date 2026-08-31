using System;

namespace Nesto.Modulos.OfertasCombinadas.Models
{
    /// <summary>
    /// Una oferta permitida de un producto concreto: el "6+2" de toda la vida.
    ///
    /// Hasta ahora solo se podían meter desde Nesto viejo, donde NO se les puede poner fecha
    /// —la tabla no tenía columnas de fecha—, así que apagar una oferta era borrar la fila y
    /// acordarse de hacerlo.
    ///
    /// Las ofertas de un CLIENTE concreto no salen aquí a propósito: son otra cosa y su sitio es
    /// la ficha de ese cliente.
    /// </summary>
    public class OfertaProductoModel
    {
        /// <summary>Nº Orden de la fila. 0 mientras no está guardada.</summary>
        public int NOrden { get; set; }

        public string Empresa { get; set; }
        public string Producto { get; set; }

        /// <summary>Lo rellena el servidor, para no tener que buscar la referencia en otra ventana.</summary>
        public string ProductoNombre { get; set; }

        /// <summary>Las unidades que se cobran. En un 6+2, el 6.</summary>
        public short CantidadConPrecio { get; set; }

        /// <summary>Las que van de regalo. En un 6+2, el 2.</summary>
        public short CantidadRegalo { get; set; }

        /// <summary>Prohíbe expresamente la oferta en vez de permitirla.</summary>
        public bool Denegar { get; set; }

        public string FiltroProducto { get; set; }

        /// <summary>Nulas = sin límite por ese lado. Inclusivas: hasta el 30/09 vale todo el día 30.</summary>
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        /// <summary>Lo calcula el servidor: si la oferta está en vigor hoy.</summary>
        public bool Vigente { get; set; }

        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }
    }
}
