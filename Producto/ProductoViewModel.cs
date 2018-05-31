using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Interactivity.InteractionRequest;
using Microsoft.Practices.Prism.Regions;
using Nesto.Contratos;
using Nesto.Modules.Producto;
using System;
using System.Windows.Input;

namespace Nesto.Modulos.Producto
{
    public class ProductoViewModel : ViewModelBase, INavigationAware
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        private IProductoService Servicio { get; }

        private ProductoModel _productoActual;
        private string _referenciaBuscar;

        public ProductoViewModel(IRegionManager regionManager, IConfiguracion configuracion, IProductoService servicio)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;
            Servicio = servicio;

            AbrirModuloCommand = new DelegateCommand(OnAbrirModulo, CanAbrirModulo);

            Titulo = "Producto";
            NotificationRequest = new InteractionRequest<INotification>();
        }

        public async void CargarProducto()
        {
            try
            {
                ProductoActual = await Servicio.LeerProducto(ReferenciaBuscar);
                if ((ReferenciaBuscar == "" || ReferenciaBuscar ==  null) && ProductoActual != null)
                {
                    ReferenciaBuscar = ProductoActual.Producto;
                }
                Titulo = "Producto " + ProductoActual.Producto;
            } catch (Exception ex)
            {
                NotificationRequest.Raise(new Notification { Content = ex.Message, Title = "Error" });
            }            
        }

        #region "Propiedades Prism"
        public InteractionRequest<INotification> NotificationRequest { get; private set; }
        #endregion

        #region "Propiedades Nesto"
        public ProductoModel ProductoActual {
            get { return _productoActual; }
            set { SetProperty(ref _productoActual, value); }
        }

        public string ReferenciaBuscar
        {
            get { return _referenciaBuscar; }
            set {
                SetProperty(ref _referenciaBuscar, value);
                CargarProducto();
            }
        }
        #endregion

        #region "Comandos"
        public ICommand AbrirModuloCommand { get; private set; }
        private bool CanAbrirModulo()
        {
            return true;
        }
        private void OnAbrirModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "ProductoView");
        }
        #endregion

        public new void OnNavigatedTo(NavigationContext navigationContext)
        {
            var parametro = navigationContext.Parameters["numeroProductoParameter"];
            if (parametro != null)
            {
                ReferenciaBuscar = parametro.ToString();
            } else
            {
                ReferenciaBuscar = "";
            }
        }

    }
}
