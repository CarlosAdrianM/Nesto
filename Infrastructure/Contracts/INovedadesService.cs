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

        // NestoAPI#526/#527: solo vienen en las sugerencias (novedades sin versión) y en el buscador.
        /// <summary>Lo que escribió el usuario, tal cual. La <see cref="Descripcion"/> es la versión ampliada, si la hay.</summary>
        public string TextoOriginal { get; set; }
        public string SugeridaNombre { get; set; }
        public DateTime? SugeridaFecha { get; set; }
        /// <summary>Pendiente, Aceptada, Implementada o Descartada.</summary>
        public string Estado { get; set; }
        /// <summary>La sugerencia lleva captura (se pide aparte con <see cref="INovedadesService.LeerImagenNovedad"/>).</summary>
        public bool TieneImagen { get; set; }

        /// <summary>Sin versión = sugerencia de un usuario, todavía sin implementar.</summary>
        public bool EsSugerencia => string.IsNullOrWhiteSpace(Version);
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

    /// <summary>
    /// Nesto#491 (NestoAPI#537): alguien a quien se puede mencionar con @Nombre en un comentario de Novedades.
    /// </summary>
    public class Mencionable
    {
        /// <summary>Lo que se escribe tras la @ (p. ej. «Alfredo»).</summary>
        public string Nombre { get; set; }
        /// <summary>A quién le llega el aviso (p. ej. «NUEVAVISION\Alfredo»).</summary>
        public string Clave { get; set; }
        public string Aplicacion { get; set; }
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

        // NestoAPI#526/#527: sugerencias de los usuarios y buscador. También lanzan con el mensaje de la API.

        /// <summary>Sugerencias abiertas (sin versión), las más votadas primero.</summary>
        Task<List<NovedadUsuario>> LeerSugerencias();
        /// <summary>Crea una sugerencia; <paramref name="imagenPng"/> es opcional (captura en PNG).</summary>
        Task<NovedadUsuario> Sugerir(string texto, byte[] imagenPng);
        /// <summary>La captura de una sugerencia.</summary>
        Task<byte[]> LeerImagenNovedad(int novedadId);
        /// <summary>Novedades (con versión) y sugerencias (sin versión) que tienen todas las palabras.</summary>
        Task<List<NovedadUsuario>> Buscar(string texto);

        // Nesto#491 (NestoAPI#537): @menciones. Lanza si la API falla (la ventana se queda sin desplegable).

        /// <summary>Los usuarios de Nesto a los que se puede mencionar con @Nombre.</summary>
        Task<List<Mencionable>> LeerMencionables();
    }
}
