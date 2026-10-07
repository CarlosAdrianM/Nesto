namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.4): un <see cref="IReceptorNavegacion"/> que abre una vista NUEVA en cada navegación, aunque ya
    /// haya otra igual abierta en la región (una pestaña más en MainRegion, otro detalle en un maestro-detalle).
    /// Equivale a un <c>Prism.Regions.INavigationAware</c> con <c>IsNavigationTarget = false</c> y
    /// <c>OnNavigatedFrom</c> vacío.
    ///
    /// Lo implementa el ViewModel (el DataContext de la vista) o la propia vista. Mientras Nesto siga sobre las
    /// regiones de Prism, lo hace cumplir <c>Nesto.Infrastructure.Navegacion.CargadorVistasNavegacion</c> (el
    /// cargador de vistas de la navegación: nunca reutiliza una vista así) y la navegación la entrega, como a
    /// cualquier receptor, <c>ReceptorNavegacionRegionBehavior</c> a la vista recién abierta (la activa).
    /// </summary>
    public interface IReceptorNavegacionPestanaNueva : IReceptorNavegacion
    {
    }
}
