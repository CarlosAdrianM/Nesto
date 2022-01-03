using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.Views;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modulos.PedidoCompra.ViewModels
{
    public class PedidoCompraViewModel : BindableBase, INavigationAware
    {
        private IRegionManager RegionManager { get; }
        public IConfiguracion Configuracion { get; set; }
        private IContainerProvider ContainerProvider { get; }
        

        public PedidoCompraViewModel(IRegionManager regionManager, IConfiguracion configuracion, IContainerProvider containerProvider)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;
            ContainerProvider = containerProvider;

            AbrirModuloCommand = new DelegateCommand(OnAbrirModulo);

            Titulo = "Pedido Compra";
        }

        private IRegionManager _scopedRegionManager;
        public IRegionManager ScopedRegionManager
        {
            get => _scopedRegionManager;
            set => SetProperty(ref _scopedRegionManager, value);
        }
        public string Titulo { get; private set; }

        public ICommand AbrirModuloCommand { get; private set; }
        private void OnAbrirModulo()
        {
            var view = ContainerProvider.Resolve<PedidoCompraView>();
            if (view != null)
            {
                var region = RegionManager.Regions["MainRegion"];
                ScopedRegionManager = region.Add(view, null, true);
                view.ScopedRegionManager = ScopedRegionManager;
                region.Activate(view);
            }
        }


        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return false;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            
        }
    }
}
