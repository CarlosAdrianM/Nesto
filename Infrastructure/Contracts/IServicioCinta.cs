using System.Windows;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.4, la cinta): por aquí cada módulo pone su menú en la cinta principal de Nesto, sin tipos de Prism.
    /// Sustituye a <c>RibbonRegionAdapter.Initialize(mainWindow.mainRibbon, nombre)</c> + <c>region.Add(menu, "MenuBar")</c>.
    /// </summary>
    public interface IServicioCinta
    {
        /// <summary>
        /// Fusiona el menú del módulo (un UserControl cuyo contenido es una <c>Ribbon</c>) con la cinta principal: sus
        /// pestañas pasan a ella, las del mismo título se juntan y cada grupo conserva el DataContext del módulo.
        /// </summary>
        void AnadirMenu(UIElement menu);
    }
}
