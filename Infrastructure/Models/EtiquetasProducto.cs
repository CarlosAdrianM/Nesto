using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Nesto#534 (NestoAPI#654): de qué producto se pide la etiqueta de código de barras a la API
    /// (POST api/Almacen/EtiquetasProducto/Imprimir) y cuántas.
    /// </summary>
    public class PeticionEtiquetasProducto
    {
        public string Producto { get; set; }
        /// <summary>Sin él (0), una.</summary>
        public int Copias { get; set; }
        /// <summary>
        /// NestoAPI#762: qué etiqueta. Null o <see cref="FORMATO_REFERENCIA"/>, la de siempre (Code128 con la referencia);
        /// <see cref="FORMATO_EAN13"/>, la del EAN-13 de la unidad (si el producto no tiene, la API le asigna el interno 290…).
        /// Una API anterior lo ignora e imprime la de la referencia.
        /// </summary>
        public string Formato { get; set; }

        public const string FORMATO_REFERENCIA = "Referencia";
        public const string FORMATO_EAN13 = "Ean13";
    }

    public class ResultadoEtiquetasProducto
    {
        public string Producto { get; set; }
        /// <summary>El texto que lleva encima del código (nombre, subgrupo, tamaño).</summary>
        public List<string> Lineas { get; set; } = new List<string>();
        public int Copias { get; set; }
        /// <summary>Las que han salido por la impresora (0 en el ensayo).</summary>
        public int Impresas { get; set; }
        /// <summary>La impresora de etiquetas de producto del usuario (ImpresoraCodBarras).</summary>
        public string Impresora { get; set; }
        public string Mensaje { get; set; }
        /// <summary>NestoAPI#762 (formato Ean13): el EAN-13 impreso. Null con el formato de siempre o con una API anterior.</summary>
        public string CodigoBarras { get; set; }
        /// <summary>NestoAPI#762: el código impreso es el interno (290… o 299…), no el del fabricante.</summary>
        public bool CodigoInterno { get; set; }
        /// <summary>
        /// NestoAPI#762: el código se acaba de crear. La lista abierta no lo tiene: hay que añadirlo a los códigos de la línea
        /// para que la lectura de la etiqueta case.
        /// </summary>
        public bool CodigoAsignado { get; set; }
    }

    /// <summary>La API no ha impreso la etiqueta: el mensaje es su motivo, para enseñarlo tal cual.</summary>
    public class EtiquetasProductoException : Exception
    {
        public EtiquetasProductoException(string message) : base(message)
        {
        }
    }
}
