using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace Nesto.Modulos.Cliente
{
    public class Cliente : IModule, IModuloNesto, ICliente
    {
        // Nesto#490 (4C.5): Prism sigue cargando el módulo por IModule, que delega en IModuloNesto
        void IModule.RegisterTypes(IContainerRegistry containerRegistry) => Registrar(new RegistroPrism(containerRegistry));
        void IModule.OnInitialized(IContainerProvider containerProvider) => Inicializar(new ResolutorPrism(containerProvider));

        public void Registrar(IRegistroNesto registro)
        {
            registro.Vista<CrearClienteView>("CrearClienteView");
            registro.Vista<Modelo347View>("Modelo347View");
            // Nesto#419: ventana de Extracto de Cliente (v1 = consultar pendientes + liquidar)
            registro.Vista<ExtractoClienteView>("ExtractoClienteView");
            registro.Transitorio<IExtractoClienteService, ExtractoClienteService>();
            // Nesto#417: clientes con NIF incorrecto para Verifactu, con corrección rápida
            registro.Vista<ClientesNifIncorrectosView>("ClientesNifIncorrectosView");
            registro.Transitorio<INifIncorrectosService, NifIncorrectosService>();
            // Nesto#442: mantenimiento de códigos postales (país, ruta, vendedores)
            registro.Vista<MantenimientoCodigosPostalesView>("MantenimientoCodigosPostalesView");
            registro.Transitorio<ICodigosPostalesService, CodigosPostalesService>();
            // NestoAPI#591: eventos con señal reembolsable (mantenimiento, lista de señales y diálogo del extracto)
            registro.Vista<MantenimientoEventosView>("MantenimientoEventosView");
            registro.Vista<SenalesEventosView>("SenalesEventosView");
            registro.Transitorio<IEventosService, EventosService>();
            registro.RegistrarDialogo<ElegirEventoDialog, ElegirEventoDialogViewModel>(ElegirEventoDialogViewModel.NOMBRE);
            registro.RegistrarDialogo<NotificacionTelefonoView, NotificacionTelefonoViewModel>();

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<ClienteMenuBar>();
            registro.Transitorio<CrearClienteViewModel>();
        }

        public void Inicializar(IResolutorNesto resolutor)
        {
            var view = resolutor.Resolver<ClienteMenuBar>();
            if (view != null)
            {
                resolutor.Resolver<IServicioCinta>().AnadirMenu(view);
            }
        }
    }
}
