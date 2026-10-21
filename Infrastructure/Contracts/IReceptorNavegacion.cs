namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.4): lo que recibe una navegación, sin tipos de Prism. Lo implementa el ViewModel (el
    /// DataContext de la vista) en lugar de <c>Prism.Regions.INavigationAware</c>.
    ///
    /// Equivale a un INavigationAware con <c>IsNavigationTarget = true</c> (si la vista ya está abierta se
    /// reutiliza la misma pestaña) y <c>OnNavigatedFrom</c> vacío: sin INavigationAware, Prism ya reutiliza
    /// la vista existente, así que solo hace falta avisar al llegar. Lo entrega la región propia
    /// (<c>Nesto.Infrastructure.Navegacion.ServicioNavegacionNesto</c>, con <c>ReceptorNavegacion.Entregar</c>) en todas
    /// las regiones. Los que abren una pestaña nueva en cada navegación (<c>IsNavigationTarget = false</c>) implementan
    /// <see cref="IReceptorNavegacionPestanaNueva"/>.
    /// </summary>
    public interface IReceptorNavegacion
    {
        /// <summary>Se ha navegado a esta vista (nueva o reutilizada), con estos parámetros (vacío si no hay).</summary>
        void AlLlegar(ParametrosNavegacion parametros);
    }
}
