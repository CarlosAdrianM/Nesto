using Nesto.Modules.Producto.Models;
using Prism.Commands;
using Prism.Mvvm;
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
        private Action<IDialogResult> _requestClose;

        public CorreccionVideoProductoViewModel(IProductoService productoService)
        {
            _productoService = productoService;
            ProductosEditables = [];

            GuardarCommand = new DelegateCommand(OnGuardar, CanGuardar);
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
            }

            GuardarCommand.RaiseCanExecuteChanged();
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
            _enlaceTienda = productoOriginal.EnlaceTienda;
            _tiempoAparicion = productoOriginal.TiempoAparicion;

            // Comandos para abrir enlaces
            AbrirEnlaceVideoCommand = new DelegateCommand(OnAbrirEnlaceVideo, CanAbrirEnlaceVideo);
            AbrirEnlaceTiendaCommand = new DelegateCommand(OnAbrirEnlaceTienda, CanAbrirEnlaceTienda);
        }

        // Comandos
        public DelegateCommand AbrirEnlaceVideoCommand { get; }
        public DelegateCommand AbrirEnlaceTiendaCommand { get; }

        // Propiedades de solo lectura del producto original
        public string NombreProducto => ProductoOriginal.NombreProducto;
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