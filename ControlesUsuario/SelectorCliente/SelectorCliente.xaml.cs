using ControlesUsuario.Models;
using Prism.Regions;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Prism.Ioc;
using ControlesUsuario.ViewModels;
using Nesto.Infrastructure.Shared;
using Prism.Mvvm;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorCliente.xaml
    /// </summary>
    public partial class SelectorCliente : UserControl, INotifyPropertyChanged
    {
        private readonly IRegionManager regionManager;

        public SelectorCliente()
        {
            InitializeComponent();
            PrepararSelectorCliente();
            regionManager = ContainerLocator.Container.Resolve<IRegionManager>();
        }


        private void PrepararSelectorCliente()
        {
            SelectorClienteViewModel vm = DataContext as SelectorClienteViewModel;
            //ControlPrincipal.DataContext = this;
            vm.listaClientes.ElementoSeleccionadoChanging += (sender, args) =>
            {
                //vm.listaClientes.Reiniciar();
            };


            vm.listaClientes.ElementoSeleccionadoChanged += (sender, args) =>
            {
                ClienteCompleto = vm.listaClientes.ElementoSeleccionado as ClienteDTO;
                Cliente = ClienteCompleto?.cliente;
                Contacto = ClienteCompleto?.contacto;
                vm.contactoSeleccionado = ClienteCompleto?.contacto.Trim();
                OnPropertyChanged(string.Empty);
                vm.ActualizarPropertyChanged();
            };
            vm.listaClientes.HayQueCargarDatos += () => { 
                vm.cargarCliente(Empresa, txtFiltro.Text, vm.contactoSeleccionado); 
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        
        

        #region "Campos de la Vista"
                
        private void brdCliente_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SelectorClienteViewModel vm = DataContext as SelectorClienteViewModel;
            try
            {
                Border controlSender = (Border)sender;
                vm.listaClientes.ElementoSeleccionado = (ClienteDTO)controlSender.DataContext;
                vm.listaClientes.Filtro = ClienteCompleto.cliente;
                vm.listaClientes.ListaOriginal?.Clear();
                txtFiltro.Focus();
            } catch
            {
                vm.listaClientes.Filtro = "Desconectado";
            }            
        }
        private void pnlDatosCliente_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SelectorClienteViewModel vm = DataContext as SelectorClienteViewModel;            
            vm.visibilidadSelectorEntrega = vm.visibilidadSelectorEntrega ? false : true;
            if (vm.visibilidadSelectorEntrega)
            {
                selectorEntrega.Focus();
            }
        }
        private void txtFiltro_GotFocus(object sender, RoutedEventArgs e)
        {
            txtFiltro.SelectAll();
        }

        private void txtFiltro_KeyUp(object sender, KeyEventArgs e)
        {
            SelectorClienteViewModel vm = DataContext as SelectorClienteViewModel;
            if (e.Key == Key.Enter)
            {
                if (vm.listaClientes.ListaOriginal == null)
                {
                    vm.listaClientes.FijarFiltroCommand.Execute(txtFiltro.Text);
                } else
                {
                    vm.listaClientes.FijarFiltroCommand.Execute(txtFiltro.Text);
                }
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement keyboardFocus = Keyboard.FocusedElement as UIElement;
                UIElement originalUIE = keyboardFocus;

                if (keyboardFocus != null && ClienteCompleto != null && ClienteCompleto.cliente == txtFiltro.Text)
                {
                    keyboardFocus.MoveFocus(tRequest);
                } else
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

            SelectorClienteViewModel vm = DataContext as SelectorClienteViewModel;
            await vm.CargarVendedor(Empresa);

            // Para poner el foco en el primer control
            //TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
            //this.MoveFocus(tRequest);
            txtFiltro.Focus();
        }

        #endregion

        #region "Propiedades"
        #endregion

        #region Comandos        
        #endregion Comandos

        #region Dependency Properties

        /// <summary>
        /// Gets or sets the EMPRESA para las llamadas a la API
        /// </summary>
        public string Empresa
        {
            get { return (string)GetValue(EmpresaProperty); }
            set
            {
                SetValue(EmpresaProperty, value);
            }
        }

        /// <summary>
        /// Identified the EMPRESA dependency property
        /// </summary>
        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register(nameof(Empresa), typeof(string),
              typeof(SelectorCliente),
                new FrameworkPropertyMetadata(Constantes.Empresas.EMPRESA_DEFECTO, new PropertyChangedCallback(OnEmpresaChanged)));

        private static void OnEmpresaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;
        }


        /// <summary>
        /// Gets or sets the CLIENTE para las llamadas a la API
        /// </summary>
        public string Cliente
        {
            get { return (string)GetValue(ClienteProperty); }
            set
            {
                SetValue(ClienteProperty, value);
            }
        }

        /// <summary>
        /// Identified the SELECCIONADA dependency property
        /// </summary>
        public static readonly DependencyProperty ClienteProperty =
            DependencyProperty.Register("Cliente", typeof(string),
              typeof(SelectorCliente),
              new FrameworkPropertyMetadata(null,
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnClienteChanged)));

        private static void OnClienteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;

            if (selector == null)
            {
                return;
            }
            SelectorClienteViewModel vm = selector.DataContext as SelectorClienteViewModel;
            if (selector.Cliente == null)
            {
                vm.listaClientes.ElementoSeleccionado = null;
            }
            else if(vm.listaClientes.Filtro != selector.Cliente.Trim())
            {
                //if (selector.contactoSeleccionado == null && selector.Contacto != null)
                //{
                //    selector.contactoSeleccionado = selector.Contacto.Trim();
                //}
                selector.txtFiltro.Text = selector.Cliente.Trim();
                vm.listaClientes.Filtro = selector.Cliente.Trim();
            }
            if (selector.ClienteCompleto == null || selector.ClienteCompleto.cliente.Trim() != selector.txtFiltro.Text || selector.ClienteCompleto.contacto != vm.contactoSeleccionado)
            {
                vm.cargarCliente(selector.Empresa, selector.txtFiltro.Text, vm.contactoSeleccionado);
            }
        }

        /// <summary>
        /// Gets or sets the CONTACTO para las llamadas a la API
        /// </summary>
        public string Contacto
        {
            get { return (string)GetValue(ContactoProperty); }
            set
            {
                SetValue(ContactoProperty, value);
            }
        }

        /// <summary>
        /// Identified the CONTACTO dependency property
        /// </summary>
        public static readonly DependencyProperty ContactoProperty =
            DependencyProperty.Register("Contacto", typeof(string),
              typeof(SelectorCliente),
              new FrameworkPropertyMetadata(null, 
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnContactoChanged)));

        private static void OnContactoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;

            
            if (selector == null)
            {
                return;
            }
            SelectorClienteViewModel vm = selector.DataContext as SelectorClienteViewModel;
            if (selector.Contacto == null)
            {
                vm.contactoSeleccionado = null;
            }
            else
            {
                if (vm.contactoSeleccionado == null || (vm.contactoSeleccionado.Trim() != selector.Contacto.Trim()))
                {
                    vm.contactoSeleccionado = selector.Contacto.Trim();
                }
            }

        }

        public string Etiqueta
        {
            get { return (string)GetValue(EtiquetaProperty); }
            set
            {
                SetValue(EtiquetaProperty, value);
            }
        }

        /// <summary>
        /// Identified the ETIQUETA dependency property
        /// </summary>
        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register("Etiqueta", typeof(string),
              typeof(SelectorCliente),
              new UIPropertyMetadata("Seleccione un cliente:"));


        public ClienteDTO ClienteCompleto
        {
            get { return (ClienteDTO)GetValue(ClienteCompletoProperty); }
            set
            {
                SetValue(ClienteCompletoProperty, value);
            }
        }

        /// <summary>
        /// Identified the CLIENTECOMPLETO dependency property
        /// </summary>
        public static readonly DependencyProperty ClienteCompletoProperty =
            DependencyProperty.Register(nameof(ClienteCompleto), typeof(ClienteDTO),
              typeof(SelectorCliente),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnClienteCompletoChanged)));

        private static void OnClienteCompletoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;
        }

        #endregion


        private void txtFiltro_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            txtFiltro.SelectAll();
        }

        private async void itmChips_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            await Task.Delay(200);
            Keyboard.Focus(txtFiltro);
        }

        private void btnButtonEditar_Click(object sender, RoutedEventArgs e)
        {
            var parameters = new NavigationParameters();
            ClienteDTO curItem = ((ListViewItem)lstClientes.ContainerFromElement((Button)sender)).Content as ClienteDTO;
            parameters.Add("empresaParameter", curItem.empresa);
            parameters.Add("clienteParameter", curItem.cliente);
            parameters.Add("contactoParameter", curItem.contacto);
            regionManager.RequestNavigate("MainRegion", "CrearClienteView", parameters);
        }


        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

    }
    public class EstadoColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var estado = (int)value;
            if (estado == -1)
            {
                return Brushes.DarkRed;
            }
            else if (estado == 5)
            {
                return Brushes.Red;
            }
            else if (estado == 7)
            {
                return Brushes.GreenYellow;
            }
            else if (estado == 0 || estado == 9)
            {
                return Brushes.Green;
            }
            else
            {
                return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
