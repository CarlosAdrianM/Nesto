using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.Unity;
using Nesto.Contratos;
using Prism.RibbonRegionAdapter;

namespace Nesto.Modulos.Cliente
{
    public class Cliente : IModule, ICliente
    {
        private IUnityContainer Container { get; }
        private IRegionManager RegionManager { get; }
        public Cliente(IUnityContainer container, IRegionManager regionManager, CrearClienteViewModel viewModel)
        {
            this.Container = container;
            this.RegionManager = regionManager;

        }
        public void Initialize()
        {
            Container.RegisterType<object, CrearClienteView>("CrearClienteView");

            var view = Container.Resolve<ClienteMenuBar>();
            if (view != null)
            {
                var regionAdapter = Container.Resolve<RibbonRegionAdapter>();
                var mainWindow = Container.Resolve<IMainWindow>();
                var region = regionAdapter.Initialize(mainWindow.mainRibbon, "Cliente");

                region.Add(view, "MenuBar");
            }
        }
    }
}
