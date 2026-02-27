using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Services;
using Nesto.Modulos.OfertasCombinadas.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.RibbonRegionAdapter;

namespace Nesto.Modulos.OfertasCombinadas
{
    public class OfertasCombinadas : IModule, IOfertasCombinadas
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var view = containerProvider.Resolve<OfertasCombinadasMenuBar>();
            if (view != null)
            {
                var regionAdapter = containerProvider.Resolve<RibbonRegionAdapter>();
                var mainWindow = containerProvider.Resolve<IMainWindow>();
                var region = regionAdapter.Initialize(mainWindow.mainRibbon, "OfertasCombinadas");

                region.Add(view, "MenuBar");
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, OfertasCombinadasView>("OfertasCombinadasView");
            containerRegistry.Register<IOfertasCombinadasService, OfertasCombinadasService>();
        }
    }

    public interface IOfertasCombinadas
    {
    }
}
