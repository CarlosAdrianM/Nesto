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
    }

    /// <summary>La API no ha impreso la etiqueta: el mensaje es su motivo, para enseñarlo tal cual.</summary>
    public class EtiquetasProductoException : Exception
    {
        public EtiquetasProductoException(string message) : base(message)
        {
        }
    }
}
