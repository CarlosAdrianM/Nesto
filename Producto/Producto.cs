using Prism.Modularity;
using Prism.RibbonRegionAdapter;
using Prism.Ioc;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto.Views;

namespace Nesto.Modulos.Producto
{
    public class Producto : IModule, IProducto
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, ProductoView>("ProductoView");
            containerRegistry.Register<object, ReposicionView>("ReposicionView");
            containerRegistry.Register<object, VideosView>("VideosView");
            containerRegistry.Register<object, EtiquetasHuecoView>("EtiquetasHuecoView");
            containerRegistry.Register<object, RecibirReposicionView>("RecibirReposicionView");
            containerRegistry.Register<object, EnviarReposicionView>("EnviarReposicionView");
            containerRegistry.Register<object, CalendarioReposicionesView>("CalendarioReposicionesView");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            var view = containerProvider.Resolve<ProductoMenuBar>();
            if (view != null)
            {
                var regionAdapter = containerProvider.Resolve<RibbonRegionAdapter>();
                var mainWindow = containerProvider.Resolve<IMainWindow>();
                var region = regionAdapter.Initialize(mainWindow.mainRibbon, "Producto");

                region.Add(view, "MenuBar");
            }
        }
    }
}
