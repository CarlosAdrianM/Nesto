using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Microsoft.Xaml.Behaviors;
using Nesto.Infrastructure.Contracts;
using Prism.Ioc;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlesUsuario.Behaviors
{
    /// <summary>
    /// Behavior reutilizable para TextBox que busca y valida productos.
    /// Cuando el usuario introduce un código de producto y pierde el foco,
    /// el behavior consulta la API y rellena los campos de la línea.
    ///
    /// Issue #258 - Carlos 11/12/25
    ///
    /// Uso en XAML:
    /// <code>
    /// xmlns:behaviors="clr-namespace:ControlesUsuario.Behaviors;assembly=ControlesUsuario"
    /// xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
    ///
    /// &lt;TextBox Text="{Binding Producto}"&gt;
    ///     &lt;i:Interaction.Behaviors&gt;
    ///         &lt;behaviors:ProductoBehavior
    ///             Empresa="{Binding DataContext.pedido.empresa, RelativeSource={RelativeSource AncestorType=DataGrid}}"
    ///             Cliente="{Binding DataContext.pedido.cliente, RelativeSource={RelativeSource AncestorType=DataGrid}}"
    ///             Contacto="{Binding DataContext.pedido.contacto, RelativeSource={RelativeSource AncestorType=DataGrid}}"
    ///             TipoLineaActual="{Binding tipoLinea}"
    ///             TipoLineaRequerido="1"/&gt;
    ///     &lt;/i:Interaction.Behaviors&gt;
    /// &lt;/TextBox&gt;
    /// </code>
    /// </summary>
    public class ProductoBehavior : Behavior<TextBox>
    {
        #region Dependency Properties

        /// <summary>
        /// Empresa para buscar el producto.
        /// </summary>
        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register(
                nameof(Empresa),
                typeof(string),
                typeof(ProductoBehavior),
                new PropertyMetadata(null));

        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty);
            set => SetValue(EmpresaProperty, value);
        }

        /// <summary>
        /// Cliente para buscar precios específicos.
        /// </summary>
        public static readonly DependencyProperty ClienteProperty =
            DependencyProperty.Register(
                nameof(Cliente),
                typeof(string),
                typeof(ProductoBehavior),
                new PropertyMetadata(null));

        public string Cliente
        {
            get => (string)GetValue(ClienteProperty);
            set => SetValue(ClienteProperty, value);
        }

        /// <summary>
        /// Contacto del cliente para precios específicos.
        /// </summary>
        public static readonly DependencyProperty ContactoProperty =
            DependencyProperty.Register(
                nameof(Contacto),
                typeof(string),
                typeof(ProductoBehavior),
                new PropertyMetadata(null));

        public string Contacto
        {
            get => (string)GetValue(ContactoProperty);
            set => SetValue(ContactoProperty, value);
        }

        /// <summary>
        /// Tipo de línea actual (bindeado al campo tipoLinea de la fila).
        /// </summary>
        public static readonly DependencyProperty TipoLineaActualProperty =
            DependencyProperty.Register(
                nameof(TipoLineaActual),
                typeof(byte?),
                typeof(ProductoBehavior),
                new PropertyMetadata(null));

        public byte? TipoLineaActual
        {
            get => (byte?)GetValue(TipoLineaActualProperty);
            set => SetValue(TipoLineaActualProperty, value);
        }

        /// <summary>
        /// Tipo de línea que activa este behavior (por defecto 1 = producto).
        /// </summary>
        public static readonly DependencyProperty TipoLineaRequeridoProperty =
            DependencyProperty.Register(
                nameof(TipoLineaRequerido),
                typeof(byte),
                typeof(ProductoBehavior),
                new PropertyMetadata((byte)1));

        public byte TipoLineaRequerido
        {
            get => (byte)GetValue(TipoLineaRequeridoProperty);
            set => SetValue(TipoLineaRequeridoProperty, value);
        }

        /// <summary>
        /// Indica si el behavior está activo (ignora TipoLinea y siempre valida).
        /// </summary>
        public static readonly DependencyProperty SiempreActivoProperty =
            DependencyProperty.Register(
                nameof(SiempreActivo),
                typeof(bool),
                typeof(ProductoBehavior),
                new PropertyMetadata(false));

        public bool SiempreActivo
        {
            get => (bool)GetValue(SiempreActivoProperty);
            set => SetValue(SiempreActivoProperty, value);
        }

        /// <summary>
        /// Servicio de productos (opcional - si no se proporciona, se intentará resolver).
        /// </summary>
        public static readonly DependencyProperty ServicioProperty =
            DependencyProperty.Register(
                nameof(Servicio),
                typeof(IServicioProducto),
                typeof(ProductoBehavior),
                new PropertyMetadata(null));

        public IServicioProducto Servicio
        {
            get => (IServicioProducto)GetValue(ServicioProperty);
            set => SetValue(ServicioProperty, value);
        }

        /// <summary>
        /// Cantidad de la línea (para calcular precios por volumen).
        /// </summary>
        public static readonly DependencyProperty CantidadProperty =
            DependencyProperty.Register(
                nameof(Cantidad),
                typeof(short),
                typeof(ProductoBehavior),
                new PropertyMetadata((short)1));

        public short Cantidad
        {
            get => (short)GetValue(CantidadProperty);
            set => SetValue(CantidadProperty, value);
        }

        /// <summary>
        /// Propiedad para bindear al campo Texto de la línea.
        /// </summary>
        public static readonly DependencyProperty TextoProperty =
            DependencyProperty.Register(
                nameof(Texto),
                typeof(string),
                typeof(ProductoBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Texto
        {
            get => (string)GetValue(TextoProperty);
            set => SetValue(TextoProperty, value);
        }

        /// <summary>
        /// Propiedad para bindear al campo Precio de la línea.
        /// </summary>
        public static readonly DependencyProperty PrecioProperty =
            DependencyProperty.Register(
                nameof(Precio),
                typeof(decimal),
                typeof(ProductoBehavior),
                new FrameworkPropertyMetadata(0m, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public decimal Precio
        {
            get => (decimal)GetValue(PrecioProperty);
            set => SetValue(PrecioProperty, value);
        }

        /// <summary>
        /// Propiedad para bindear al campo IVA de la línea.
        /// </summary>
        public static readonly DependencyProperty IvaProperty =
            DependencyProperty.Register(
                nameof(Iva),
                typeof(string),
                typeof(ProductoBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Iva
        {
            get => (string)GetValue(IvaProperty);
            set => SetValue(IvaProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Evento que se dispara cuando se encuentra un producto válido.
        /// </summary>
        public event EventHandler<ProductoEncontradoEventArgs> ProductoEncontrado;

        /// <summary>
        /// Evento que se dispara cuando no se encuentra el producto o hay error.
        /// </summary>
        public event EventHandler<ProductoNoEncontradoEventArgs> ProductoNoEncontrado;

        #endregion

        #region Private Fields

        private IServicioProducto _servicio;
        private string _valorAnterior;
        private Brush _borderBrushOriginal;
        private object _dataContextCapturado;

        #endregion

        #region Behavior Overrides

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.LostFocus += OnLostFocus;
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
            AssociatedObject.GotFocus += OnGotFocus;
            _borderBrushOriginal = AssociatedObject.BorderBrush;
            System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] OnAttached: Behavior attached to TextBox");
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            try
            {
                if (AssociatedObject != null)
                {
                    AssociatedObject.LostFocus -= OnLostFocus;
                    AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
                    AssociatedObject.GotFocus -= OnGotFocus;
                }
            }
            catch
            {
                // Ignorar errores durante el detaching
            }
            _dataContextCapturado = null;
        }

        #endregion

        #region Event Handlers

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AssociatedObject == null) return;
                var dc = AssociatedObject.DataContext;
                if (dc != null && !dc.GetType().FullName.Contains("NamedObject"))
                {
                    _dataContextCapturado = dc;
                    System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] OnGotFocus: DataContext capturado tipo={dc.GetType().FullName}");
                }
            }
            catch
            {
                // Ignorar errores si el visual tree fue destruido
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (AssociatedObject == null) return;
                if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    _valorAnterior = AssociatedObject.Text;
                }
            }
            catch
            {
                // Ignorar errores si el visual tree fue destruido
            }
        }

        private async void OnLostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                await ValidarProducto();
            }
            catch
            {
                // Ignorar errores si el visual tree fue destruido durante la validación
            }
        }

        #endregion

        #region Private Methods

        private bool DebeValidar()
        {
            if (SiempreActivo)
            {
                System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] DebeValidar: SiempreActivo=true");
                return true;
            }

            if (!TipoLineaActual.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] DebeValidar: TipoLineaActual es null, no validar");
                return false;
            }

            var debeValidar = TipoLineaActual.Value == TipoLineaRequerido;
            System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] DebeValidar: TipoLineaActual={TipoLineaActual.Value}, TipoLineaRequerido={TipoLineaRequerido}, resultado={debeValidar}");
            return debeValidar;
        }

        private async Task ValidarProducto()
        {
            if (AssociatedObject == null || !AssociatedObject.IsLoaded)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto iniciando...");

            if (!DebeValidar())
            {
                System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: No debe validar, saliendo");
                RestaurarEstiloNormal();
                return;
            }

            var texto = AssociatedObject.Text?.Trim();
            System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: texto='{texto}', Empresa='{Empresa}'");

            if (string.IsNullOrEmpty(texto))
            {
                System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: texto vacío, saliendo");
                RestaurarEstiloNormal();
                return;
            }

            // Obtener el servicio
            var servicio = Servicio ?? _servicio ?? ResolverServicio();
            if (servicio == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: Servicio es null, no se puede validar");
                MostrarError("Servicio no disponible");
                return;
            }
            _servicio = servicio;
            System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: Servicio obtenido, buscando producto...");

            // Buscar el producto
            var producto = await _servicio.BuscarProducto(Empresa, texto, Cliente, Contacto, Cantidad);

            if (producto != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: Producto encontrado - Producto='{producto.Producto}', Nombre='{producto.Nombre}', Precio={producto.Precio}");

                // Verificar si el producto realmente cambió (ignorando mayúsculas/minúsculas y espacios)
                bool productoCambio = !string.Equals(texto, producto.Producto?.Trim(), StringComparison.OrdinalIgnoreCase);

                // Actualizar el TextBox solo si el código del producto es diferente
                if (productoCambio)
                {
                    AssociatedObject.Text = producto.Producto;
                    System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: Producto cambió de '{texto}' a '{producto.Producto}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: Producto no cambió, no se actualiza TextBox");
                }

                // Actualizar propiedades directamente en el DataContext capturado
                var dataContext = _dataContextCapturado ?? AssociatedObject.DataContext;
                if (dataContext != null && !dataContext.GetType().FullName.Contains("NamedObject"))
                {
                    System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: Usando DataContext tipo={dataContext.GetType().FullName}");

                    // Solo actualizar Producto si realmente cambió
                    if (productoCambio)
                    {
                        SetProperty(dataContext, "Producto", producto.Producto);
                    }
                    SetPropertyIfChanged(dataContext, "texto", producto.Nombre);
                    SetPropertyIfChanged(dataContext, "PrecioUnitario", producto.Precio);
                    SetProperty(dataContext, "AplicarDescuento", producto.AplicarDescuento);
                    SetPropertyIfChanged(dataContext, "DescuentoProducto", producto.Descuento);
                    SetPropertyIfChanged(dataContext, "iva", producto.Iva);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: DataContext es null o es NewItemPlaceholder");
                }

                // Actualizar las propiedades del behavior solo si cambiaron
                if (!string.Equals(Texto, producto.Nombre))
                {
                    Texto = producto.Nombre;
                }
                if (Precio != producto.Precio)
                {
                    Precio = producto.Precio;
                }
                if (!string.Equals(Iva, producto.Iva))
                {
                    Iva = producto.Iva;
                }
                MostrarExito();
                OnProductoEncontrado(producto);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] ValidarProducto: Producto NO encontrado para '{texto}'");
                MostrarError("Producto no encontrado");
                OnProductoNoEncontrado(texto, "El producto no existe");
            }
        }

        private void SetProperty(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] SetProperty: {propertyName} = {value}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] SetProperty: No se pudo establecer '{propertyName}'");
            }
        }

        /// <summary>
        /// Establece una propiedad solo si el valor es diferente al actual.
        /// Evita disparar PropertyChanged innecesariamente.
        /// </summary>
        private void SetPropertyIfChanged(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                var currentValue = property.GetValue(target);
                if (!Equals(currentValue, value))
                {
                    property.SetValue(target, value);
                    System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] SetPropertyIfChanged: {propertyName} cambió de '{currentValue}' a '{value}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProductoBehavior] SetPropertyIfChanged: {propertyName} sin cambios (valor={value})");
                }
            }
        }

        private IServicioProducto ResolverServicio()
        {
            try
            {
                if (ContainerLocator.Container != null)
                {
                    return ContainerLocator.Container.Resolve<IServicioProducto>();
                }
            }
            catch
            {
                // Ignorar errores de resolución
            }
            return null;
        }

        private void MostrarError(string mensaje)
        {
            if (AssociatedObject == null || !AssociatedObject.IsLoaded) return;
            AssociatedObject.BorderBrush = Brushes.Red;
            AssociatedObject.ToolTip = mensaje;
        }

        private void MostrarExito()
        {
            if (AssociatedObject == null || !AssociatedObject.IsLoaded) return;
            AssociatedObject.BorderBrush = Brushes.Green;
            AssociatedObject.ToolTip = null;
        }

        private void RestaurarEstiloNormal()
        {
            if (AssociatedObject == null || !AssociatedObject.IsLoaded) return;
            AssociatedObject.BorderBrush = _borderBrushOriginal;
            AssociatedObject.ToolTip = null;
        }

        private void OnProductoEncontrado(ProductoDTO producto)
        {
            ProductoEncontrado?.Invoke(this, new ProductoEncontradoEventArgs
            {
                Producto = producto.Producto,
                Nombre = producto.Nombre,
                Precio = producto.Precio,
                AplicarDescuento = producto.AplicarDescuento,
                Descuento = producto.Descuento,
                Iva = producto.Iva
            });
        }

        private void OnProductoNoEncontrado(string productoBuscado, string motivo)
        {
            ProductoNoEncontrado?.Invoke(this, new ProductoNoEncontradoEventArgs
            {
                ProductoBuscado = productoBuscado,
                Motivo = motivo
            });
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// Argumentos del evento ProductoEncontrado.
    /// </summary>
    public class ProductoEncontradoEventArgs : EventArgs
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public decimal Precio { get; set; }
        public bool AplicarDescuento { get; set; }
        public decimal Descuento { get; set; }
        public string Iva { get; set; }
    }

    /// <summary>
    /// Argumentos del evento ProductoNoEncontrado.
    /// </summary>
    public class ProductoNoEncontradoEventArgs : EventArgs
    {
        public string ProductoBuscado { get; set; }
        public string Motivo { get; set; }
    }

    #endregion
}
