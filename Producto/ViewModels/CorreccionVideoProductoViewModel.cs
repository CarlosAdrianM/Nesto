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

namespace Nesto.Modules.Producto.ViewModels
{
    public class CorreccionVideoProductoViewModel : BindableBase, IDialogAware
    {
        private readonly IProductoService _productoService;
        private readonly IRegionManager _regionManager;
        private Action<IDialogResult> _requestClose;

        public CorreccionVideoProductoViewModel(IProductoService productoService, IRegionManager regionManager)
        {
            _productoService = productoService;
            _regionManager = regionManager;
            ProductosEditables = [];

            GuardarCommand = new DelegateCommand(OnGuardar, CanGuardar);
            AbrirProductosConBusquedaCommand = new DelegateCommand<string>(OnAbrirProductosConBusqueda, CanAbrirProductosConBusqueda);
        }

        // Implementación correcta del evento RequestClose
        event Action<IDialogResult> IDialogAware.RequestClose
        {
            add => _requestClose += value;
            remove => _requestClose -= value;
        }

        // Propiedades del video
        private VideoModel _video;
        public VideoModel Video
        {
            get => _video;
            set
            {
                _ = SetProperty(ref _video, value);
                RaisePropertyChanged(nameof(TituloVideo));
                CargarProductosEditables();
            }
        }

        public string TituloVideo => Video?.Titulo;

        // Lista de productos editables
        public ObservableCollection<ProductoEditable> ProductosEditables { get; }

        // Comandos
        public DelegateCommand GuardarCommand { get; }
        public DelegateCommand<string> AbrirProductosConBusquedaCommand { get; }

        // Resumen de cambios
        public string ResumenCambios
        {
            get
            {
                if (ProductosEditables == null || !ProductosEditables.Any())
                {
                    return "";
                }

                var conCambios = ProductosEditables.Count(p => p.TieneCambios);
                if (conCambios == 0)
                {
                    return "Sin cambios";
                }

                var paraEliminar = ProductosEditables.Count(p => p.MarcarParaEliminar);
                var paraActualizar = conCambios - paraEliminar;

                var partes = new List<string>();
                if (paraActualizar > 0)
                {
                    partes.Add($"{paraActualizar} para actualizar");
                }

                if (paraEliminar > 0)
                {
                    partes.Add($"{paraEliminar} para eliminar");
                }

                return string.Join(", ", partes);
            }
        }

        // Propiedades de IDialogAware
        public string Title => "Correcciones de productos del vídeo";

        private void CargarProductosEditables()
        {
            ProductosEditables.Clear();

            if (Video?.Productos != null)
            {
                foreach (var producto in Video.Productos)
                {
                    var editable = new ProductoEditable(producto);
                    editable.PropertyChanged += (s, e) =>
                    {
                        GuardarCommand.RaiseCanExecuteChanged();
                        RaisePropertyChanged(nameof(ResumenCambios));
                    };
                    ProductosEditables.Add(editable);
                }

                _ = CargarNombresProductoAsociadoAsync();
            }

            GuardarCommand.RaiseCanExecuteChanged();
        }

        internal async System.Threading.Tasks.Task CargarNombresProductoAsociadoAsync()
        {
            foreach (var editable in ProductosEditables.ToList())
            {
                if (string.IsNullOrWhiteSpace(editable.Referencia))
                {
                    continue;
                }

                try
                {
                    var producto = await _productoService.LeerProducto(editable.Referencia);
                    editable.NombreProductoAsociado = producto?.Nombre;
                }
                catch
                {
                    // Si la referencia no existe o falla la API, dejamos NombreProductoAsociado vacío
                }
            }
        }

        private async void OnGuardar()
        {
            try
            {
                var productosConCambios = ProductosEditables.Where(p => p.TieneCambios).ToList();

                if (!productosConCambios.Any())
                {
                    // Sin cambios, cerrar diálogo
                    _requestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
                    return;
                }

                // Procesar cada producto con cambios
                foreach (var productoEditable in productosConCambios)
                {
                    if (productoEditable.MarcarParaEliminar)
                    {
                        // Eliminar producto
                        await _productoService.EliminarVideoProducto(
                            productoEditable.ProductoOriginal.Id,
                            "Producto eliminado: no aparece en el vídeo");
                    }
                    else
                    {
                        // Actualizar producto
                        var dto = new ActualizacionVideoProductoDto
                        {
                            Referencia = productoEditable.Referencia,
                            EnlaceTienda = productoEditable.EnlaceTienda,
                            TiempoAparicion = productoEditable.TiempoAparicion,
                            Observaciones = "Corrección desde interfaz de video"
                        };

                        await _productoService.ActualizarVideoProducto(productoEditable.ProductoOriginal.Id, dto);
                    }
                }

                // Cerrar con éxito
                var result = new DialogResult(ButtonResult.OK);
                result.Parameters.Add("productosModificados", productosConCambios.Count);
                _requestClose?.Invoke(result);
            }
            catch (Exception)
            {
                // Aquí podrías mostrar un mensaje de error
                // Por ahora solo cerramos con Cancel
                _requestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
            }
        }

        private bool CanGuardar()
        {
            return ProductosEditables?.Any(p => p.TieneCambios) == true;
        }

        private bool CanAbrirProductosConBusqueda(string nombre) => !string.IsNullOrWhiteSpace(nombre);

        private void OnAbrirProductosConBusqueda(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return;
            }

            // Issue #343: navegar a Productos con búsqueda contextual del nombre del VideoProducto.
            // Cerramos el diálogo primero: navegar con el diálogo modal encima genera
            // experiencia confusa y no tenemos cambios sin guardar (los cambios se registran
            // vía ResumenCambios y el CanGuardar lo advertiría si los hubiese).
            var parameters = new NavigationParameters
            {
                { "busquedaContextualParameter", nombre }
            };

