using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Control para seleccionar un almacén.
    /// Carlos 09/12/25: Issue #253/#52 - Creado siguiendo el patrón de SelectorCCC.
    /// </summary>
    public partial class SelectorAlmacen : UserControl, INotifyPropertyChanged
    {
        private readonly IServicioAlmacenes _servicioAlmacenes;
        private bool _estaCargando = false;
        private bool _estaAutoSeleccionando = false;

        #region Constructores

        /// <summary>
        /// Constructor sin parámetros (usado por XAML).
        /// </summary>
        public SelectorAlmacen()
        {
            InitializeComponent();

            try
            {
                _servicioAlmacenes = ContainerLocator.Container.Resolve<IServicioAlmacenes>();
            }
            catch
            {
                // Se usa solo para poder testar controles que incluyan un SelectorAlmacen
            }

            // Cargar almacenes al inicializar
            CargarAlmacenesAsync();
        }

        /// <summary>
        /// Constructor con DI (PREFERIDO).
        /// </summary>
        public SelectorAlmacen(IServicioAlmacenes servicioAlmacenes)
        {
            InitializeComponent();
            _servicioAlmacenes = servicioAlmacenes ?? throw new ArgumentNullException(nameof(servicioAlmacenes));

            // Cargar almacenes al inicializar
            CargarAlmacenesAsync();
        }

        #endregion

        #region DependencyProperty - Salida (TwoWay)

        public static readonly DependencyProperty AlmacenSeleccionadoProperty =
            DependencyProperty.Register(
                nameof(AlmacenSeleccionado),
                typeof(string),
                typeof(SelectorAlmacen),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnAlmacenSeleccionadoChanged));

        public string AlmacenSeleccionado
        {
            get => (string)GetValue(AlmacenSeleccionadoProperty);
            set => SetValue(AlmacenSeleccionadoProperty, value);
        }

        private static void OnAlmacenSeleccionadoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorAlmacen)d;

            // Comparar valores para evitar propagaciones innecesarias (prevenir bucles)
            if (e.OldValue?.ToString() == e.NewValue?.ToString())
                return;

            // Evitar bucle infinito cuando AutoSeleccionarAlmacen() cambia el valor
            if (selector._estaAutoSeleccionando)
                return;

            Debug.WriteLine($"[SelectorAlmacen] OnAlmacenSeleccionadoChanged: '{e.OldValue}' -> '{e.NewValue}', _estaCargando={selector._estaCargando}");

            // Si no estamos cargando y ya hay lista, intentar auto-seleccionar
            // Esto maneja el caso cuando el valor se establece después de que la lista ya cargó
            if (!selector._estaCargando && selector.ListaAlmacenes != null && selector.ListaAlmacenes.Count > 0)
            {
                selector.AutoSeleccionarAlmacen();
            }

            // Disparar PropertyChanged para que el binding TwoWay funcione
            selector.OnPropertyChanged(nameof(AlmacenSeleccionado));
        }

        #endregion

        #region Propiedades Internas (INotifyPropertyChanged)

        private ObservableCollection<AlmacenItem> _listaAlmacenes;
        public ObservableCollection<AlmacenItem> ListaAlmacenes
        {
            get => _listaAlmacenes;
            set
            {
                if (_listaAlmacenes != value)
                {
                    _listaAlmacenes = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Lógica de Carga

        /// <summary>
        /// Valor especial para indicar que hay diferentes almacenes en las líneas.
        /// Carlos 09/12/25: Debe coincidir con VALOR_VARIOS en DetallePedidoViewModel.
        /// </summary>
        public const string VALOR_VARIOS = "VARIOS";

        /// <summary>
        /// Carga los almacenes desde la API.
        /// </summary>
        private async void CargarAlmacenesAsync()
        {
            Debug.WriteLine($"[SelectorAlmacen] CargarAlmacenesAsync");

            // Modo degradado: si no hay servicio, usar lista hardcodeada
            if (_servicioAlmacenes == null)
            {
                Debug.WriteLine("[SelectorAlmacen] Servicio Almacenes no disponible (modo degradado) - usando lista local");
                ListaAlmacenes = new ObservableCollection<AlmacenItem>
                {
                    // El item VARIOS se muestra en gris/cursiva (EsFicticio = true)
                    new AlmacenItem { Codigo = VALOR_VARIOS, Nombre = "(Diferentes almacenes)", EsFicticio = true },
                    new AlmacenItem { Codigo = "ALG", Nombre = "Algete", EsFicticio = false, PermiteNegativo = false },
                    new AlmacenItem { Codigo = "REI", Nombre = "Reina", EsFicticio = false, PermiteNegativo = false },
                    new AlmacenItem { Codigo = "ALC", Nombre = "Alcobendas", EsFicticio = false, PermiteNegativo = false }
                };
                AutoSeleccionarAlmacen();
                return;
            }

            _estaCargando = true;
            try
            {
                Debug.WriteLine($"[SelectorAlmacen] Llamando servicio ObtenerAlmacenes...");
                var almacenes = await _servicioAlmacenes.ObtenerAlmacenes();
                Debug.WriteLine($"[SelectorAlmacen] Servicio retornó {almacenes?.Count ?? 0} almacenes");

                var lista = new ObservableCollection<AlmacenItem>
                {
                    // Añadir item especial para indicar valores diferentes (se muestra en gris/cursiva)
                    new AlmacenItem { Codigo = VALOR_VARIOS, Nombre = "(Diferentes almacenes)", EsFicticio = true }
                };

                foreach (var almacen in almacenes)
                {
                    lista.Add(almacen);
                }

                ListaAlmacenes = lista;
                Debug.WriteLine($"[SelectorAlmacen] Lista asignada con {lista.Count} elementos");

                // Auto-seleccionar si hay valor pendiente
                AutoSeleccionarAlmacen();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SelectorAlmacen] Error al cargar almacenes: {ex.Message}");

                // Lista por defecto en caso de error
                ListaAlmacenes = new ObservableCollection<AlmacenItem>
                {
                    new AlmacenItem { Codigo = VALOR_VARIOS, Nombre = "(Diferentes almacenes)", EsFicticio = true },
                    new AlmacenItem { Codigo = "ALG", Nombre = "Algete", EsFicticio = false, PermiteNegativo = false },
                    new AlmacenItem { Codigo = "REI", Nombre = "Reina", EsFicticio = false, PermiteNegativo = false },
                    new AlmacenItem { Codigo = "ALC", Nombre = "Alcobendas", EsFicticio = false, PermiteNegativo = false }
                };
                AutoSeleccionarAlmacen();
            }
            finally
            {
                _estaCargando = false;
            }
        }

        #endregion

        #region Lógica de Auto-Selección

        /// <summary>
        /// Auto-selecciona el almacén si ya hay una selección válida en la lista.
        /// Usa Trim() para manejar valores con espacios que pueden venir de la BD.
        /// Carlos 09/12/25: Necesario porque la carga async significa que el valor
        /// se establece antes de que la lista esté disponible.
        /// </summary>
        private void AutoSeleccionarAlmacen()
        {
            if (ListaAlmacenes == null || ListaAlmacenes.Count == 0)
                return;

            // Si ya hay una selección, verificar que existe en la lista (con Trim)
            if (!string.IsNullOrEmpty(AlmacenSeleccionado))
            {
                var seleccionTrim = AlmacenSeleccionado.Trim();
                var almacen = ListaAlmacenes.FirstOrDefault(a => a.Codigo?.Trim() == seleccionTrim);
                if (almacen != null)
                {
                    Debug.WriteLine($"[SelectorAlmacen] Selección existente '{AlmacenSeleccionado}' válida - forzando re-selección con código exacto '{almacen.Codigo}'");
                    // Forzar re-selección con el código exacto de la lista para que el ComboBox lo encuentre
                    // Esto es necesario porque WPF ComboBox no re-evalúa SelectedValue cuando ItemsSource cambia
                    _estaAutoSeleccionando = true;
                    try
                    {
                        AlmacenSeleccionado = null;
                        AlmacenSeleccionado = almacen.Codigo;
                    }
                    finally
                    {
                        _estaAutoSeleccionando = false;
                    }
                    return;
                }
                else
                {
                    Debug.WriteLine($"[SelectorAlmacen] Selección '{AlmacenSeleccionado}' no encontrada en lista");
                }
            }

            // No auto-seleccionar por defecto - dejar vacío para que el usuario elija
            Debug.WriteLine($"[SelectorAlmacen] Sin selección previa válida, dejando vacío");
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
