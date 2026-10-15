using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Services;
using Nesto.Modulos.CanalesExternos.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalesExternos : IModule, IModuloNesto, ICanalesExternos
    {
        // Nesto#490 (4C.5): Prism sigue cargando el módulo por IModule, que delega en IModuloNesto
        void IModule.RegisterTypes(IContainerRegistry containerRegistry) => Registrar(new RegistroPrism(containerRegistry));
        void IModule.OnInitialized(IContainerProvider containerProvider) => Inicializar(new ResolutorPrism(containerProvider));

        public void Registrar(IRegistroNesto registro)
        {
            registro.Vista<CanalesExternosPedidosView>("CanalesExternosPedidosView");
            registro.Vista<CanalesExternosPagosView>("CanalesExternosPagosView");
            registro.Vista<CanalesExternosProductosView>("CanalesExternosProductosView");
            registro.Vista<CanalesExternosFacturasView>("CanalesExternosFacturasView");
            registro.Vista<CanalesExternosCuadreFacturasView>("CanalesExternosCuadreFacturasView");
            registro.Vista<PoisonPillsView>("PoisonPillsView");

            // Registrar servicios
            registro.Transitorio<IPoisonPillsService, PoisonPillsService>();
            registro.Transitorio<IFacturasAmazonService, FacturasAmazonService>(); // Nesto#434
            registro.Transitorio<IClientesPorTelefonoService, ClientesPorTelefonoService>(); // Nesto#340

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<CanalesExternosMenuBar>();
            registro.Transitorio<CanalesExternosMenuBarViewModel>();
            // Los ViewModels de sus vistas
            registro.Transitorio<ViewModels.CanalesExternosPedidosViewModel>();
            registro.Transitorio<ViewModels.CanalesExternosPagosViewModel>();
            registro.Transitorio<ViewModels.CanalesExternosProductosViewModel>();
            registro.Transitorio<ViewModels.CanalesExternosFacturasViewModel>();
            registro.Transitorio<ViewModels.CanalesExternosCuadreFacturasViewModel>();
            registro.Transitorio<ViewModels.PoisonPillsViewModel>();
        }

        public void Inicializar(IResolutorNesto resolutor)
        {
            var view = resolutor.Resolver<CanalesExternosMenuBar>();
            if (view != null)
            {
                resolutor.Resolver<IServicioCinta>().AnadirMenu(view);
            }
        }
    }
}
