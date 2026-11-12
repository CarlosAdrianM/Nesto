using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#372 + NestoAPI#495: qué novedades se enseñan al arrancar Nesto. Conviven dos criterios y se juntan (sin repetir):
    /// <list type="number">
    /// <item><b>Por versión</b> (el de siempre, Nesto#372): si la versión instalada es posterior a <see cref="Parametros.Claves.UltimaVersionNovedades"/>,
    /// las de las versiones posteriores (<c>desdeVersion</c>). Se conserva para que, al actualizar, salgan también las sugerencias
    /// implementadas en esa versión, que conservan su Id antiguo y por Id no saldrían.</item>
    /// <item><b>Por Id</b> (NestoAPI#495): las de ámbitos Nesto y NestoAPI con Id mayor que <see cref="Parametros.Claves.UltimaNovedadVista"/>.
    /// Así una novedad de la API sale sin publicar Nesto. Una de Nesto de una versión que aún no ha llegado a este puesto espera
    /// (y lo que va detrás de ella también, para no avisarlo dos veces).</item>
    /// </list>
    /// La primera vez (sin <see cref="Parametros.Claves.UltimaNovedadVista"/>) no se enseña el histórico: se apunta el Id por el que va y solo
    /// sale lo de la versión, como antes. Si la lectura por Id falla, no se guarda nada (la próxima vez se vuelve a mirar).
    /// Nunca lanza: las novedades no deben impedir arrancar.
    /// </summary>
    public class ComprobadorNovedadesArranque
    {
        private readonly IConfiguracion _configuracion;
        private readonly INovedadesService _novedades;
        private readonly string _versionActual;
        private const string EMPRESA = Constantes.Empresas.EMPRESA_DEFECTO;

        public ComprobadorNovedadesArranque(IConfiguracion configuracion, INovedadesService novedades, string versionActual)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
            _novedades = novedades ?? throw new ArgumentNullException(nameof(novedades));
            _versionActual = versionActual;
        }

        /// <summary>Las novedades que hay que enseñar (vacía si ninguna), de la versión más nueva a la más antigua. Deja guardado lo visto.</summary>
        public async Task<List<NovedadUsuario>> Comprobar()
        {
            var mostrar = new List<NovedadUsuario>();
            try
            {
                string ultimaVersion = await LeerSinFallar(Parametros.Claves.UltimaVersionNovedades).ConfigureAwait(false);
                int? ultimaId = NovedadesHelper.UltimaNovedadVistaDe(await LeerSinFallar(Parametros.Claves.UltimaNovedadVista).ConfigureAwait(false));

                if (NovedadesHelper.DebeMostrarNovedades(_versionActual, ultimaVersion))
                {
                    mostrar.AddRange((await _novedades.ObtenerNovedades(ultimaVersion?.Trim()).ConfigureAwait(false) ?? new List<NovedadUsuario>())
                        .Where(n => n != null));
                }

                List<NovedadUsuario> porId = await _novedades.ObtenerNovedadesDesdeId(ultimaId ?? 0).ConfigureAwait(false);
                if (porId != null)
                {
                    int siguiente = NovedadesHelper.SiguienteUltimaNovedadVista(porId, _versionActual, ultimaId ?? 0);
                    if (ultimaId.HasValue)
                    {
                        HashSet<int> yaEstan = new HashSet<int>(mostrar.Select(n => n.Id));
                        mostrar.AddRange(NovedadesHelper.NovedadesParaAvisarPorId(porId, siguiente).Where(n => yaEstan.Add(n.Id)));
                    }
                    if (!ultimaId.HasValue || siguiente > ultimaId.Value)
                    {
                        await GuardarSinFallar(Parametros.Claves.UltimaNovedadVista, siguiente.ToString()).ConfigureAwait(false);
                    }
                }

                if (!string.IsNullOrWhiteSpace(_versionActual) && ultimaVersion?.Trim() != _versionActual)
                {
                    await GuardarSinFallar(Parametros.Claves.UltimaVersionNovedades, _versionActual).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // Las novedades nunca deben impedir arrancar Nesto
            }
            return Ordenar(mostrar);
        }

        /// <summary>De la versión más nueva a la más antigua (como las da la API); dentro de cada versión, el orden en que llegaron.</summary>
        internal static List<NovedadUsuario> Ordenar(IEnumerable<NovedadUsuario> novedades)
        {
            return novedades
                .OrderByDescending(n => Version.TryParse(n.Version?.Trim(), out Version v) ? v : new Version(0, 0))
                .ToList();
        }

        // leerParametro lanza si el parámetro no existe todavía (ni siquiera para «(defecto)»): se trata como vacío, y el
        // GuardarParametro de después lo crea.
        private async Task<string> LeerSinFallar(string clave)
        {
            try
            {
                return await _configuracion.leerParametro(EMPRESA, clave).ConfigureAwait(false) ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private async Task GuardarSinFallar(string clave, string valor)
        {
            try
            {
                await _configuracion.GuardarParametro(EMPRESA, clave, valor).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Se volverá a intentar en el siguiente arranque
            }
        }
    }
}
