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

        /// <summary>NestoAPI#558: «Algo no funciona» (categoría Incidencia), no una idea nueva.</summary>
        public bool EsIncidencia { get; set; }
        /// <summary>
        /// NestoAPI#558: en las incidencias, versión, pantalla y errores de ELMAH de la última hora. La API
        /// solo lo manda a Dirección / Informática.
        /// </summary>
        public string Contexto { get; set; }

        /// <summary>
        /// Nesto#519 (NestoAPI#616): los ficheros adjuntos (PDF e imágenes). null = la API todavía no los
        /// conoce (no trae la propiedad): no se enseñan ni los chips ni el botón de adjuntar.
        /// </summary>
        public List<AdjuntoNovedad> Adjuntos { get; set; }

        /// <summary>
        /// Sugerencia 551: a quién afecta (Vendedores, Almacén, Tiendas, Administración). null o vacía = a todos
        /// (también si la API todavía no lo conoce).
        /// </summary>
        public List<string> Perfiles { get; set; }

        /// <summary>Sin versión = sugerencia de un usuario, todavía sin implementar.</summary>
        public bool EsSugerencia => string.IsNullOrWhiteSpace(Version);
    }

    /// <summary>
    /// Sugerencia 551 (GET api/Novedades/MisPerfiles): con qué perfiles filtra la API las novedades de quien
    /// pregunta. Los deduce la API de los grupos de dominio (Almacén, Tiendas, Administración/Compras/TiendaOnline,
    /// Comerciales = Vendedores); Dirección e Informática las ven todas.
    /// </summary>
    public class PerfilesUsuarioNovedades
    {
        public List<string> Perfiles { get; set; } = new List<string>();
        /// <summary>No se le filtra nada (Dirección, Informática o sin perfil conocido).</summary>
        public bool VeTodas { get; set; }
        /// <summary>Dirección o Informática: puede cambiar a quién afecta cada novedad.</summary>
        public bool PuedeEditar { get; set; }
        /// <summary>Los perfiles que se pueden poner a una novedad.</summary>
        public List<string> Disponibles { get; set; }
    }

    /// <summary>Nesto#519 (NestoAPI#616): un fichero adjunto a una novedad o sugerencia.</summary>
    public class AdjuntoNovedad
    {
        public int Id { get; set; }
        /// <summary>Nombre original del fichero (p. ej. «Normas cupones.pdf»).</summary>
        public string Nombre { get; set; }
        /// <summary>Content-Type (application/pdf, image/png, image/jpeg, image/gif, image/webp).</summary>
        public string Tipo { get; set; }
        /// <summary>Tamaño en bytes.</summary>
        public long Tamano { get; set; }
    }

    /// <summary>
    /// Nesto#519 (NestoAPI#616): los adjuntos de las novedades. La lista viene en el DTO de cada novedad;
    /// aquí, descargar, subir y borrar. Todos lanzan con el mensaje de la API.
    /// </summary>
    public interface IServicioAdjuntosNovedades
    {
        /// <summary>GET api/Novedades/Adjuntos/{id}: el contenido del fichero.</summary>
        Task<byte[]> Descargar(int adjuntoId);
        /// <summary>POST api/Novedades/{id}/Adjuntos (multipart, campo «fichero» por cada uno): los adjuntos creados.</summary>
        Task<List<AdjuntoNovedad>> Subir(int novedadId, IEnumerable<string> rutasFicheros);
        /// <summary>DELETE api/Novedades/Adjuntos/{id}.</summary>
        Task Borrar(int adjuntoId);
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

        /// <summary>
        /// Sugerencia 551: todas las publicadas, también las que no son de los perfiles del usuario
        /// (GET api/Novedades?todas=true). Como <see cref="ObtenerNovedades"/>, nunca lanza.
        /// </summary>
        Task<List<NovedadUsuario>> ObtenerTodasLasNovedades();

        /// <summary>Sugerencia 551: los perfiles con los que la API filtra las novedades. Lanza si la API falla.</summary>
        Task<PerfilesUsuarioNovedades> LeerMisPerfiles();

        /// <summary>
        /// Sugerencia 551 (Dirección / Informática): a quién afecta la novedad. Vacía = a todos. Lanza con el
        /// mensaje de la API.
        /// </summary>
        Task CambiarPerfiles(int novedadId, IEnumerable<string> perfiles);

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
        /// <summary>
        /// NestoAPI#558: «Algo no funciona». Mismo formulario que <see cref="Sugerir"/>, pero la API lo guarda
        /// como incidencia y le añade el contexto (versión, <paramref name="pantalla"/> y errores de ELMAH).
        /// </summary>
        Task<NovedadUsuario> AvisarAlgoNoFunciona(string texto, byte[] imagenPng, string pantalla);
        /// <summary>La captura de una sugerencia.</summary>
        Task<byte[]> LeerImagenNovedad(int novedadId);
        /// <summary>Novedades (con versión) y sugerencias (sin versión) que tienen todas las palabras.</summary>
        Task<List<NovedadUsuario>> Buscar(string texto);

        // Nesto#491 (NestoAPI#537): @menciones. Lanza si la API falla (la ventana se queda sin desplegable).

        /// <summary>Los usuarios de Nesto a los que se puede mencionar con @Nombre.</summary>
        Task<List<Mencionable>> LeerMencionables();
    }
}
