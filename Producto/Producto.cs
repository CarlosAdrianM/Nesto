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
            // Lo que suena al leer en Enviar y Recibir reposición
            registro.Singleton<ISonidosLector, SonidosLectorSistema>();

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<ProductoMenuBar>();
            registro.Transitorio<ProductoMenuBarViewModel>();
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
