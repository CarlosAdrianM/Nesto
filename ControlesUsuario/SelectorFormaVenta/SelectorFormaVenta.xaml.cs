using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Control para seleccionar una Forma de Venta.
    /// Carlos 04/12/24: Issue #252 - Creado siguiendo el patrón de SelectorCCC con DI y anti-bucles.
    /// </summary>
    public partial class SelectorFormaVenta : UserControl, INotifyPropertyChanged
    {
        private readonly IServicioFormaVenta _servicioFormaVenta;
        private bool _estaCargando = false;
        private bool _estaAutoSeleccionando = false;

        #region Constructores

        /// <summary>
        /// Constructor sin parámetros (usado por XAML).
        /// Resuelve dependencias desde ContainerLocator.
        /// </summary>
        public SelectorFormaVenta()
        {
            InitializeComponent();

            try
            {
                _servicioFormaVenta = ContainerLocator.Container.Resolve<IServicioFormaVenta>();
            }
            catch
            {
                // Se usa solo para poder testar controles que incluyan un SelectorFormaVenta
            }
        }

        /// <summary>
        /// Constructor con DI (PREFERIDO).
        /// </summary>
        public SelectorFormaVenta(IServicioFormaVenta servicioFormaVenta)
        {
            InitializeComponent();

            _servicioFormaVenta = servicioFormaVenta ?? throw new ArgumentNullException(nameof(servicioFormaVenta));
        }

        #endregion

        #region DependencyProperties - Entradas

        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register(
                nameof(Empresa),
                typeof(string),
                typeof(SelectorFormaVenta),
                new FrameworkPropertyMetadata(null, OnEmpresaChanged));

        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty);
            set => SetValue(EmpresaProperty, value);
        }

        private static void OnEmpresaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorFormaVenta)d;
            Debug.WriteLine($"[SelectorFormaVenta] OnEmpresaChanged: '{e.OldValue}' -> '{e.NewValue}', _estaCargando={selector._estaCargando}");
            if (selector._estaCargando) return; // Prevenir bucles
            selector.CargarFormasVentaAsync();
        }

        /// <summary>
        /// Permite filtrar solo las formas de venta visibles por comerciales.
        /// Por defecto es false (muestra todas).
        /// </summary>
        public static readonly DependencyProperty SoloVisiblesPorComercialesProperty =
            DependencyProperty.Register(
                nameof(SoloVisiblesPorComerciales),
                typeof(bool),
                typeof(SelectorFormaVenta),
                new FrameworkPropertyMetadata(false));

        public bool SoloVisiblesPorComerciales
        {
            get => (bool)GetValue(SoloVisiblesPorComercialesProperty);
            set => SetValue(SoloVisiblesPorComercialesProperty, value);
        }

        #endregion

        #region DependencyProperties - Salida (TwoWay)

        /// <summary>
        /// Propiedad para binding con string (número de la forma de venta).
        /// </summary>
        public static readonly DependencyProperty FormaVentaSeleccionadaProperty =
            DependencyProperty.Register(
                nameof(FormaVentaSeleccionada),
                typeof(string),
                typeof(SelectorFormaVenta),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnFormaVentaSeleccionadaChanged));

        public string FormaVentaSeleccionada
        {
            get => (string)GetValue(FormaVentaSeleccionadaProperty);
            set => SetValue(FormaVentaSeleccionadaProperty, value);
        }

        private static void OnFormaVentaSeleccionadaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorFormaVenta)d;

            // Comparar valores para evitar propagaciones innecesarias (prevenir bucles)
            if (e.OldValue?.ToString() == e.NewValue?.ToString())
                return;

            // Evitar bucle infinito cuando AutoSeleccionarFormaVenta() cambia el valor
            if (selector._estaAutoSeleccionando)
                return;

            Debug.WriteLine($"[SelectorFormaVenta] OnFormaVentaSeleccionadaChanged: '{e.OldValue}' -> '{e.NewValue}', _estaCargando={selector._estaCargando}");

            // Si no estamos cargando y ya hay lista, intentar auto-seleccionar
            // Esto maneja el caso cuando el valor se establece después de que la lista ya cargó
            if (!selector._estaCargando && selector.ListaFormasVenta != null && selector.ListaFormasVenta.Any())
            {
                selector.AutoSeleccionarFormaVenta();
            }

            selector.OnPropertyChanged(nameof(FormaVentaSeleccionada));
        }

        /// <summary>
        /// Propiedad para binding con objeto completo FormaVentaItem.
        /// </summary>
        public static readonly DependencyProperty FormaVentaSeleccionadaCompletaProperty =
            DependencyProperty.Register(
                nameof(FormaVentaSeleccionadaCompleta),
                typeof(FormaVentaItem),
                typeof(SelectorFormaVenta),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnFormaVentaSeleccionadaCompletaChanged));

        public FormaVentaItem FormaVentaSeleccionadaCompleta
        {
            get => (FormaVentaItem)GetValue(FormaVentaSeleccionadaCompletaProperty);
            set => SetValue(FormaVentaSeleccionadaCompletaProperty, value);
        }

        private static void OnFormaVentaSeleccionadaCompletaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorFormaVenta)d;
            var newValue = e.NewValue as FormaVentaItem;
            var oldValue = e.OldValue as FormaVentaItem;

            // Comparar valores para evitar propagaciones innecesarias (prevenir bucles)
            if (oldValue?.Numero == newValue?.Numero)
                return;

            Debug.WriteLine($"[SelectorFormaVenta] OnFormaVentaSeleccionadaCompletaChanged: '{oldValue?.Numero}' -> '{newValue?.Numero}'");

            // Sincronizar con FormaVentaSeleccionada (string)
            if (newValue != null && selector.FormaVentaSeleccionada != newValue.Numero)
            {
                selector.FormaVentaSeleccionada = newValue.Numero;
            }

            selector.OnPropertyChanged(nameof(FormaVentaSeleccionadaCompleta));
        }

        #endregion

        #region Propiedades Internas (INotifyPropertyChanged)

        private ObservableCollection<FormaVentaItem> _listaFormasVenta;
        public ObservableCollection<FormaVentaItem> ListaFormasVenta
        {
            get => _listaFormasVenta;
            set
            {
                if (_listaFormasVenta != value)
                {
                    _listaFormasVenta = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Lógica de Carga

        /// <summary>
        /// Valor especial para indicar que hay diferentes formas de venta en las líneas.
        /// Carlos 09/12/25: Debe coincidir con VALOR_VARIOS en DetallePedidoViewModel.
        /// </summary>
        public const string VALOR_VARIOS = "VARIOS";

        /// <summary>
        /// Carga las Formas de Venta desde la API para la Empresa actual.
        /// Incluye anti-loop con flag _estaCargando.
        /// </summary>
        private async void CargarFormasVentaAsync()
        {
            Debug.WriteLine($"[SelectorFormaVenta] CargarFormasVentaAsync - Empresa='{Empresa}'");

            // Validar que tenemos los datos necesarios
            if (string.IsNullOrWhiteSpace(Empresa))
            {
                Debug.WriteLine($"[SelectorFormaVenta] Empresa vacía - Saliendo");
                ListaFormasVenta = new ObservableCollection<FormaVentaItem>();
                return;
            }

            // Modo degradado: si no hay servicio, no hacer nada
            if (_servicioFormaVenta == null)
            {
                Debug.WriteLine("[SelectorFormaVenta] Servicio FormaVenta no disponible (modo degradado)");
                return;
            }

            _estaCargando = true;
            try
            {
                Debug.WriteLine($"[SelectorFormaVenta] Llamando servicio ObtenerFormasVenta...");
                var formasVenta = await _servicioFormaVenta.ObtenerFormasVenta(Empresa);
                Debug.WriteLine($"[SelectorFormaVenta] Servicio retornó {formasVenta?.Count() ?? 0} formas de venta");

                // Aplicar filtro si es necesario
                if (SoloVisiblesPorComerciales)
                {
                    formasVenta = formasVenta.Where(f => f.VisiblePorComerciales);
                    Debug.WriteLine($"[SelectorFormaVenta] Filtradas a {formasVenta.Count()} visibles por comerciales");
                }

                // Añadir item especial para indicar valores diferentes al principio
                var lista = new ObservableCollection<FormaVentaItem>
                {
                    new FormaVentaItem { Numero = VALOR_VARIOS, Descripcion = "(Diferentes formas de venta)" }
                };
                foreach (var fv in formasVenta)
                {
                    lista.Add(fv);
                }

                ListaFormasVenta = lista;
                Debug.WriteLine($"[SelectorFormaVenta] Lista asignada con {lista.Count} elementos");

                // Auto-seleccionar si hay valor pendiente
                AutoSeleccionarFormaVenta();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SelectorFormaVenta] Error al cargar formas de venta: {ex.Message}");
                ListaFormasVenta = new ObservableCollection<FormaVentaItem>();
            }
            finally
            {
                _estaCargando = false;
            }
        }

        #endregion

        #region Lógica de Auto-Selección

        /// <summary>
        /// Auto-selecciona la forma de venta si ya hay una selección válida en la lista.
        /// Usa Trim() para manejar valores con espacios que pueden venir de la BD.
        /// Carlos 09/12/25: Necesario porque la carga async significa que el valor
        /// se establece antes de que la lista esté disponible.
        /// </summary>
        private void AutoSeleccionarFormaVenta()
        {
            if (ListaFormasVenta == null || !ListaFormasVenta.Any())
                return;

            _estaAutoSeleccionando = true;
            try
            {
                // Si ya hay una selección válida en la lista (por string), sincronizar con objeto completo
                if (!string.IsNullOrEmpty(FormaVentaSeleccionada))
                {
                    var seleccionTrim = FormaVentaSeleccionada.Trim();
                    var formaVenta = ListaFormasVenta.FirstOrDefault(f => f.Numero?.Trim() == seleccionTrim);
                    if (formaVenta != null)
                    {
                        Debug.WriteLine($"[SelectorFormaVenta] Selección existente '{FormaVentaSeleccionada}' válida - forzando re-selección con objeto de la lista");
                        // Forzar re-selección con el objeto exacto de la lista para que el ComboBox lo encuentre
                        // Esto es necesario porque WPF ComboBox compara por referencia cuando usa SelectedItem
                        FormaVentaSeleccionadaCompleta = null;
                        FormaVentaSeleccionadaCompleta = formaVenta;
                        return;
                    }
                }

                // Si ya hay selección completa válida, buscar el objeto equivalente en la lista
                if (FormaVentaSeleccionadaCompleta != null)
                {
                    var formaVentaEnLista = ListaFormasVenta.FirstOrDefault(f => f.Numero?.Trim() == FormaVentaSeleccionadaCompleta.Numero?.Trim());
                    if (formaVentaEnLista != null)
                    {
                        Debug.WriteLine($"[SelectorFormaVenta] Selección completa existente '{FormaVentaSeleccionadaCompleta.Numero}' válida - forzando re-selección");
                        // Usar el objeto de la lista, no el original (que puede ser una instancia diferente)
                        FormaVentaSeleccionadaCompleta = null;
                        FormaVentaSeleccionadaCompleta = formaVentaEnLista;
                        return;
                    }
                }

                // No auto-seleccionar por defecto - dejar vacío para que el usuario elija
                Debug.WriteLine($"[SelectorFormaVenta] Sin selección previa, dejando vacío");
            }
            finally
            {
                _estaAutoSeleccionando = false;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
