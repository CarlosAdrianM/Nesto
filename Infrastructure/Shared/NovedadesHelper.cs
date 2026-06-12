using System;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#372: decide si hay que mostrar el popup de novedades al arrancar, comparando la
    /// versión actual de Nesto (ClickOnce) con la última versión cuyas novedades vio el usuario
    /// (guardada como parámetro de usuario).
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
    }
}
