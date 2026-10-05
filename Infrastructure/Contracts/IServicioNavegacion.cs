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
    /// Cubre navegar a una vista por nombre y, desde el paso 3, saber cuál es la vista activa de una
    /// región y cerrarla. Lo que añade vistas a mano (pestañas nuevas, los maestro-detalle de
    /// PedidoVenta y PedidoCompra con RegionManager con ámbito) y lo que recibe la navegación
    /// (INavigationAware) sigue en Prism.
    /// </summary>
    public interface IServicioNavegacion
    {
        void RequestNavigate(string regionName, string source);
        void RequestNavigate(string regionName, string source, ParametrosNavegacion parameters);

        /// <summary>La vista activa de la región; null si no hay ninguna o la región no existe. Nunca lanza.</summary>
        object VistaActiva(string regionName);

        /// <summary>Cierra la vista activa de la región (la desactiva y la quita), si la hay.</summary>
        void CerrarVistaActiva(string regionName);

        /// <summary>
        /// Quita TODAS las vistas de la región (activas o no), p. ej. antes de navegar al detalle de otro elemento:
        /// en una región de una sola vista activa, las anteriores se quedan desactivadas pero dentro. Si la región
        /// no existe, no hace nada.
        /// </summary>
        void QuitarVistas(string regionName);
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
