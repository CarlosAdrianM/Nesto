using Prism.Modularity;
using Prism.Regions;
using Nesto.Contratos;
using Prism.RibbonRegionAdapter;
using Prism.Ioc;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalesExternos : IModule, ICanalesExternos
    {        
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var view = containerProvider.Resolve<CanalesExternosMenuBar>();
            if (view != null)
            {
                var regionAdapter = containerProvider.Resolve<RibbonRegionAdapter>();
                var mainWindow = containerProvider.Resolve<IMainWindow>();
                var region = regionAdapter.Initialize(mainWindow.mainRibbon, "CanalesExternos");

                region.Add(view, "MenuBar");
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, CanalesExternosPedidosView>("CanalesExternosPedidosView");
            containerRegistry.Register<object, CanalesExternosPagosView>("CanalesExternosPagosView");
        }
    }
}
