using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#490 (4C.4): lo común de los ViewModels de Nesto sin decir nada de la navegación: el <see cref="Titulo"/> y el
    /// <see cref="RaisePropertyChanged"/> de compatibilidad con Prism. <see cref="ViewModelBase"/> hereda de aquí y abre
    /// una pestaña nueva en cada navegación; quien quiera reutilizar su pestaña (p. ej. la lista de rapports) hereda de
    /// aquí e implementa <see cref="Contracts.IReceptorNavegacion"/>.
    /// </summary>
    public class ViewModelBasico : ObservableObject
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
    }
}
