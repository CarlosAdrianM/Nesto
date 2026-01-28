using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Nesto.Modules.Producto.ViewModels
{
    public class VideosViewModel : BindableBase, INavigationAware
    {
        public event Action<VideoModel> VideoCompletoSeleccionadoCambiado;

        private readonly IProductoService _servicio;
        private readonly IDialogService _dialogService;
        private readonly IConfiguracion _configuracion;
        private readonly IRegionManager _regionManager;

        private const int VIDEOS_POR_PAGINA = 20;

        public VideosViewModel(IProductoService servicio, IDialogService dialogService, IConfiguracion configuracion, IRegionManager regionManager)
        {
            _servicio = servicio;
            _dialogService = dialogService;
            _configuracion = configuracion;
            _regionManager = regionManager;

            CargarMasVideosCommand = new DelegateCommand(OnCargarMasVideos, CanCargarMasVideos);
            BuscarCommand = new DelegateCommand(OnBuscar, CanBuscar);
            CorrigeVideoProductoCommand = new DelegateCommand(OnCorrigeVideoProducto, CanCorrigeVideoProducto);
            AbrirVideoEnNavegadorCommand = new DelegateCommand(OnAbrirVideoEnNavegador, CanAbrirVideoEnNavegador);
            AbrirProductoCommand = new DelegateCommand<string>(OnAbrirProducto);

            Videos = [];
            Titulo = "Videos";
        }

        #region Propiedades

        public string Titulo { get; private set; }

        private ObservableCollection<VideoLookupModel> _videos;
        public ObservableCollection<VideoLookupModel> Videos
        {
            get => _videos;
            set => SetProperty(ref _videos, value);
        }

        private VideoLookupModel _videoSeleccionado;
        public VideoLookupModel VideoSeleccionado
        {
            get => _videoSeleccionado;
            set
            {
                if (SetProperty(ref _videoSeleccionado, value))
                {
                    if (value != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            VideoModel videoCompleto = await _servicio.CargarVideoCompleto(value.Id);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                VideoCompletoSeleccionado = videoCompleto;
                            });
                        });
                    }
                    else
                    {
                        VideoCompletoSeleccionado = null;
                    }
                    AbrirVideoEnNavegadorCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private VideoModel _videoCompletoSeleccionado;
        public VideoModel VideoCompletoSeleccionado
        {
            get => _videoCompletoSeleccionado;
            set
            {
                if (SetProperty(ref _videoCompletoSeleccionado, value))
                {
                    VideoCompletoSeleccionadoCambiado?.Invoke(value);
                    OtrosProductosEnEsteVideo = new ObservableCollection<ProductoVideoModel>(
                        _videoCompletoSeleccionado?.Productos?.ToList() ?? []
                    );
                    RaisePropertyChanged(nameof(HayVideosProductosSinReferencia));
                    CorrigeVideoProductoCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<ProductoVideoModel> _otrosProductosEnEsteVideo;
        public ObservableCollection<ProductoVideoModel> OtrosProductosEnEsteVideo
        {
            get => _otrosProductosEnEsteVideo;
            set
            {
                if (SetProperty(ref _otrosProductosEnEsteVideo, value))
                {
                    RaisePropertyChanged(nameof(HayVideosProductosSinReferencia));
                }
            }
        }

        public bool HayVideosProductosSinReferencia => OtrosProductosEnEsteVideo is not null &&
                                                       OtrosProductosEnEsteVideo.Any(p => !string.IsNullOrWhiteSpace(p.NombreProducto) &&
                                                            string.IsNullOrWhiteSpace(p.Referencia));

        private string _textoBusqueda;
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                if (SetProperty(ref _textoBusqueda, value))
                {
                    BuscarCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _estaCargando;
        public bool EstaCargando
        {
            get => _estaCargando;
            set
            {
                if (SetProperty(ref _estaCargando, value))
                {
                    CargarMasVideosCommand.RaiseCanExecuteChanged();
                    BuscarCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _hayMasVideos = true;
        public bool HayMasVideos
        {
            get => _hayMasVideos;
            set
            {
                if (SetProperty(ref _hayMasVideos, value))
                {
                    CargarMasVideosCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _esBusqueda;
        public bool EsBusqueda
        {
            get => _esBusqueda;
            set => SetProperty(ref _esBusqueda, value);
        }

        #endregion

        #region Comandos

        public DelegateCommand CargarMasVideosCommand { get; }

        private bool CanCargarMasVideos()
        {
            return !EstaCargando && HayMasVideos;
        }

        private async void OnCargarMasVideos()
        {
            await CargarVideosAsync(false);
        }

        public DelegateCommand BuscarCommand { get; }

        private bool CanBuscar()
        {
            return !EstaCargando;
        }

        private async void OnBuscar()
        {
            EsBusqueda = !string.IsNullOrWhiteSpace(TextoBusqueda);
            await CargarVideosAsync(true);
        }

        public DelegateCommand CorrigeVideoProductoCommand { get; }

        private bool CanCorrigeVideoProducto()
        {
            return VideoCompletoSeleccionado != null;
        }

        private async void OnCorrigeVideoProducto()
        {
            if (VideoCompletoSeleccionado == null)
            {
                return;
            }

            var dialogParameters = new DialogParameters
            {
                { "producto", VideoCompletoSeleccionado }
            };

            var result = await _dialogService.ShowDialogAsync("CorreccionVideoProductoView", dialogParameters);

            if (result.Result == ButtonResult.OK)
            {
                VideoCompletoSeleccionado = await _servicio.CargarVideoCompleto(VideoCompletoSeleccionado.Id);
            }
        }

        public DelegateCommand AbrirVideoEnNavegadorCommand { get; }

        private bool CanAbrirVideoEnNavegador()
        {
            return VideoSeleccionado != null && !string.IsNullOrEmpty(VideoSeleccionado.UrlVideo);
        }

        private void OnAbrirVideoEnNavegador()
        {
            if (VideoSeleccionado != null && !string.IsNullOrEmpty(VideoSeleccionado.UrlVideo))
            {
                Process.Start(new ProcessStartInfo(VideoSeleccionado.UrlVideo) { UseShellExecute = true });
            }
        }

        public DelegateCommand<string> AbrirProductoCommand { get; }

        private void OnAbrirProducto(string productoId)
        {
            if (!string.IsNullOrEmpty(productoId))
            {
                var parameters = new NavigationParameters
                {
                    { "numeroProductoParameter", productoId }
                };
                _regionManager.RequestNavigate("MainRegion", "ProductoView", parameters);
            }
        }

        #endregion

        #region Metodos

        public async Task CargarVideosIniciales()
        {
            await CargarVideosAsync(true);
        }

        private async Task CargarVideosAsync(bool limpiar)
        {
            EstaCargando = true;

            try
            {
                int skip = limpiar ? 0 : Videos.Count;
                List<VideoLookupModel> nuevosVideos;

                if (EsBusqueda && !string.IsNullOrWhiteSpace(TextoBusqueda))
                {
                    nuevosVideos = await _servicio.BuscarVideos(TextoBusqueda, skip, VIDEOS_POR_PAGINA);
                }
                else
                {
                    nuevosVideos = await _servicio.CargarVideos(skip, VIDEOS_POR_PAGINA);
                }

                if (limpiar)
                {
                    Videos.Clear();
                }

                foreach (var video in nuevosVideos)
                {
                    Videos.Add(video);
                }

                HayMasVideos = nuevosVideos.Count == VIDEOS_POR_PAGINA;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error al cargar videos: {ex.Message}");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        #endregion

        #region INavigationAware

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _ = CargarVideosIniciales();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return false;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        #endregion
    }
}
