using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#501: abre la ventana de Novedades con todas las publicadas. Un único sitio para el menú de la
    /// cinta («Nesto» → «Novedades», Nesto#372), el botón junto a la campana y el aviso de versión nueva de
    /// la campana (que la abre en esa versión).
    /// </summary>
    public interface IAbridorNovedades
    {
        /// <summary>
        /// Abre Novedades en <paramref name="version"/> o, sin ella (o si esa versión no tiene novedades), en la
        /// más nueva. Nunca lanza: consultar las novedades no debe tirar la aplicación.
        /// </summary>
        Task Abrir(string version = null);
    }

    public class AbridorNovedades : IAbridorNovedades
    {
        internal const string DIALOGO_NOVEDADES = "NovedadesDialog";

        private readonly INovedadesService _novedadesService;
        private readonly IDialogService _dialogService;

        public AbridorNovedades(INovedadesService novedadesService, IDialogService dialogService)
        {
            _novedadesService = novedadesService ?? throw new ArgumentNullException(nameof(novedadesService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public async Task Abrir(string version = null)
        {
            try
            {
                // ObtenerNovedades nunca lanza: si falla, la ventana sale vacía (y con Sugerencias).
                List<NovedadUsuario> novedades = await _novedadesService.ObtenerNovedades() ?? new List<NovedadUsuario>();
                var parametros = new DialogParameters
                {
                    { "novedades", novedades }
                };
                if (!string.IsNullOrWhiteSpace(version))
                {
                    parametros.Add(NovedadesDialogViewModel.PARAMETRO_VERSION, version.Trim());
                }
                _dialogService.ShowDialog(DIALOGO_NOVEDADES, parametros, _ => { });
            }
            catch (Exception)
            {
                // Consultar las novedades nunca debe tirar la aplicación
            }
        }
    }
}
