using Nesto.Infrastructure.Shared;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para BarraFiltro.xaml
    /// </summary>
    public partial class BarraFiltro : UserControl, INotifyPropertyChanged
    {
        public BarraFiltro()
        {
            InitializeComponent();
            ControlPrincipal.DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string Text
        { 
            get => txtFiltro.Text;
            set => txtFiltro.Text = value;
        }

        #region "Campos de la Vista"

        private void txtFiltro_GotFocus(object sender, RoutedEventArgs e)
        {
            txtFiltro.SelectAll();
        }

        private void txtFiltro_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ListaItems.ListaOriginal == null)
                {
                    ListaItems.FijarFiltroCommand.Execute(txtFiltro.Text);
                }
                else
                {
                    ListaItems.FijarFiltroCommand.Execute(txtFiltro.Text);
                }
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement keyboardFocus = Keyboard.FocusedElement as UIElement;
                UIElement originalUIE = keyboardFocus;

                if (keyboardFocus != null && ListaItems.ElementoSeleccionado != null)
                {
                    keyboardFocus.MoveFocus(tRequest);
                    txtFiltro.Focus();
                }
                else
                {
                    txtFiltro.SelectAll();
                }

                e.Handled = true;
            }
        }

        private void txtFiltro_MouseUp(object sender, MouseButtonEventArgs e)
        {
            txtFiltro.SelectAll();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ListaItems.ElementoSeleccionadoChanged += (sender, args) => {
                ItemSeleccionado = ListaItems.ElementoSeleccionado;
            };
            /*
            ListaItems.HayQueCargarDatos += () => { };
            */
            // Para poner el foco en el primer control
            //TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
            //this.MoveFocus(tRequest);
            txtFiltro.Focus();
            Focusable = true;
        }

        private void txtFiltro_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            txtFiltro.SelectAll();
        }

        private async void itmChips_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            await Task.Delay(200);
            Keyboard.Focus(txtFiltro);
        }


        #endregion

        #region "Propiedades"
        private bool _cargando;
        public bool Cargando
        {
            get => _cargando; 
            set { 
                if (_cargando != value)
                {
                    _cargando = value;
                    OnPropertyChanged(nameof(Cargando));
                    OnPropertyChanged(nameof(VisibilidadCargando));
                }
            }
        }

        public Visibility VisibilidadCargando
        {
            get => Cargando ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Dependency Properties

        /// <summary>
        /// Gets or sets the ListaItems 
        /// </summary>
        public ColeccionFiltrable ListaItems
        {
            get => (ColeccionFiltrable)GetValue(ListaItemsProperty);
            set => SetValue(ListaItemsProperty, value);
        }

        /// <summary>
        /// Identified the ListaItems dependency property
        /// </summary>
        public static readonly DependencyProperty ListaItemsProperty =
            DependencyProperty.Register("ListaItems", typeof(ColeccionFiltrable),
              typeof(BarraFiltro),
                new FrameworkPropertyMetadata(new PropertyChangedCallback(OnListaItemsChanged)));

        private static void OnListaItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            BarraFiltro barra = (BarraFiltro)d;
        }


        public string Etiqueta
        {
            get => (string)GetValue(EtiquetaProperty);
            set => SetValue(EtiquetaProperty, value);
        }

        /// <summary>
        /// Identified the EMPRESA dependency property
        /// </summary>
        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register("Etiqueta", typeof(string),
              typeof(BarraFiltro),
              new UIPropertyMetadata("Seleccione un elemento:"));


        public object ItemSeleccionado
        {
            get { return (object)GetValue(ItemSeleccionadoProperty); }
            set
            {
                SetValue(ItemSeleccionadoProperty, value);
            }
        }

        /// <summary>
        /// Identified the EMPRESA dependency property
        /// </summary>
        public static readonly DependencyProperty ItemSeleccionadoProperty =
            DependencyProperty.Register("ItemSeleccionado", typeof(object),
              typeof(BarraFiltro),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnItemSeleccionadoChanged)));

        private static void OnItemSeleccionadoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            
        }


        #endregion
        #region "Funciones Auxiliares"
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public void SelectAll()
        {
            txtFiltro.SelectAll();
        }

        #endregion

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            txtFiltro.Focus();
            Keyboard.Focus(txtFiltro);
        }
    }

    public class FiltroColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string valor = value as string;

            if (valor.StartsWith("-"))
            {
                return Brushes.LightCoral;
            }
            else if (valor.Contains("|"))
            {
                return Brushes.LightGoldenrodYellow;
            }
            else if (valor.StartsWith("*") || valor.EndsWith("*"))
            {
                return Brushes.LightSeaGreen;
            }
            else
            {
                return Brushes.LightGray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
