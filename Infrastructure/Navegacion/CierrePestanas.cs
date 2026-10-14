using Nesto.Infrastructure.Contracts;
using System.Windows;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4, shell): lo que hace la X de una pestaña del shell (<c>CloseTabAction</c>), sin tipos de Prism.
    /// Antes la acción buscaba la región de Prism del TabControl (<c>RegionManager.GetObservableRegion</c>) y quitaba la
    /// vista; ahora lo hace a través de <see cref="IServicioNavegacion"/>, igual que el resto de Nesto.
    /// </summary>
    public static class CierrePestanas
    {
        /// <summary>La región de las pestañas del shell (el TabControl de MainWindow).</summary>
        public const string REGION_PRINCIPAL = "MainRegion";

        /// <summary>
        /// Si el ViewModel de la vista (su DataContext) es un <see cref="ITabCloseConfirmation"/>, le pregunta; si dice
        /// que no, no la cierra. Si no, la quita de la región. Devuelve si la ha intentado quitar.
        /// </summary>
        public static bool Cerrar(IServicioNavegacion navegacion, string region, object vista)
        {
            if ((vista as FrameworkElement)?.DataContext is ITabCloseConfirmation confirmacion && !confirmacion.ConfirmTabClose())
            {
                return false;
            }
            navegacion.CerrarVista(region, vista);
            return true;
        }
    }
}
