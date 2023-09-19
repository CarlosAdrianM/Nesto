using ControlesUsuario.Dialogs;
using Nesto.Modules.Producto.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    public class ReposicionViewModel : BindableBase, INavigationAware
    {
        

        public ReposicionViewModel(IProductoService servicio, IDialogService dialogService) 
        {
            Servicio = servicio;
            DialogService = dialogService;
            TraspasarDiarioCommand = new DelegateCommand(OnTraspasarDiario, CanTraspasarDiario);
            CargarDiarios();            
        }

        
        public string Titulo { get; set; } = "Reposición";
        
        private string _almacenOrigen;
        public string AlmacenOrigen 
        { 
            get => _almacenOrigen;
            set
            {
                SetProperty(ref _almacenOrigen, value);
            }
        }
        private DiarioProductoModel _diarioDestino;
        public DiarioProductoModel DiarioDestino { 
            get => _diarioDestino;
            set {
                SetProperty(ref _diarioDestino, value);
                TraspasarDiarioCommand.RaiseCanExecuteChanged();
            }
        }
        private DiarioProductoModel _diarioOrigen;       

        public DiarioProductoModel DiarioOrigen { 
            get => _diarioOrigen;
            set
            {
                SetProperty(ref _diarioOrigen, value);
                TraspasarDiarioCommand.RaiseCanExecuteChanged();
                if (value != null && value.Almacenes != null && string.IsNullOrEmpty(AlmacenOrigen))
                {
                    AlmacenOrigen = value.Almacenes.FirstOrDefault();
                }
            }
        }

        public DelegateCommand TraspasarDiarioCommand { get; private set; }
        public IProductoService Servicio { get; }
        public IDialogService DialogService { get; }
        public List<DiarioProductoModel> ListaDiarios { get; private set; }
        public List<DiarioProductoModel> ListaDiariosConMovimientos => ListaDiarios?.Where(d => !d.EstaVacio)?.ToList() ?? new List<DiarioProductoModel>();
        public List<DiarioProductoModel> ListaDiariosSinMovimientos => ListaDiarios?.Where(d => d.EstaVacio)?.ToList() ?? new List<DiarioProductoModel>();        

        private bool CanTraspasarDiario()
        {
            return DiarioOrigen != null && DiarioDestino != null;
        }
        private async void OnTraspasarDiario()
        {
            bool resultado;
            try
            {
                resultado = await Servicio.TraspasarDiario(DiarioOrigen.Id, DiarioDestino.Id, AlmacenOrigen).ConfigureAwait(true);
                if (!resultado)
                {
                    DialogService.ShowError("No se ha podido traspasar el diario");
                }
            } 
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
            finally
            {
                await CargarDiarios().ConfigureAwait(true);
            }
            
        }


        private async Task CargarDiarios()
        {
            ListaDiarios = await Servicio.LeerDiariosProducto();
            RaisePropertyChanged(nameof(ListaDiariosConMovimientos));
            RaisePropertyChanged(nameof(ListaDiariosSinMovimientos));
        }



        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true; //false para poder abrir varias instancias
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            
        }
    }
}
