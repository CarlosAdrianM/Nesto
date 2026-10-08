using Nesto.Infrastructure.Contracts;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#340 (fase 4A.2): la base de los ViewModels ya no hereda de <c>Prism.Mvvm.BindableBase</c>
    /// sino de <c>CommunityToolkit.Mvvm.ComponentModel.ObservableObject</c>. Es el primer paso real
    /// para quitar Prism.
    ///
    /// Los dos exponen el mismo <c>SetProperty(ref campo, valor)</c> con la misma semántica, así que
    /// las 175 llamadas de los ViewModels que heredan de aquí siguen valiendo sin tocar nada. Lo
    /// único que cambia de nombre es <c>RaisePropertyChanged</c> (Prism), que en CommunityToolkit se
    /// llama <c>OnPropertyChanged</c>; para no tener que editar sus 79 usos —y, sobre todo, para no
    /// meter un cambio de 14 pantallas en un solo push— se deja como envoltorio (en <see cref="ViewModelBasico"/>).
    ///
    /// Nesto#490 (4C.4, 6.º tramo): ya no implementa <c>Prism.Regions.INavigationAware</c> (con
    /// <c>IsNavigationTarget = false</c> y <c>OnNavigatedFrom</c> vacío) sino <see cref="IReceptorNavegacionPestanaNueva"/>:
    /// cada navegación a una de sus vistas sigue abriendo una pestaña nueva (lo hace cumplir
    /// <c>CargadorVistasNavegacion</c>) y <see cref="AlLlegar"/> sustituye a <c>OnNavigatedTo</c>. Quien quiera
    /// reutilizar su pestaña hereda de <see cref="ViewModelBasico"/> e implementa <see cref="IReceptorNavegacion"/>.
    /// </summary>
    public class ViewModelBase : ViewModelBasico, IReceptorNavegacionPestanaNueva
    {
        /// <summary>Se ha navegado a la vista (siempre una nueva). Por defecto no hace nada.</summary>
        public virtual void AlLlegar(ParametrosNavegacion parametros)
        {
        }
    }

}
