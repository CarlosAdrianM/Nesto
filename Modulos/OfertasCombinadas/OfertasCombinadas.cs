using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Services;
using Nesto.Modulos.OfertasCombinadas.ViewModels;
using Nesto.Modulos.OfertasCombinadas.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Nesto.Modulos.OfertasCombinadas
{
    public class OfertasCombinadas : IModule, IModuloNesto, IOfertasCombinadas
    {
        // Nesto#490 (4C.5): Prism sigue cargando el módulo por IModule, que delega en IModuloNesto
        void IModule.RegisterTypes(IContainerRegistry containerRegistry) => Registrar(new RegistroPrism(containerRegistry));
        void IModule.OnInitialized(IContainerProvider containerProvider) => Inicializar(new ResolutorPrism(containerProvider));

        public void Registrar(IRegistroNesto registro)
        {
            registro.Vista<OfertasCombinadasView>("OfertasCombinadasView");
            registro.Transitorio<IOfertasCombinadasService, OfertasCombinadasService>();

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<OfertasCombinadasMenuBar>();
            registro.Transitorio<OfertasCombinadasMenuBarViewModel>();
        }

        public void Inicializar(IResolutorNesto resolutor)
        {
            var view = resolutor.Resolver<OfertasCombinadasMenuBar>();
            if (view != null)
            {
                resolutor.Resolver<IServicioCinta>().AnadirMenu(view);
            }
        }
    }

    public interface IOfertasCombinadas
    {
    }
}