            _requestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
            _regionManager.RequestNavigate("MainRegion", "ProductoView", parameters);
        }

        private void OnAbrirVideo()
        {
            if (!string.IsNullOrEmpty(Video?.UrlVideo))
            {
                _ = Process.Start(new ProcessStartInfo(Video.UrlVideo) { UseShellExecute = true });
            }
        }

        private bool CanAbrirVideo()
        {
            return !string.IsNullOrEmpty(Video?.UrlVideo);
        }

        // Métodos de IDialogAware
        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            // Limpieza si es necesaria
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // Tu código usa "producto" como parámetro
            if (parameters.ContainsKey("producto"))
            {
                Video = parameters.GetValue<VideoModel>("producto");
            }
        }

        // Método público para cerrar el diálogo (para el code-behind)
        public void CerrarDialogo(ButtonResult resultado)
        {
            _requestClose?.Invoke(new DialogResult(resultado));
        }
    }

    // Clase helper para manejar productos editables
    public class ProductoEditable : BindableBase
    {
        public ProductoVideoModel ProductoOriginal { get; }

        public ProductoEditable(ProductoVideoModel productoOriginal)
        {
            ProductoOriginal = productoOriginal;

            // Inicializar con valores originales
            _referencia = productoOriginal.Referencia;
            _nombreProducto = productoOriginal.NombreProducto;
            _enlaceTienda = productoOriginal.EnlaceTienda;
            _tiempoAparicion = productoOriginal.TiempoAparicion;

            // Comandos para abrir enlaces
            AbrirEnlaceVideoCommand = new DelegateCommand(OnAbrirEnlaceVideo, CanAbrirEnlaceVideo);
            AbrirEnlaceTiendaCommand = new DelegateCommand(OnAbrirEnlaceTienda, CanAbrirEnlaceTienda);
        }

        // Comandos
        public DelegateCommand AbrirEnlaceVideoCommand { get; }
        public DelegateCommand AbrirEnlaceTiendaCommand { get; }

        // Nombre del VideoProducto tal como lo nombra el vídeo (ej. "Alta Frecuencia").
        // Se inicializa desde ProductoOriginal.NombreProducto y NO se sobrescribe al resolver
        // la Referencia: el nombre del producto asociado a la Referencia va en NombreProductoAsociado.
        private string _nombreProducto;
        public string NombreProducto
        {
            get => _nombreProducto;
            set => SetProperty(ref _nombreProducto, value);
        }

        // Nombre del producto real que apunta la Referencia (solo lectura desde la UI).
        // Se rellena al cargar el diálogo y se actualiza cuando el behaviour resuelve la
        // referencia contra la API. Sirve para detectar errores tipo "el vídeo habla de un
        // Alta Frecuencia pero la referencia apunta a una Crema anticelulítica" (Issue #347).
        private string _nombreProductoAsociado;
        public string NombreProductoAsociado
        {
            get => _nombreProductoAsociado;
            set => SetProperty(ref _nombreProductoAsociado, value);
        }

        public string EnlaceVideoOriginal => ProductoOriginal.EnlaceVideo;

        // Propiedades editables
        private string _referencia;
        public string Referencia
        {
            get => _referencia;
            set
            {
                _ = SetProperty(ref _referencia, value);
                RaisePropertyChanged(nameof(TieneCambios));
            }
        }

        private string _enlaceTienda;
        public string EnlaceTienda
        {
            get => _enlaceTienda;
            set
            {
                _ = SetProperty(ref _enlaceTienda, value);
                RaisePropertyChanged(nameof(TieneCambios));
            }
        }

        private string _tiempoAparicion;
        public string TiempoAparicion
        {
            get => _tiempoAparicion;
            set
            {
                _ = SetProperty(ref _tiempoAparicion, value);
                RaisePropertyChanged(nameof(TieneCambios));
            }
        }

        private bool _marcarParaEliminar;
        public bool MarcarParaEliminar
        {
            get => _marcarParaEliminar;
            set
            {
                _ = SetProperty(ref _marcarParaEliminar, value);
                RaisePropertyChanged(nameof(TieneCambios));
            }
        }

        // Indica si este producto tiene cambios pendientes
        public bool TieneCambios =>
            MarcarParaEliminar ||
            Referencia != ProductoOriginal.Referencia ||
            EnlaceTienda != ProductoOriginal.EnlaceTienda ||
            TiempoAparicion != ProductoOriginal.TiempoAparicion;

        // Métodos para abrir enlaces
        private void OnAbrirEnlaceVideo()
        {
            if (!string.IsNullOrEmpty(ProductoOriginal.EnlaceVideo))
            {
                _ = Process.Start(new ProcessStartInfo(ProductoOriginal.EnlaceVideo) { UseShellExecute = true });
            }
        }

        private bool CanAbrirEnlaceVideo()
        {
            return !string.IsNullOrEmpty(ProductoOriginal.EnlaceVideo);
        }

        private void OnAbrirEnlaceTienda()
        {
            if (!string.IsNullOrEmpty(EnlaceTienda))
            {
                _ = Process.Start(new ProcessStartInfo(EnlaceTienda) { UseShellExecute = true });
            }
        }

        private bool CanAbrirEnlaceTienda()
        {
            return !string.IsNullOrEmpty(EnlaceTienda);
        }
    }

    // DTOs (mantener consistencia con el API)
    public class ActualizacionVideoProductoDto
    {
        public string Referencia { get; set; }
        public string EnlaceTienda { get; set; }
        public string TiempoAparicion { get; set; }
        public string Observaciones { get; set; }
    }
}