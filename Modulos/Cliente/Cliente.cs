using Prism.Modularity;
using Prism.RibbonRegionAdapter;
using Prism.Ioc;
using Nesto.Modulos.Cliente.ViewModels;
using Nesto.Infrastructure.Contracts;

namespace Nesto.Modulos.Cliente
{
    public class Cliente : IModule, ICliente
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, CrearClienteView>("CrearClienteView");
            containerRegistry.Register<object, Modelo347View>("Modelo347View");
            containerRegistry.RegisterDialog<NotificacionTelefonoView, NotificacionTelefonoViewModel>();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            var view = containerProvider.Resolve<ClienteMenuBar>();
            if (view != null)
            {
                var regionAdapter = containerProvider.Resolve<RibbonRegionAdapter>();
                var mainWindow = containerProvider.Resolve<IMainWindow>();
                var region = regionAdapter.Initialize(mainWindow.mainRibbon, "Cliente");

                region.Add(view, "MenuBar");
            }
        }
    }
}