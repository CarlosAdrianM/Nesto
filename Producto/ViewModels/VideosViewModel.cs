using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.Models;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Nesto.Modules.Producto.ViewModels
{
    public class VideosViewModel : ObservableObject, INavigationAware
    {
        public event Action<VideoModel> VideoCompletoSeleccionadoCambiado;

        private readonly IProductoService _servicio;
        private readonly IServicioDialogos _dialogService;
        private readonly IConfiguracion _configuracion;
        private readonly IServicioNavegacion _navegacion;

        private const int VIDEOS_POR_PAGINA = 20;

        public VideosViewModel(IProductoService servicio, IServicioDialogos dialogService, IConfiguracion configuracion, IServicioNavegacion navegacion)
        {
            _servicio = servicio;
            _dialogService = dialogService;
            _configuracion = configuracion;
            _navegacion = navegacion;

            CargarMasVideosCommand = new RelayCommand(OnCargarMasVideos, CanCargarMasVideos);
            BuscarCommand = new RelayCommand(OnBuscar, CanBuscar);
            CorrigeVideoProductoCommand = new RelayCommand(OnCorrigeVideoProducto, CanCorrigeVideoProducto);
            AbrirVideoEnNavegadorCommand = new RelayCommand(OnAbrirVideoEnNavegador, CanAbrirVideoEnNavegador);
            AbrirProductoCommand = new RelayCommand<string>(OnAbrirProducto);
            BorrarVideoCommand = new AsyncRelayCommand(OnBorrarVideo, CanBorrarVideo);
            DarDeBajaVideoCommand = new AsyncRelayCommand(OnDarDeBajaVideo, CanBorrarVideo);

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
                    AbrirVideoEnNavegadorCommand.NotifyCanExecuteChanged();
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
                    OnPropertyChanged(nameof(HayVideosProductosSinReferencia));
                    CorrigeVideoProductoCommand.NotifyCanExecuteChanged();
                    BorrarVideoCommand.NotifyCanExecuteChanged();
                    DarDeBajaVideoCommand.NotifyCanExecuteChanged();
                }
            }
        }

        // Nesto#497: el botón «Borrar vídeo» solo lo ve quien puede borrar (NestoAPI#545): quien
        // lleva los vídeos (TiendaOnline), Dirección e Informática. La API lo vuelve a comprobar.
        private bool? _puedeBorrarVideos;
        public bool PuedeBorrarVideos => _puedeBorrarVideos ??=
            _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE) ||
            _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) ||
            _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA);

        private bool _estaBorrando;
        public bool EstaBorrando
        {
            get => _estaBorrando;
            set
            {
                if (SetProperty(ref _estaBorrando, value))
                {
                    BorrarVideoCommand.NotifyCanExecuteChanged();
                    DarDeBajaVideoCommand.NotifyCanExecuteChanged();
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
                    OnPropertyChanged(nameof(HayVideosProductosSinReferencia));
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
                    BuscarCommand.NotifyCanExecuteChanged();
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
                    CargarMasVideosCommand.NotifyCanExecuteChanged();
                    BuscarCommand.NotifyCanExecuteChanged();
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
                    CargarMasVideosCommand.NotifyCanExecuteChanged();
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

        public RelayCommand CargarMasVideosCommand { get; }

        private bool CanCargarMasVideos()
        {
            return !EstaCargando && HayMasVideos;
        }

        private async void OnCargarMasVideos()
        {
            await CargarVideosAsync(false);
        }

        public RelayCommand BuscarCommand { get; }

        private bool CanBuscar()
        {
            return !EstaCargando;
        }

        private async void OnBuscar()
        {
            EsBusqueda = !string.IsNullOrWhiteSpace(TextoBusqueda);
            await CargarVideosAsync(true);
        }

        public RelayCommand CorrigeVideoProductoCommand { get; }

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

            var dialogParameters = new ParametrosDialogo
            {
                { "producto", VideoCompletoSeleccionado }
            };

            var result = await _dialogService.ShowDialogAsync("CorreccionVideoProductoView", dialogParameters);

            if (result.Result == ResultadoBoton.OK)
            {
                VideoCompletoSeleccionado = await _servicio.CargarVideoCompleto(VideoCompletoSeleccionado.Id);
            }
        }

        public RelayCommand AbrirVideoEnNavegadorCommand { get; }

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

        public RelayCommand<string> AbrirProductoCommand { get; }

        private void OnAbrirProducto(string productoId)
        {
            if (!string.IsNullOrEmpty(productoId))
            {
                var parameters = new ParametrosNavegacion
                {
                    { "numeroProductoParameter", productoId }
                };
                _navegacion.RequestNavigate("MainRegion", "ProductoView", parameters);
            }
        }

        public AsyncRelayCommand BorrarVideoCommand { get; }

        private bool CanBorrarVideo()
        {
            return VideoCompletoSeleccionado != null && PuedeBorrarVideos && !EstaBorrando;
        }

        /// <summary>
        /// Nesto#497: borra el vídeo seleccionado tras confirmarlo. La API solo borra si está
        /// duplicado; si no, su mensaje explica que para retirarlo se usa la baja.
        /// </summary>
        private async Task OnBorrarVideo()
        {
            VideoModel video = VideoCompletoSeleccionado;
            if (video == null)
            {
                return;
            }

            if (!await _dialogService.ShowConfirmationAsync("Borrar vídeo", TextoConfirmacionBorrado(video)))
            {
                return;
            }

            EstaBorrando = true;
            try
            {
                await _servicio.BorrarVideo(video.Id);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
                return;
            }
            finally
            {
                EstaBorrando = false;
            }

            _dialogService.ShowNotification("Vídeo borrado", $"Se ha borrado el vídeo \"{video.Titulo}\".");
            VideoSeleccionado = null;
            VideoCompletoSeleccionado = null;
            await CargarVideosAsync(true);
        }

        public AsyncRelayCommand DarDeBajaVideoCommand { get; }

        /// <summary>
        /// Carlos 28/09/26: tienda online retira a su criterio los vídeos que en Nesto no pintan nada
        /// (shorts, vídeos de vida corta). Es la baja: sale del listado, del buscador y de la tienda,
        /// y se puede reponer (a mano, quitando la FechaBaja). Mismo permiso que el borrado.
        /// </summary>
        private async Task OnDarDeBajaVideo()
        {
            VideoModel video = VideoCompletoSeleccionado;
            if (video == null)
            {
                return;
            }

            if (!await _dialogService.ShowConfirmationAsync("Dar de baja el vídeo", TextoConfirmacionBaja(video)))
            {
                return;
            }

            EstaBorrando = true;
            try
            {
                await _servicio.DarDeBajaVideo(video.Id);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
                return;
            }
            finally
            {
                EstaBorrando = false;
            }

            _dialogService.ShowNotification("Vídeo dado de baja", $"El vídeo \"{video.Titulo}\" ya no sale en Nesto ni en la tienda.");
            VideoSeleccionado = null;
            VideoCompletoSeleccionado = null;
            await CargarVideosAsync(true);
        }

        internal static string TextoConfirmacionBaja(VideoModel video)
        {
            return $"¿Dar de baja el vídeo \"{video.Titulo}\" (YouTube {video.VideoId})? Dejará de salir en Nesto, en el buscador y en la tienda. Sus productos no se borran y se puede volver a dar de alta.";
        }

        internal static string TextoConfirmacionBorrado(VideoModel video)
        {
            int productos = video.Productos?.Count ?? 0;
            return $"¿Seguro que quieres borrar el vídeo \"{video.Titulo}\" (YouTube {video.VideoId}) y sus {productos} productos? No se puede deshacer.";
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
                MarcarDuplicados(Videos);

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

        /// <summary>
        /// Nesto#497: marca los vídeos que comparten VideoId de YouTube con otro de la lista (el
        /// 25/09/26, 1981 y 1983 duplicaban a 1980 y 1982). Solo ve lo cargado: los duplicados se
        /// publican a la vez y salen juntos en la lista.
        /// </summary>
        internal static void MarcarDuplicados(IEnumerable<VideoLookupModel> videos)
        {
            List<VideoLookupModel> lista = videos.ToList();
            HashSet<string> repetidos = lista
                .Where(v => !string.IsNullOrWhiteSpace(v.VideoId))
                .GroupBy(v => v.VideoId.Trim(), StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.Ordinal);
            foreach (VideoLookupModel video in lista)
            {
                video.EsDuplicado = !string.IsNullOrWhiteSpace(video.VideoId) && repetidos.Contains(video.VideoId.Trim());
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
