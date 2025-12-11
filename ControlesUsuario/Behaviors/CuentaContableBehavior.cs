using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Microsoft.Xaml.Behaviors;
using Nesto.Infrastructure.Contracts;
using Prism.Ioc;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlesUsuario.Behaviors
{
    /// <summary>
    /// Behavior reutilizable para TextBox que valida y busca cuentas contables.
    /// Soporta formato abreviado (572.13 → 57200013) y valida existencia en BD.
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
    ///         &lt;behaviors:CuentaContableBehavior
    ///             Empresa="{Binding DataContext.pedido.empresa, RelativeSource={RelativeSource AncestorType=DataGrid}}"
    ///             TipoLineaActual="{Binding tipoLinea}"
    ///             TipoLineaRequerido="2"/&gt;
    ///     &lt;/i:Interaction.Behaviors&gt;
    /// &lt;/TextBox&gt;
    /// </code>
    /// </summary>
    public class CuentaContableBehavior : Behavior<TextBox>
    {
        #region Dependency Properties

        /// <summary>
        /// Empresa para buscar la cuenta contable.
        /// </summary>
        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register(
                nameof(Empresa),
                typeof(string),
                typeof(CuentaContableBehavior),
                new PropertyMetadata(null));

        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty);
            set => SetValue(EmpresaProperty, value);
        }

        /// <summary>
        /// Tipo de línea actual (bindeado al campo tipoLinea de la fila).
        /// </summary>
        public static readonly DependencyProperty TipoLineaActualProperty =
            DependencyProperty.Register(
                nameof(TipoLineaActual),
                typeof(byte?),
                typeof(CuentaContableBehavior),
                new PropertyMetadata(null));

        public byte? TipoLineaActual
        {
            get => (byte?)GetValue(TipoLineaActualProperty);
            set => SetValue(TipoLineaActualProperty, value);
        }

        /// <summary>
        /// Tipo de línea que activa este behavior (por defecto 2 = cuenta contable).
        /// </summary>
        public static readonly DependencyProperty TipoLineaRequeridoProperty =
            DependencyProperty.Register(
                nameof(TipoLineaRequerido),
                typeof(byte),
                typeof(CuentaContableBehavior),
                new PropertyMetadata((byte)2));

        public byte TipoLineaRequerido
        {
            get => (byte)GetValue(TipoLineaRequeridoProperty);
            set => SetValue(TipoLineaRequeridoProperty, value);
        }

        /// <summary>
        /// Indica si el behavior está activo (ignora TipoLinea y siempre valida).
        /// Útil para TextBox que siempre son cuentas contables.
        /// </summary>
        public static readonly DependencyProperty SiempreActivoProperty =
            DependencyProperty.Register(
                nameof(SiempreActivo),
                typeof(bool),
                typeof(CuentaContableBehavior),
                new PropertyMetadata(false));

        public bool SiempreActivo
        {
            get => (bool)GetValue(SiempreActivoProperty);
            set => SetValue(SiempreActivoProperty, value);
        }

        /// <summary>
        /// Servicio de cuentas contables (opcional - si no se proporciona, se intentará resolver).
        /// </summary>
        public static readonly DependencyProperty ServicioProperty =
            DependencyProperty.Register(
                nameof(Servicio),
                typeof(IServicioCuentaContable),
                typeof(CuentaContableBehavior),
                new PropertyMetadata(null));

        public IServicioCuentaContable Servicio
        {
            get => (IServicioCuentaContable)GetValue(ServicioProperty);
            set => SetValue(ServicioProperty, value);
        }

        /// <summary>
        /// Propiedad para bindear al campo Texto de la línea.
        /// El behavior actualiza esta propiedad con el nombre de la cuenta cuando la encuentra.
        /// </summary>
        public static readonly DependencyProperty TextoProperty =
            DependencyProperty.Register(
                nameof(Texto),
                typeof(string),
                typeof(CuentaContableBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Texto
        {
            get => (string)GetValue(TextoProperty);
            set => SetValue(TextoProperty, value);
        }

        /// <summary>
        /// Propiedad para bindear al campo IVA de la línea (opcional).
        /// El behavior actualiza esta propiedad con el código IVA de la cuenta.
        /// </summary>
        public static readonly DependencyProperty IvaProperty =
            DependencyProperty.Register(
                nameof(Iva),
                typeof(string),
                typeof(CuentaContableBehavior),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Iva
        {
            get => (string)GetValue(IvaProperty);
            set => SetValue(IvaProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Evento que se dispara cuando se encuentra una cuenta contable válida.
        /// </summary>
        public event EventHandler<CuentaEncontradaEventArgs> CuentaEncontrada;

        /// <summary>
        /// Evento que se dispara cuando no se encuentra la cuenta o hay error.
        /// </summary>
        public event EventHandler<CuentaNoEncontradaEventArgs> CuentaNoEncontrada;

        #endregion

        #region Private Fields

        private IServicioCuentaContable _servicio;
        private string _valorAnterior;
        private Brush _borderBrushOriginal;
        private object _dataContextCapturado; // Capturar el DataContext antes de que cambie

        #endregion

        #region Behavior Overrides

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.LostFocus += OnLostFocus;
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
            AssociatedObject.GotFocus += OnGotFocus;
            _borderBrushOriginal = AssociatedObject.BorderBrush;
            System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] OnAttached: Behavior attached to TextBox");
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
                // Ignorar errores durante el detaching (puede ocurrir si el visual tree ya fue destruido)
            }
            _dataContextCapturado = null;
        }

        #endregion

        #region Event Handlers

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                // Capturar el DataContext cuando el TextBox recibe el foco
                // En este momento, el DataContext es el LineaPedidoVentaWrapper real, no el NewItemPlaceholder
                if (AssociatedObject == null) return;
                var dc = AssociatedObject.DataContext;
                if (dc != null && !dc.GetType().FullName.Contains("NamedObject"))
                {
                    _dataContextCapturado = dc;
                    System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] OnGotFocus: DataContext capturado tipo={dc.GetType().FullName}");
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
                await ValidarCuenta();
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
                System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] DebeValidar: SiempreActivo=true");
                return true;
            }

            // Si no tiene TipoLineaActual definido, no validar
            if (!TipoLineaActual.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] DebeValidar: TipoLineaActual es null, no validar");
                return false;
            }

            var debeValidar = TipoLineaActual.Value == TipoLineaRequerido;
            System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] DebeValidar: TipoLineaActual={TipoLineaActual.Value}, TipoLineaRequerido={TipoLineaRequerido}, resultado={debeValidar}");
            return debeValidar;
        }

        private async Task ValidarCuenta()
        {
            // Verificar que el AssociatedObject siga siendo válido (puede no serlo si se canceló la edición)
            if (AssociatedObject == null || !AssociatedObject.IsLoaded)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta iniciando...");

            if (!DebeValidar())
            {
                System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: No debe validar, saliendo");
                RestaurarEstiloNormal();
                return;
            }

            var texto = AssociatedObject.Text?.Trim();
            System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: texto='{texto}', Empresa='{Empresa}'");

            if (string.IsNullOrEmpty(texto))
            {
                System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: texto vacío, saliendo");
                RestaurarEstiloNormal();
                return;
            }

            // Intentar expandir la cuenta
            string cuentaExpandida;
            if (!CuentaContableHelper.TryExpandirCuenta(texto, out cuentaExpandida))
            {
                System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: formato inválido '{texto}'");
                MostrarError("Formato de cuenta inválido");
                OnCuentaNoEncontrada(texto, "Formato de cuenta inválido");
                return;
            }
            System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: cuenta expandida '{texto}' -> '{cuentaExpandida}'");

            // Obtener el servicio: primero de la propiedad, luego intentar resolver
            var servicio = Servicio ?? _servicio ?? ResolverServicio();
            if (servicio == null)
            {
                System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: Servicio es null, no se puede validar");
                MostrarError("Servicio no disponible");
                return;
            }
            _servicio = servicio; // Cachear para próximas llamadas
            System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: Servicio obtenido, buscando cuenta...");

            // Buscar la cuenta
            var cuenta = await _servicio.BuscarCuenta(Empresa, cuentaExpandida);

            if (cuenta != null)
            {
                System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: Cuenta encontrada - Cuenta='{cuenta.Cuenta}', Nombre='{cuenta.Nombre}', Iva='{cuenta.Iva}'");
                // Actualizar el TextBox con la cuenta expandida
                AssociatedObject.Text = cuenta.Cuenta;

                // Actualizar propiedades directamente en el DataContext capturado (más fiable que two-way binding en behaviors)
                // Usamos el DataContext capturado en OnGotFocus porque el actual puede ser NewItemPlaceholder
                var dataContext = _dataContextCapturado ?? AssociatedObject.DataContext;
                if (dataContext != null && !dataContext.GetType().FullName.Contains("NamedObject"))
                {
                    System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: Usando DataContext tipo={dataContext.GetType().FullName}");

                    // Usar reflexión para acceder a las propiedades Producto, texto e iva
                    var productoProperty = dataContext.GetType().GetProperty("Producto");
                    var textoProperty = dataContext.GetType().GetProperty("texto");
                    var ivaProperty = dataContext.GetType().GetProperty("iva");

                    // Actualizar Producto con la cuenta expandida (57200013 en vez de 572.13)
                    if (productoProperty != null && productoProperty.CanWrite)
                    {
                        productoProperty.SetValue(dataContext, cuenta.Cuenta);
                        System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: DataContext.Producto establecido a '{cuenta.Cuenta}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: No se pudo encontrar o escribir propiedad 'Producto'");
                    }

                    if (textoProperty != null && textoProperty.CanWrite)
                    {
                        textoProperty.SetValue(dataContext, cuenta.Nombre);
                        System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: DataContext.texto establecido a '{cuenta.Nombre}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: No se pudo encontrar o escribir propiedad 'texto'");
                    }

                    if (ivaProperty != null && ivaProperty.CanWrite)
                    {
                        ivaProperty.SetValue(dataContext, cuenta.Iva);
                        System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: DataContext.iva establecido a '{cuenta.Iva}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: No se pudo encontrar o escribir propiedad 'iva'");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: DataContext es null o es NewItemPlaceholder. _dataContextCapturado={_dataContextCapturado != null}");
                }

                // También actualizar las propiedades del behavior (por si acaso las bindings funcionan)
                Texto = cuenta.Nombre;
                Iva = cuenta.Iva;
                System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: Propiedades actualizadas - Texto='{Texto}', Iva='{Iva}'");
                MostrarExito();
                OnCuentaEncontrada(cuenta);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[CuentaContableBehavior] ValidarCuenta: Cuenta NO encontrada para '{cuentaExpandida}'");
                MostrarError("Cuenta no encontrada");
                OnCuentaNoEncontrada(cuentaExpandida, "La cuenta no existe en el plan contable");
            }
        }

        private IServicioCuentaContable ResolverServicio()
        {
            // Intentar resolver desde el contenedor de Prism
            try
            {
                if (ContainerLocator.Container != null)
                {
                    return ContainerLocator.Container.Resolve<IServicioCuentaContable>();
                }
            }
            catch
            {
                // Ignorar errores de resolución
            }

            // Alternativa: buscar IConfiguracion y crear el servicio manualmente
            try
            {
                if (ContainerLocator.Container != null)
                {
                    var configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
                    if (configuracion != null)
                    {
                        return new ServicioCuentaContable(configuracion);
                    }
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

        private void OnCuentaEncontrada(CuentaContableDTO cuenta)
        {
            CuentaEncontrada?.Invoke(this, new CuentaEncontradaEventArgs
            {
                Cuenta = cuenta.Cuenta,
                Nombre = cuenta.Nombre,
                Iva = cuenta.Iva
            });
        }

        private void OnCuentaNoEncontrada(string cuentaBuscada, string motivo)
        {
            CuentaNoEncontrada?.Invoke(this, new CuentaNoEncontradaEventArgs
            {
                CuentaBuscada = cuentaBuscada,
                Motivo = motivo
            });
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// Argumentos del evento CuentaEncontrada.
    /// </summary>
    public class CuentaEncontradaEventArgs : EventArgs
    {
        public string Cuenta { get; set; }
        public string Nombre { get; set; }
        public string Iva { get; set; }
    }

    /// <summary>
    /// Argumentos del evento CuentaNoEncontrada.
    /// </summary>
    public class CuentaNoEncontradaEventArgs : EventArgs
    {
        public string CuentaBuscada { get; set; }
        public string Motivo { get; set; }
    }

    #endregion
}
