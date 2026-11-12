using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#372: decide si hay que mostrar el popup de novedades al arrancar, comparando la
    /// versión actual de Nesto (ClickOnce) con la última versión cuyas novedades vio el usuario
    /// (guardada como parámetro de usuario).
    /// NestoAPI#495: además, por «última novedad vista» (Id), para que las novedades de la API (ámbito NestoAPI) salgan
    /// sin publicar Nesto. Ver <see cref="ComprobadorNovedadesArranque"/>.
    /// </summary>
    public static class NovedadesHelper
    {
        public static bool DebeMostrarNovedades(string versionActual, string ultimaVersionVista)
        {
            if (!Version.TryParse(versionActual, out Version actual))
            {
                return false;
            }
            // Sin última versión vista (o no parseable) hacemos bootstrap silencioso: no se
            // muestra el histórico completo; el llamante guarda la versión actual y a partir
            // de la siguiente actualización ya se muestran las novedades nuevas.
            if (!Version.TryParse(ultimaVersionVista, out Version vista))
            {
                return false;
            }
            return actual > vista;
        }

        /// <summary>NestoAPI#495: el parámetro UltimaNovedadVista tal como se lee. Vacío o algo que no es un número = nunca ha visto nada (null).</summary>
        public static int? UltimaNovedadVistaDe(string valor)
        {
            string texto = valor?.Trim();
            return int.TryParse(texto, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int id)
                ? id
                : (int?)null;
        }

        /// <summary>
        /// NestoAPI#495: una novedad de Nesto de una versión posterior a la instalada (1.x, la de este programa) todavía no ha
        /// llegado a este puesto: no se avisa hasta que se actualice. Las de NestoAPI, las de versión no legible y las mal
        /// etiquetadas con otra numeración (p. ej. 2.17, como la 753) no esperan a nada.
        /// </summary>
        public static bool AunNoHaLlegado(NovedadUsuario novedad, string versionActual)
        {
            if (novedad == null
                || !string.Equals(novedad.Ambito?.Trim(), AmbitosNovedades.NESTO, StringComparison.OrdinalIgnoreCase)
                || !Version.TryParse(novedad.Version?.Trim(), out Version version)
                || !Version.TryParse(versionActual?.Trim(), out Version actual))
            {
                return false;
            }
            return version.Major == actual.Major && version > actual;
        }

        /// <summary>
        /// NestoAPI#495: hasta qué Id se da por visto: el mayor de las nuevas, salvo que alguna de Nesto aún no haya llegado a
        /// este puesto; entonces se queda justo antes de la primera de esas, para avisarla cuando llegue su versión. Nunca retrocede.
        /// </summary>
        public static int SiguienteUltimaNovedadVista(IEnumerable<NovedadUsuario> nuevas, string versionActual, int actual)
        {
            List<NovedadUsuario> lista = (nuevas ?? Enumerable.Empty<NovedadUsuario>()).Where(n => n != null && n.Id > 0).ToList();
            List<int> pendientes = lista.Where(n => AunNoHaLlegado(n, versionActual)).Select(n => n.Id).ToList();
            int tope = pendientes.Count > 0
                ? pendientes.Min() - 1
                : (lista.Count > 0 ? lista.Max(n => n.Id) : 0);
            return Math.Max(Math.Max(actual, 0), tope);
        }

        /// <summary>
        /// NestoAPI#495: de las nuevas, las que se avisan: con versión (una sugerencia sin implementar no es una novedad) y hasta
        /// <paramref name="hastaId"/> (la siguiente última vista). Lo que hay detrás de una novedad de Nesto que aún no ha llegado
        /// espera a la actualización, para no avisarlo dos veces.
        /// </summary>
        public static List<NovedadUsuario> NovedadesParaAvisarPorId(IEnumerable<NovedadUsuario> nuevas, int hastaId)
        {
            return (nuevas ?? Enumerable.Empty<NovedadUsuario>())
                .Where(n => n != null && n.Id > 0 && n.Id <= hastaId && !string.IsNullOrWhiteSpace(n.Version))
                .ToList();
        }
    }
}
