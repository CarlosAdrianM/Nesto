using Prism.Modularity;
using Nesto.Contratos;
using Prism.RibbonRegionAdapter;
using Prism.Ioc;

namespace Nesto.Modulos.Cliente
{
    public class Cliente : IModule, ICliente
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, CrearClienteView>("CrearClienteView");
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