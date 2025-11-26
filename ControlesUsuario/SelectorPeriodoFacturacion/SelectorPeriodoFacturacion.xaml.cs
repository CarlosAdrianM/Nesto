using Nesto.Infrastructure.Shared;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Selector para el periodo de facturación del pedido.
    /// Carlos 26/11/24: Creado siguiendo el patrón de SelectorCCC con ElementName bindings.
    /// NO usa DataContext=this para permitir herencia del parent.
    /// </summary>
    public partial class SelectorPeriodoFacturacion : UserControl, INotifyPropertyChanged
    {
        public SelectorPeriodoFacturacion()
        {
            InitializeComponent();
            // NO establecer DataContext = this
            // Los bindings usan ElementName=Root

            // Cargar los periodos disponibles
            CargarPeriodos();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region Modelo interno

        /// <summary>
        /// Modelo para representar un periodo de facturación
        /// </summary>
        public class PeriodoFacturacionItem
        {
            public string Codigo { get; set; }
            public string Descripcion { get; set; }
        }

        #endregion

        #region Dependency Properties

        /// <summary>
        /// Código del periodo seleccionado (NRM, FDM, etc.)
        /// </summary>
        public string Seleccionado
        {
            get => (string)GetValue(SeleccionadoProperty);
            set => SetValue(SeleccionadoProperty, value);
        }

        public static readonly DependencyProperty SeleccionadoProperty =
            DependencyProperty.Register(
                nameof(Seleccionado),
                typeof(string),
                typeof(SelectorPeriodoFacturacion),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSeleccionadoChanged));

        private static void OnSeleccionadoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorPeriodoFacturacion)d;
            // Notificar cambio para que el binding TwoWay funcione
            selector.OnPropertyChanged(nameof(Seleccionado));
        }

        /// <summary>
        /// Etiqueta que se muestra encima del ComboBox
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
                typeof(SelectorPeriodoFacturacion),
                new UIPropertyMetadata("Periodo de Facturación:"));

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
                typeof(SelectorPeriodoFacturacion),
                new UIPropertyMetadata(Visibility.Visible));

        #endregion

        #region Propiedades internas

        private ObservableCollection<PeriodoFacturacionItem> _listaPeriodosFacturacion;
        public ObservableCollection<PeriodoFacturacionItem> ListaPeriodosFacturacion
        {
            get => _listaPeriodosFacturacion;
            set
            {
                if (_listaPeriodosFacturacion != value)
                {
                    _listaPeriodosFacturacion = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Métodos

        /// <summary>
        /// Carga los periodos de facturación disponibles
        /// </summary>
        private void CargarPeriodos()
        {
            ListaPeriodosFacturacion = new ObservableCollection<PeriodoFacturacionItem>
            {
                new PeriodoFacturacionItem
                {
                    Codigo = Constantes.PeriodosFacturacion.NORMAL,
                    Descripcion = "Normal"
                },
                new PeriodoFacturacionItem
                {
                    Codigo = Constantes.PeriodosFacturacion.FIN_DE_MES,
                    Descripcion = "Fin de Mes"
                }
            };
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
