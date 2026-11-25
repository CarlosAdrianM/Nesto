using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Views;

namespace Nesto.Modulos.Cajas
{
    public class Cajas : IModuloNesto, ICajas
    {
        /// <summary>El nombre con el que se navega a la ventana; la campana usa el mismo (NotificacionBuzon).</summary>
        public const string FACTURAS_PENDIENTES_VERIFACTU_VIEW = NotificacionBuzon.VISTA_FACTURAS_PENDIENTES_VERIFACTU;

        /// <summary>Nesto#261: consulta de auditoría de enlaces de pago.</summary>
        public const string AUDITORIA_ENLACES_PAGO_VIEW = "AuditoriaEnlacesPagoView";

        /// <summary>Inmovilizados (Carlos 25/11/26): consulta e impresión de la etiqueta de la máquina.</summary>
        public const string INMOVILIZADOS_VIEW = "InmovilizadosView";

        public void Registrar(IRegistroNesto registro)
        {
            registro.Vista<CajasView>("CajasView");
            registro.Vista<BancosView>("BancosView");
            registro.Vista<MayorCuentaView>("MayorCuentaView");
            // NestoAPI#522: facturas pendientes de Verifactu (también se abre desde la campana)
            registro.Vista<FacturasPendientesVerifactuView>(FACTURAS_PENDIENTES_VERIFACTU_VIEW);
            registro.Transitorio<Interfaces.IFacturasVerifactuService, Services.FacturasVerifactuService>();
            // Nesto#261: auditoría de enlaces de pago (solo lectura)
            registro.Vista<AuditoriaEnlacesPagoView>(AUDITORIA_ENLACES_PAGO_VIEW);
            registro.Transitorio<Interfaces.IAuditoriaEnlacesPagoService, Services.AuditoriaEnlacesPagoService>();
            // Inmovilizados (25/11/26): buscar e imprimir la etiqueta «INMOVILIZADO · NO ES STOCK»
            registro.Vista<InmovilizadosView>(INMOVILIZADOS_VIEW);
            registro.Transitorio<Interfaces.IInmovilizadosService, Services.InmovilizadosService>();
            // Nesto#645: cobro en caja de la factura recién creada en la tienda (lo abre el detalle del pedido)
            registro.RegistrarDialogo<CobroCajaDialog, ViewModels.CobroCajaDialogViewModel>(ViewModels.CobroCajaDialogViewModel.NOMBRE);
            registro.Transitorio<ViewModels.CobroCajaDialogViewModel>();

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<CajasMenuBar>();
            registro.Transitorio<CajasMenuBarViewModel>();
            // Los ViewModels de sus vistas
            registro.Transitorio<ViewModels.CajasViewModel>();
            registro.Transitorio<ViewModels.BancosViewModel>();
            registro.Transitorio<ViewModels.MayorCuentaViewModel>();
            registro.Transitorio<ViewModels.FacturasPendientesVerifactuViewModel>();
            registro.Transitorio<ViewModels.AuditoriaEnlacesPagoViewModel>();
            registro.Transitorio<ViewModels.InmovilizadosViewModel>();
        }

        public void Inicializar(IResolutorNesto resolutor)
        {
            var view = resolutor.Resolver<CajasMenuBar>();
            if (view != null)
            {
                resolutor.Resolver<IServicioCinta>().AnadirMenu(view);
            }
        }
    }
}
