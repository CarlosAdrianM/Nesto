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

        // NestoAPI#520: feedback de los usuarios. Vienen a null si la API aún no tiene las tablas de
        // feedback (o fallan): entonces la ventana se comporta como siempre, sin votos ni comentarios.
        public int? VotosPositivos { get; set; }
        public int? VotosNegativos { get; set; }
        /// <summary>1, -1 o null si el usuario no ha votado.</summary>
        public short? MiVoto { get; set; }
        public int? NumeroComentarios { get; set; }
    }

    /// <summary>NestoAPI#520: comentario de un usuario en una novedad (la imagen se pide aparte).</summary>
    public class ComentarioNovedad
    {
        public int Id { get; set; }
        public int NovedadId { get; set; }
        public string NombreVisible { get; set; }
        public string Cliente { get; set; }
        public string VersionCliente { get; set; }
        public string Texto { get; set; }
        public DateTime Fecha { get; set; }
        public bool TieneImagen { get; set; }
        /// <summary>Lo escribió quien pregunta: puede borrarlo.</summary>
        public bool EsMio { get; set; }
    }

    public interface INovedadesService
    {
        /// <summary>
        /// Devuelve las novedades publicadas; con <paramref name="desdeVersion"/> solo las de
        /// versiones posteriores. Nunca lanza: ante cualquier error devuelve lista vacía
        /// (las novedades no deben bloquear el arranque de Nesto).
        /// </summary>
        Task<List<NovedadUsuario>> ObtenerNovedades(string desdeVersion = null);

        // NestoAPI#520: feedback. A diferencia de ObtenerNovedades, estos SÍ lanzan (con el mensaje de la
        // API) para que la ventana se lo cuente al usuario en vez de callarlo.

        /// <summary>1 me gusta, -1 no me gusta, 0 quitar el voto.</summary>
        Task VotarNovedad(int novedadId, short voto);
        Task<List<ComentarioNovedad>> LeerComentarios(int novedadId);
        /// <summary>Publica un comentario; <paramref name="imagenPng"/> es opcional (captura en PNG).</summary>
        Task<ComentarioNovedad> Comentar(int novedadId, string texto, byte[] imagenPng);
        Task<byte[]> LeerImagenComentario(int comentarioId);
        Task BorrarComentario(int comentarioId);
    }
}
