using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.ViewModels;

namespace Nesto.Modulos.Cliente
{
    public class Cliente : IModuloNesto, ICliente
    {
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
            // Carlos 29/10/26: copiar el enlace de pago de las inscripciones (inyectable para las pruebas)
            registro.Transitorio<ControlesUsuario.Services.IServicioPortapapeles, ControlesUsuario.Services.ServicioPortapapeles>();
            registro.RegistrarDialogo<ElegirEventoDialog, ElegirEventoDialogViewModel>(ElegirEventoDialogViewModel.NOMBRE);
            // Nesto#517: inscribir en un evento (desde el mantenimiento y desde la ficha del cliente)
            registro.RegistrarDialogo<InscribirEventoDialog, InscribirEventoDialogViewModel>(InscribirEventoDialogViewModel.NOMBRE);
            // Nesto#545: «Anunciar otra vez» con el reclamo del aviso
            registro.RegistrarDialogo<AnunciarEventoDialog, AnunciarEventoDialogViewModel>(AnunciarEventoDialogViewModel.NOMBRE);
            registro.RegistrarDialogo<NotificacionTelefonoView, NotificacionTelefonoViewModel>();
            // Nesto#546 (NestoAPI#693): pedidos con la fecha prometida en riesgo (la abre la cinta y el aviso de la campana)
            registro.Vista<PedidosPromesaRiesgoView>(NotificacionBuzon.VISTA_PEDIDOS_PROMESA_RIESGO);
            registro.Transitorio<PedidosPromesaRiesgoViewModel>();

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<ClienteMenuBar>();
            registro.Transitorio<CrearClienteViewModel>();
            // Los ViewModels de sus vistas
            registro.Transitorio<Modelo347ViewModel>();
            registro.Transitorio<ExtractoClienteViewModel>();
            registro.Transitorio<ClientesNifIncorrectosViewModel>();
            registro.Transitorio<MantenimientoCodigosPostalesViewModel>();
            registro.Transitorio<MantenimientoEventosViewModel>();
            registro.Transitorio<SenalesEventosViewModel>();
            registro.Transitorio<InscripcionesClienteViewModel>(); // Nesto#517: pestaña Eventos de la ficha
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
