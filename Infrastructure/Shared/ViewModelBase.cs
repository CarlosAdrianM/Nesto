using Prism.Mvvm;
using Prism.Regions;

namespace Nesto.Infrastructure.Shared
{
    public class ViewModelBase : BindableBase, INavigationAware
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

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
        }

        public virtual bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return false;
        }
    }

}
