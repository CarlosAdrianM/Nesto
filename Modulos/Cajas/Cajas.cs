using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Nesto.Modulos.Cajas
{
    public class Cajas : IModule, ICajas
    {
        /// <summary>El nombre con el que se navega a la ventana; la campana usa el mismo (NotificacionBuzon).</summary>
        public const string FACTURAS_PENDIENTES_VERIFACTU_VIEW = NotificacionBuzon.VISTA_FACTURAS_PENDIENTES_VERIFACTU;

        /// <summary>Nesto#261: consulta de auditoría de enlaces de pago.</summary>
        public const string AUDITORIA_ENLACES_PAGO_VIEW = "AuditoriaEnlacesPagoView";

        public void OnInitialized(IContainerProvider containerProvider)
        {
            var view = containerProvider.Resolve<CajasMenuBar>();
            if (view != null)
            {
                containerProvider.Resolve<IServicioCinta>().AnadirMenu(view);
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, CajasView>("CajasView");
            containerRegistry.Register<object, BancosView>("BancosView");
            containerRegistry.Register<object, MayorCuentaView>("MayorCuentaView");
            // NestoAPI#522: facturas pendientes de Verifactu (también se abre desde la campana)
            containerRegistry.Register<object, FacturasPendientesVerifactuView>(FACTURAS_PENDIENTES_VERIFACTU_VIEW);
            containerRegistry.Register<Interfaces.IFacturasVerifactuService, Services.FacturasVerifactuService>();
            // Nesto#261: auditoría de enlaces de pago (solo lectura)
            containerRegistry.Register<object, AuditoriaEnlacesPagoView>(AUDITORIA_ENLACES_PAGO_VIEW);
            containerRegistry.Register<Interfaces.IAuditoriaEnlacesPagoService, Services.AuditoriaEnlacesPagoService>();
        }
    }
}
