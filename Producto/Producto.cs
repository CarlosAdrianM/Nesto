using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Nesto.Modulos.Producto
{
    public class Producto : IModule, IModuloNesto, IProducto
    {
        // Nesto#490 (4C.5): Prism sigue cargando el módulo por IModule, que delega en IModuloNesto
        void IModule.RegisterTypes(IContainerRegistry containerRegistry) => Registrar(new RegistroPrism(containerRegistry));
        void IModule.OnInitialized(IContainerProvider containerProvider) => Inicializar(new ResolutorPrism(containerProvider));

        public void Registrar(IRegistroNesto registro)
        {
            registro.Vista<ProductoView>("ProductoView");
            registro.Vista<ReposicionView>("ReposicionView");
            registro.Vista<VideosView>("VideosView");
            registro.Vista<EtiquetasHuecoView>("EtiquetasHuecoView");
            registro.Vista<RecibirReposicionView>("RecibirReposicionView");
            registro.Vista<EnviarReposicionView>("EnviarReposicionView");
            registro.Vista<CalendarioReposicionesView>("CalendarioReposicionesView");
            // Nesto#526: lo recibido que no coincide con lo enviado (también la abre el aviso de la campana)
            registro.Vista<DiferenciasReposicionView>("DiferenciasReposicionView");
            // Lo que suena al leer en Enviar y Recibir reposición
            registro.Singleton<ISonidosLector, SonidosLectorSistema>();

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<ProductoMenuBar>();
            registro.Transitorio<ProductoMenuBarViewModel>();
            // Los ViewModels de sus vistas
            registro.Transitorio<Nesto.Modules.Producto.ViewModels.ProductoViewModel>();
            registro.Transitorio<Nesto.Modules.Producto.ViewModels.ReposicionViewModel>();
            registro.Transitorio<Nesto.Modules.Producto.ViewModels.VideosViewModel>();
            registro.Transitorio<Nesto.Modules.Producto.ViewModels.EtiquetasHuecoViewModel>();
            registro.Transitorio<Nesto.Modules.Producto.ViewModels.RecibirReposicionViewModel>();
            registro.Transitorio<Nesto.Modules.Producto.ViewModels.EnviarReposicionViewModel>();
            registro.Transitorio<Nesto.Modules.Producto.ViewModels.CalendarioReposicionesViewModel>();
            registro.Transitorio<Nesto.Modules.Producto.ViewModels.DiferenciasReposicionViewModel>();

            // Nesto#529 (NestoAPI#631): propuestas de stock (Compras decide; la campana con tipo PropuestasStock abre aquí)
            registro.Vista<PropuestasStockView>(NotificacionBuzon.VISTA_PROPUESTAS_STOCK);
            registro.Transitorio<Nesto.Modules.Producto.ViewModels.PropuestasStockViewModel>();
            registro.Singleton<IServicioPropuestasStock, ServicioPropuestasStock>();
        }

        public void Inicializar(IResolutorNesto resolutor)
        {
            var view = resolutor.Resolver<ProductoMenuBar>();
            if (view != null)
            {
                resolutor.Resolver<IServicioCinta>().AnadirMenu(view);
            }
        }
    }
}
