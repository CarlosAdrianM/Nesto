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

        /// <summary>
        /// Nesto#490 (panel lateral, tramo 1): cuando ya han puesto su menú todos los módulos (al final de
        /// <c>ArranqueNesto.InicializarModulos</c>), saca de la cinta el catálogo del menú lateral, repartido por rol y con
        /// los mismos comandos y ViewModels que la cinta. Lo deja en <see cref="Catalogo"/> y lo devuelve.
        /// </summary>
        CatalogoMenu Completar();

        /// <summary>El catálogo del último <see cref="Completar"/> (vacío hasta entonces).</summary>
        CatalogoMenu Catalogo { get; }
    }
}
