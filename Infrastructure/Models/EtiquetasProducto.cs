using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Nesto#534 (NestoAPI#654): de qué producto se pide la etiqueta de código de barras a la API
    /// (POST api/Almacen/EtiquetasProducto/Imprimir) y cuántas. Desde NestoAPI#791 la etiqueta es siempre la del EAN-13 de la
    /// unidad (si el producto no tiene código, la API le da el interno 29… al imprimir): ya no se elige formato.
    /// </summary>
    public class PeticionEtiquetasProducto
    {
        public string Producto { get; set; }
        /// <summary>Sin él (0), una.</summary>
        public int Copias { get; set; }
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
        /// <summary>NestoAPI#762/#791: el EAN-13 impreso (el del fabricante o el interno 29…).</summary>
        public string CodigoBarras { get; set; }
        /// <summary>NestoAPI#762: el código impreso es el interno (290… o 299…), no el del fabricante.</summary>
        public bool CodigoInterno { get; set; }
        /// <summary>
        /// NestoAPI#762/#791: el código interno se acaba de crear al imprimir. La lista abierta no lo tiene: hay que añadirlo a
        /// los códigos de la línea (o de la ficha) para que la lectura de la etiqueta case.
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
