using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#372: entrada del changelog en lenguaje de usuario (GET api/Novedades).
    /// </summary>
    public class NovedadUsuario
    {
        public int Id { get; set; }
        public string Version { get; set; }
        public DateTime Fecha { get; set; }
        /// <summary>Nuevo / Mejorado / Corregido</summary>
        public string Categoria { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string Ambito { get; set; }
    }

    public interface INovedadesService
    {
        /// <summary>
        /// Devuelve las novedades publicadas; con <paramref name="desdeVersion"/> solo las de
        /// versiones posteriores. Nunca lanza: ante cualquier error devuelve lista vacía
        /// (las novedades no deben bloquear el arranque de Nesto).
        /// </summary>
        Task<List<NovedadUsuario>> ObtenerNovedades(string desdeVersion = null);
    }
}
