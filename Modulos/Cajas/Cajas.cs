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

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<CajasMenuBar>();
            registro.Transitorio<CajasMenuBarViewModel>();
            // Los ViewModels de sus vistas
            registro.Transitorio<ViewModels.CajasViewModel>();
            registro.Transitorio<ViewModels.BancosViewModel>();
            registro.Transitorio<ViewModels.MayorCuentaViewModel>();
            registro.Transitorio<ViewModels.FacturasPendientesVerifactuViewModel>();
            registro.Transitorio<ViewModels.AuditoriaEnlacesPagoViewModel>();
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
