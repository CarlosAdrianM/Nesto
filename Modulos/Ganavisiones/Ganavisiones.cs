using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Ganavisiones.Interfaces;
using Nesto.Modulos.Ganavisiones.Services;
using Nesto.Modulos.Ganavisiones.ViewModels;
using Nesto.Modulos.Ganavisiones.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Nesto.Modulos.Ganavisiones
{
    public class Ganavisiones : IModule, IModuloNesto, IGanavisiones
    {
        // Nesto#490 (4C.5): Prism sigue cargando el módulo por IModule, que delega en IModuloNesto
        void IModule.RegisterTypes(IContainerRegistry containerRegistry) => Registrar(new RegistroPrism(containerRegistry));
        void IModule.OnInitialized(IContainerProvider containerProvider) => Inicializar(new ResolutorPrism(containerProvider));

        public void Registrar(IRegistroNesto registro)
        {
            registro.Vista<GanavisionesView>("GanavisionesView");
            registro.Transitorio<IGanavisionesService, GanavisionesService>();

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<GanavisionesMenuBar>();
            registro.Transitorio<GanavisionesMenuBarViewModel>();
            // Los ViewModels de sus vistas
            registro.Transitorio<GanavisionesViewModel>();
        }

        public void Inicializar(IResolutorNesto resolutor)
        {
            var view = resolutor.Resolver<GanavisionesMenuBar>();
            if (view != null)
            {
                resolutor.Resolver<IServicioCinta>().AnadirMenu(view);
            }
        }
    }

    public interface IGanavisiones
    {
    }
}
