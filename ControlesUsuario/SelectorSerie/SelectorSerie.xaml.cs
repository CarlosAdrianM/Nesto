using ControlesUsuario.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Selector para la serie de facturación del pedido.
    /// Carlos 09/12/25: Issue #245 - Creado siguiendo el patrón de SelectorPeriodoFacturacion.
    /// NO usa DataContext=this para permitir herencia del parent.
    /// </summary>
    public partial class SelectorSerie : UserControl, INotifyPropertyChanged
    {
        public SelectorSerie()
        {
            InitializeComponent();
            // NO establecer DataContext = this
            // Los bindings usan ElementName=Root

            // Cargar las series disponibles (síncrono para que el binding funcione)
            CargarSeries();

            // Sincronizar el ComboBox cuando el control esté completamente cargado
            Loaded += SelectorSerie_Loaded;
        }

        private void SelectorSerie_Loaded(object sender, RoutedEventArgs e)
        {
            // Sincronizar el ComboBox con el valor actual de SerieSeleccionada
            // Esto es necesario porque el binding puede haberse evaluado antes de que
            // el ComboBox estuviera listo
            SincronizarComboBox(SerieSeleccionada);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region Dependency Properties

        /// <summary>
        /// Código de la serie seleccionada (NV, CV, etc.)
        /// </summary>
        public string SerieSeleccionada
        {
            get => (string)GetValue(SerieSeleccionadaProperty);
            set => SetValue(SerieSeleccionadaProperty, value);
        }

        public static readonly DependencyProperty SerieSeleccionadaProperty =
            DependencyProperty.Register(
                nameof(SerieSeleccionada),
                typeof(string),
                typeof(SelectorSerie),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSerieSeleccionadaChanged));

        private static void OnSerieSeleccionadaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorSerie)d;

            // Notificar cambio para que el binding TwoWay funcione
            selector.OnPropertyChanged(nameof(SerieSeleccionada));

            // Forzar sincronización del ComboBox cuando el valor viene del exterior
            selector.SincronizarComboBox(e.NewValue as string);
        }

        /// <summary>
        /// Sincroniza el ComboBox cuando el valor de SerieSeleccionada cambia desde el binding externo.
        /// Usa Trim() para manejar valores con espacios que pueden venir de la BD.
        /// </summary>
        private void SincronizarComboBox(string codigo)
        {
            if (comboSerie == null || ListaSeries == null)
                return;

            // Buscar el item correspondiente al código (con Trim por si viene con espacios de la BD)
            var codigoTrim = codigo?.Trim();
            var item = ListaSeries.FirstOrDefault(s => s.Codigo?.Trim() == codigoTrim);

            if (item != null && comboSerie.SelectedItem != item)
            {
                comboSerie.SelectedItem = item;
            }
        }

        /// <summary>
        /// Etiqueta que se muestra antes del ComboBox
        /// </summary>
        public string Etiqueta
        {
            get => (string)GetValue(EtiquetaProperty);
            set => SetValue(EtiquetaProperty, value);
        }

        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register(
                nameof(Etiqueta),
                typeof(string),
                typeof(SelectorSerie),
                new UIPropertyMetadata("Serie:"));

        /// <summary>
        /// Visibilidad de la etiqueta
        /// </summary>
        public Visibility VisibilidadEtiqueta
        {
            get => (Visibility)GetValue(VisibilidadEtiquetaProperty);
            set => SetValue(VisibilidadEtiquetaProperty, value);
        }

        public static readonly DependencyProperty VisibilidadEtiquetaProperty =
            DependencyProperty.Register(
                nameof(VisibilidadEtiqueta),
                typeof(Visibility),
                typeof(SelectorSerie),
                new UIPropertyMetadata(Visibility.Collapsed));

        #endregion

        #region Propiedades internas

        private ObservableCollection<SerieItem> _listaSeries;
        public ObservableCollection<SerieItem> ListaSeries
        {
            get => _listaSeries;
            set
            {
                if (_listaSeries != value)
                {
                    _listaSeries = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Métodos

        /// <summary>
        /// Carga las series de facturación disponibles
        /// </summary>
        private void CargarSeries()
        {
            ListaSeries = new ObservableCollection<SerieItem>
            {
                new SerieItem { Codigo = "NV", Nombre = "Nueva Visión" },
                new SerieItem { Codigo = "CV", Nombre = "Cursos" },
                new SerieItem { Codigo = "UL", Nombre = "Unión Láser" },
                new SerieItem { Codigo = "VC", Nombre = "Visnú Cosméticos" },
                new SerieItem { Codigo = "DV", Nombre = "Deuda Vencida" }
            };
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
