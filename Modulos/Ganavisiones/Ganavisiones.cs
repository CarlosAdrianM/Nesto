using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Ganavisiones.Interfaces;
using Nesto.Modulos.Ganavisiones.Services;
using Nesto.Modulos.Ganavisiones.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Nesto.Modulos.Ganavisiones
{
    public class Ganavisiones : IModule, IGanavisiones
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var view = containerProvider.Resolve<GanavisionesMenuBar>();
            if (view != null)
            {
                containerProvider.Resolve<IServicioCinta>().AnadirMenu(view);
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, GanavisionesView>("GanavisionesView");
            containerRegistry.Register<IGanavisionesService, GanavisionesService>();
        }
    }

    public interface IGanavisiones
    {
    }
}
