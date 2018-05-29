using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.Unity;
using Nesto.Contratos;
using Prism.RibbonRegionAdapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Producto
{
    public class Producto : IModule, IProducto
    {
        private IUnityContainer Container { get; }
        private IRegionManager RegionManager { get; }

        public Producto(IUnityContainer container, IRegionManager regionManager, ProductoViewModel viewModel)
        {
            this.Container = container;
            this.RegionManager = regionManager;

        }
        public void Initialize()
        {
            Container.RegisterType<object, ProductoView>("ProductoView");

            var view = Container.Resolve<ProductoMenuBar>();
            if (view != null)
            {
                var regionAdapter = Container.Resolve<RibbonRegionAdapter>();
                var mainWindow = Container.Resolve<IMainWindow>();
                var region = regionAdapter.Initialize(mainWindow.mainRibbon, "Producto");

                region.Add(view, "MenuBar");
            }
        }
    }
}
