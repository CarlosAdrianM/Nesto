using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Qué etiquetas de hueco se piden a la API (POST api/Almacen/EtiquetasHueco/Imprimir): o una lista de huecos
    /// (9 cifras PPPFFFCCC o 002/004/001), o un rango de un pasillo con filas y columnas desde-hasta (3 cifras).
    /// </summary>
    public class PeticionEtiquetasHueco
    {
        public List<string> Huecos { get; set; }
        public string Pasillo { get; set; }
        public string FilaDesde { get; set; }
        public string FilaHasta { get; set; }
        public string ColumnaDesde { get; set; }
        public string ColumnaHasta { get; set; }
        /// <summary>Del rango, solo los huecos que tienen algo ubicado.</summary>
        public bool SoloEnUso { get; set; }
    }

    public class ResultadoEtiquetasHueco
    {
        /// <summary>Las que han salido por la impresora (0 en el ensayo).</summary>
        public int Impresas { get; set; }
        /// <summary>La impresora de etiquetas de producto del usuario (ImpresoraCodBarras).</summary>
        public string Impresora { get; set; }
        /// <summary>Los huecos, en 9 cifras.</summary>
        public List<string> Huecos { get; set; } = new List<string>();
        public string Mensaje { get; set; }
    }

    /// <summary>La API no ha hecho las etiquetas: el mensaje es su motivo, para enseñarlo tal cual.</summary>
    public class EtiquetasHuecoException : Exception
    {
        public EtiquetasHuecoException(string message) : base(message)
        {
        }
    }
}
