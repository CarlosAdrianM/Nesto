namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.4): navegación propia de Nesto, para dejar de depender del <c>IRegionManager</c>
    /// de Prism en los ViewModels. Mismo plan que el de los diálogos (4C.2):
    /// 1. Esta interfaz, con una implementación (<c>Nesto.Infrastructure.Navegacion.ServicioNavegacionPrism</c>)
    ///    que se limita a delegar en el IRegionManager: mismas regiones, mismas vistas registradas.
    /// 2. Migrar las llamadas módulo a módulo. Los métodos se llaman IGUAL que en Prism
    ///    (<c>RequestNavigate</c>) para que la migración sea mecánica.
    /// 3. Cambiar la implementación por una sin Prism. Por eso aquí NO aparece ningún tipo de Prism:
    ///    los parámetros son <see cref="ParametrosNavegacion"/>.
    ///
    /// Solo cubre navegar a una vista por nombre. Lo que manipula las regiones a mano (añadir y
    /// activar vistas en los maestro-detalle de PedidoVenta y PedidoCompra, los RegionManager con
    /// ámbito) y lo que recibe la navegación (INavigationAware) se queda en Prism hasta un paso posterior.
    /// </summary>
    public interface IServicioNavegacion
    {
        void RequestNavigate(string regionName, string source);
        void RequestNavigate(string regionName, string source, ParametrosNavegacion parameters);
    }

    /// <summary>
    /// Nesto#490 (4C.4): parámetros de una navegación, sin tipos de Prism. Mismo uso que
    /// <c>NavigationParameters</c>: <c>new ParametrosNavegacion { { "clienteParameter", cliente } }</c>.
    /// Quien recibe la navegación los sigue leyendo de <c>NavigationContext.Parameters</c> con las
    /// mismas claves.
    /// </summary>
    public class ParametrosNavegacion : ParametrosNesto
    {
    }
}
