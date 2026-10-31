using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#592 (NestoAPI#774): un globo («consejo») tal como lo manda GET api/Globos: ya filtrado para el usuario (activo, en
    /// fechas, de su grupo, desde su versión, no visto) y ordenado. Nesto solo decide DÓNDE pintarlo.
    /// </summary>
    public class GloboNesto
    {
        public int Id { get; set; }
        public string Pantalla { get; set; }
        /// <summary>Clave completa del control («EnviarReposicion.EstadoRecogida»). Null = tarjeta centrada.</summary>
        public string Ancla { get; set; }
        public string Titulo { get; set; }
        public string Texto { get; set; }
        public short Orden { get; set; }
        public int? NovedadId { get; set; }
    }

    /// <summary>Un globo que toca enseñar ahora, y si va junto a su control o como tarjeta centrada.</summary>
    public sealed class GloboAEnsenar
    {
        public GloboAEnsenar(GloboNesto globo, bool centrado)
        {
            Globo = globo ?? throw new ArgumentNullException(nameof(globo));
            Centrado = centrado;
        }

        public GloboNesto Globo { get; }
        /// <summary>Sin ancla, o el control no existe o no está a la vista.</summary>
        public bool Centrado { get; }
    }

    /// <summary>NestoAPI#774: GET api/Globos?aplicacion=Nesto&amp;pantalla=…&amp;version=… y POST api/Globos/{id}/Visto.</summary>
    public interface IClienteGlobos
    {
        /// <summary>Lanza si falla (el servicio lo trata como «ninguno»).</summary>
        Task<List<GloboNesto>> LeerPendientesAsync(string pantalla);

        Task MarcarVistoAsync(int id);
    }

    /// <summary>
    /// Nesto#592: qué globos se enseñan en cada pantalla. Sin vista (se prueba sin WPF); lo pinta <c>ControlesUsuario.Globos</c>.
    /// <list type="bullet">
    /// <item>Una sola llamada por pantalla en la sesión (caché en memoria); sin sondeo. Si falla, ninguno, y se vuelve a pedir la
    ///   próxima vez que se entre en la pantalla.</item>
    /// <item>Como mucho <see cref="MaximoSeguidos"/> seguidos, en el orden de la API; los demás, la próxima vez que se entre.</item>
    /// <item>Una tanda a la vez en toda la aplicación (si ya hay globos en pantalla, ninguno más).</item>
    /// <item>Sin ancla, o con el ancla ausente o no visible: tarjeta centrada.</item>
    /// <item>«Entendido» o «Siguiente» → visto en el servidor (por usuario). Esc → no se marca y no vuelve en esta sesión.</item>
    /// </list>
    /// </summary>
    public interface IServicioGlobos
    {
        int MaximoSeguidos { get; }

        /// <summary>Los que tocan ahora (como mucho <see cref="MaximoSeguidos"/>). Quedan reservados hasta Entendido, Aplazar o Soltar.</summary>
        Task<IReadOnlyList<GloboAEnsenar>> QueEnsenarAsync(string pantalla, Func<string, bool> anclaVisible);

        /// <summary>Lo ha leído: se marca visto en el servidor y no vuelve.</summary>
        Task EntendidoAsync(GloboNesto globo);

        /// <summary>Esc: no se marca visto; vuelve en la próxima sesión.</summary>
        void Aplazar(GloboNesto globo);

        /// <summary>No llegó a verse (p. ej. se cerró la pantalla antes): vuelve a estar pendiente en esta sesión.</summary>
        void Soltar(GloboNesto globo);
    }
}
