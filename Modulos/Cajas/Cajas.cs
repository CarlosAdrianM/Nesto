using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.RibbonRegionAdapter;

namespace Nesto.Modulos.Cajas
{
    public class Cajas : IModule, ICajas
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var view = containerProvider.Resolve<CajasMenuBar>();
            if (view != null)
            {
                var regionAdapter = containerProvider.Resolve<RibbonRegionAdapter>();
                var mainWindow = containerProvider.Resolve<IMainWindow>();
                var region = regionAdapter.Initialize(mainWindow.mainRibbon, "Cajas");

                region.Add(view, "MenuBar");
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, CajasView>("CajasView");
            containerRegistry.Register<object, BancosView>("BancosView");
        }
    }
}
