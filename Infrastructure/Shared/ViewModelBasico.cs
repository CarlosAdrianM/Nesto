using CommunityToolkit.Mvvm.ComponentModel;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#490 (4C.4): lo común de los ViewModels de Nesto sin decir nada de la navegación: el <see cref="Titulo"/> (Nesto#621: fuera el
    /// RaisePropertyChanged de compatibilidad con Prism; se usa el OnPropertyChanged de ObservableObject). <see cref="ViewModelBase"/> hereda de aquí y abre
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
    }
}
