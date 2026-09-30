using Nesto.Infrastructure.Contracts;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
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

        internal const string REGION_PRINCIPAL = "MainRegion";

        private readonly INovedadesService _novedadesService;
        private readonly IServicioDialogos _dialogService;
        private readonly IRegionManager _regionManager;

        public AbridorNovedades(INovedadesService novedadesService, IServicioDialogos dialogService)
            : this(novedadesService, dialogService, null) { }

        /// <summary>El que usa el contenedor. NestoAPI#558: con la región principal se sabe qué pantalla había abierta.</summary>
        public AbridorNovedades(INovedadesService novedadesService, IServicioDialogos dialogService, IRegionManager regionManager)
        {
            _novedadesService = novedadesService ?? throw new ArgumentNullException(nameof(novedadesService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _regionManager = regionManager;
        }

        /// <summary>
        /// NestoAPI#558: el nombre de la vista activa de la región principal (sin el «View» del final), para
        /// el contexto de «Algo no funciona». null si no hay o no se puede saber: nunca lanza.
        /// </summary>
        internal string PantallaActiva()
        {
            try
            {
                if (_regionManager == null || !_regionManager.Regions.ContainsRegionWithName(REGION_PRINCIPAL))
                {
                    return null;
                }
                return NombrePantalla(_regionManager.Regions[REGION_PRINCIPAL].ActiveViews.FirstOrDefault());
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static string NombrePantalla(object vista)
        {
            string nombre = vista?.GetType().Name;
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return null;
            }
            return nombre.Length > 4 && nombre.EndsWith("View", StringComparison.Ordinal)
                ? nombre.Substring(0, nombre.Length - 4)
                : nombre;
        }

        public async Task Abrir(string version = null)
        {
            try
            {
                // ObtenerNovedades nunca lanza: si falla, la ventana sale vacía (y con Sugerencias).
                List<NovedadUsuario> novedades = await _novedadesService.ObtenerNovedades() ?? new List<NovedadUsuario>();
                var parametros = new ParametrosDialogo
                {
                    { "novedades", novedades }
                };
                if (!string.IsNullOrWhiteSpace(version))
                {
                    parametros.Add(NovedadesDialogViewModel.PARAMETRO_VERSION, version.Trim());
                }
                string pantalla = PantallaActiva();
                if (pantalla != null)
                {
                    parametros.Add(NovedadesDialogViewModel.PARAMETRO_PANTALLA, pantalla);
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
