﻿using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.Regions;
using Microsoft.Practices.Unity;
using Nesto.Contratos;
using Prism.RibbonRegionAdapter;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalesExternos : IModule, ICanalesExternos
    {
        private IUnityContainer Container { get; }
        private IRegionManager RegionManager { get; }

        public CanalesExternos(IUnityContainer container, IRegionManager regionManager, CanalesExternosViewModel viewModel)
        {
            this.Container = container;
            this.RegionManager = regionManager;

        }
        public void Initialize()
        {
            Container.RegisterType<object, CanalesExternosView>("CanalesExternosView");

            var view = Container.Resolve<CanalesExternosMenuBar>();
            if (view != null)
            {
                var regionAdapter = Container.Resolve<RibbonRegionAdapter>();
                var mainWindow = Container.Resolve<IMainWindow>();
                var region = regionAdapter.Initialize(mainWindow.mainRibbon, "CanalesExternos");

                region.Add(view, "MenuBar");
            }
        }
    }
}