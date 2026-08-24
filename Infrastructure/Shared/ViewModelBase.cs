using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Regions;
using System.Runtime.CompilerServices;

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
    /// meter un cambio de 14 pantallas en un solo push— se deja abajo como envoltorio.
    ///
    /// Sigue implementando <c>INavigationAware</c> de Prism.Regions: eso no se puede quitar hasta la
    /// fase 4E (regiones). Convivir a medias es aceptable durante la transición, es el principio 2
    /// del roadmap.
    /// </summary>
    public class ViewModelBase : ObservableObject, INavigationAware
    {
        private string _titulo;
        public string Titulo
        {
            get
            {
                return _titulo;
            }
            set
            {
                SetProperty(ref _titulo, value);
            }
        }

        /// <summary>
        /// Compatibilidad con el nombre que usaba Prism. Lo que hace es exactamente lo mismo que
        /// <see cref="ObservableObject.OnPropertyChanged(string)"/>; existe solo para que los
        /// ViewModels que ya estaban escritos no tengan que cambiar. Al migrar cada módulo en
        /// 4A.3/4A.5 conviene ir sustituyendo las llamadas, y cuando no quede ninguna, borrar esto.
        /// </summary>
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        public virtual void OnNavigatedTo(NavigationContext navigationContext)
        {
        }

        public virtual bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return false;
        }
    }

}
