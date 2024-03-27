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
using System.Linq;
using Nesto.Infrastructure.Contracts;
using System.Windows.Threading;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorCliente.xaml
    /// </summary>
    public partial class SelectorCliente : UserControl, INotifyPropertyChanged
    {
        private readonly IRegionManager regionManager;
        private DispatcherTimer timer;
        private SelectorClienteViewModel vm;

        public SelectorCliente()
        {
            InitializeComponent();
            PrepararSelectorCliente();
            regionManager = ContainerLocator.Container.Resolve<IRegionManager>();
        }

        public SelectorCliente(SelectorClienteViewModel vm, IRegionManager regionManager)
        {
            // Este constructor se usa únicamente para poder hacer tests
            InitializeComponent();
            DataContext = vm;
            PrepararSelectorCliente();
            this.regionManager = regionManager;
        }


        private void PrepararSelectorCliente()
        {
            vm = DataContext as SelectorClienteViewModel;
            //ControlPrincipal.DataContext = this;
            
            vm.listaClientes.ElementoSeleccionadoChanging += (sender, args) =>
            {
                //vm.listaClientes.Reiniciar();
                //EstaCambiandoDeCliente = !ReferenceEquals(args.NewValue, args.OldValue);                
            };


            vm.listaClientes.ElementoSeleccionadoChanged += (sender, args) =>
            {
                ClienteDTO oldCliente = args.OldValue as ClienteDTO;
                ClienteDTO newCliente = args.NewValue as ClienteDTO;
                if (vm.listaClientes is not null && vm.listaClientes is not null && ClienteCompleto != vm.listaClientes.ElementoSeleccionado)
                {
                    selectorCliente.SetValue(ClienteCompletoProperty, vm.listaClientes.ElementoSeleccionado as ClienteDTO);
                }
                if (Cliente != newCliente?.cliente)
                {
                    selectorCliente.SetValue(ClienteProperty, newCliente?.cliente);
                }
                if (Contacto != newCliente?.contacto)
                {
                    selectorCliente.SetValue(ContactoProperty, newCliente?.contacto);
                }

                OnPropertyChanged(string.Empty);
                vm.ActualizarPropertyChanged();

                // La propiedad de dependencia Configuracion la usamos solo para los test
                if (Configuracion is null && vm.Configuracion is not null)
                {
                    Configuracion = vm.Configuracion;
                }
                if (vm.Configuracion is null && Configuracion is not null)
                {
                    vm.Configuracion = Configuracion;
                }
            };
            vm.listaClientes.HayQueCargarDatos += () => {
                // Por aquí entra cuando buscamos un cliente en el selector directamente
                vm.cargarCliente(Empresa, txtFiltro.Text, null);
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
        // Propiedad pública para acceder a SelectorDireccionEntrega (para tests)
        public SelectorDireccionEntrega ControlSelectorDireccionEntrega => selectorEntrega; 
        //public bool EstaCambiandoDeCliente { get; set; }
        #endregion

        #region Comandos        
        #endregion Comandos

        #region Dependency Properties

        /// <summary>
        /// Gets or sets the Configuracion para las llamadas a la API
        /// </summary>
        public IConfiguracion Configuracion
        {
            get { return (IConfiguracion)GetValue(ConfiguracionProperty); }
            set
            {
                SetValue(ConfiguracionProperty, value);
            }
        }

        /// <summary>
        /// Identified the Configuracion dependency property
        /// </summary>
        public static readonly DependencyProperty ConfiguracionProperty =
            DependencyProperty.Register(nameof(Configuracion), typeof(IConfiguracion),
              typeof(SelectorCliente),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnConfiguracionChanged)));

        private static void OnConfiguracionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;
        }

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
        /// Identified the CLIENTE dependency property
        /// </summary>
        public static readonly DependencyProperty ClienteProperty =
            DependencyProperty.Register(nameof(Cliente), typeof(string),
              typeof(SelectorCliente),
              new FrameworkPropertyMetadata(null,
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnClienteChanged)));

        private static void OnClienteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;
            if (selector == null || e.NewValue == e.OldValue)
            {
                return;
            }

            SelectorClienteViewModel vm = selector.DataContext as SelectorClienteViewModel;
            
            string newValue = (string)e.NewValue;
            if (newValue != null && newValue != newValue.Trim())
            {
                newValue = newValue.Trim(); // Aplica Trim() al nuevo valor
                selector.SetValue(ClienteProperty, newValue);
            }            

            
            if (selector.Cliente == null)
            {
                vm.listaClientes.ElementoSeleccionado = null;
            }
            else if (vm.listaClientes.Filtro != selector.Cliente.Trim())
            {
                //if (selector.contactoSeleccionado == null && selector.Contacto != null)
                //{
                //    selector.contactoSeleccionado = selector.Contacto.Trim();
                //}
                selector.txtFiltro.Text = selector.Cliente.Trim();
                vm.listaClientes.Filtro = selector.Cliente.Trim();
            };
            //if (selector.ClienteCompleto == null || selector.ClienteCompleto.cliente.Trim() != selector.txtFiltro.Text || selector.ClienteCompleto.contacto != vm.contactoSeleccionado) // ¿e.NewValue != e.OldValue?
            //vm.cargarCliente(selector.Empresa, selector.txtFiltro.Text, vm.contactoSeleccionado);
            // Si se cambia el cliente, se vuelve a poner el contacto por defecto. Si se quiere poner otro contacto, se puede hacer sin cambiar el cliente --> NOOOOOOO
            // No se puede poner el contacto por defecto, porque al cambiar de pedido, se cambian a la vez cliente y contacto y no hay que poner el contacto por defecto, sino el del pedido
            //if (selector.ClienteCompleto == null || e.NewValue != e.OldValue || selector.ClienteCompleto.contacto != selector.Contacto) // ¿e.NewValue != e.OldValue?{

            if (selector.Contacto == (vm.listaClientes?.ElementoSeleccionado as ClienteDTO)?.contacto &&
                selector.Cliente  == (vm.listaClientes?.ElementoSeleccionado as ClienteDTO)?.cliente)
            {
                return;
            }

            //if (selector.Contacto != (vm.listaClientes?.ElementoSeleccionado as ClienteDTO)?.contacto ||
            //    selector.Cliente  != (vm.listaClientes?.ElementoSeleccionado as ClienteDTO)?.cliente)
            //if (selector._haPulsadoIntro)
            //{
            //    //vm.cargarCliente(selector.Empresa, selector.Cliente, null);
            //    selector.Contacto = null;
            //    selector.ResetTimer();
            //}
            //else
            //{
            //    selector.ResetTimer();
            //    //vm.cargarCliente(selector.Empresa, selector.Cliente, selector.Contacto);
            //}
            selector.ResetTimer();

            //if (selector.EstaCambiandoDeCliente)
            //{

            //}
            //else
            //{
            //    vm.cargarCliente(selector.Empresa, selector.txtFiltro.Text, selector.Contacto);
            //}


            //if (selector.Contacto != vm.contactoSeleccionado) // Aquí es donde creo que falla, porque ya debería haber cambiado el elemento, pero actualiza en el anterior
            //{
            //    selector.Contacto = vm.contactoSeleccionado;
            //}

            //selector.EstaCambiandoDeCliente = false;
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
            DependencyProperty.Register(nameof(Contacto), typeof(string),
              typeof(SelectorCliente),
              new FrameworkPropertyMetadata(null, 
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnContactoChanged)));

        private static void OnContactoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;            
            if (selector is null || selector.Empresa is null || selector.Cliente is null)
            {
                return;
            }

            SelectorClienteViewModel vm = selector.DataContext as SelectorClienteViewModel;

            string newValue = (string)e.NewValue;
            if (newValue != null && newValue.Trim() != newValue)
            {
                newValue = newValue.Trim(); // Aplica Trim() al nuevo valor
                selector.SetValue(ContactoProperty, newValue);
            }            

            selector.ResetTimer();
        }

        /// <summary>
        /// Gets or sets the ETIQUETA para las llamadas a la API
        /// </summary>
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

        /// <summary>
        /// Gets or sets the ClienteCompleto para las llamadas a la API
        /// </summary>
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
            //if (selector is not null && selector.ClienteCompleto is not null && selector.ClienteCompleto.cliente != selector.Cliente)
            //{
            //    selector.SetValue(ClienteProperty, selector.ClienteCompleto.cliente);
            //}
            //if (selector is not null && selector.ClienteCompleto is not null && selector.ClienteCompleto.contacto != selector.Contacto)
            //{
            //    selector.SetValue(ContactoProperty, selector.ClienteCompleto.contacto);
            //}
        }

        #endregion

        private void ResetTimer()
        {
            if (timer == null)
            {
                timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, TimerElapsed, Dispatcher);
            }
            else
            {
                timer.Stop();
                timer.Start();
            }
        }

        private void TimerElapsed(object sender, EventArgs e)
        {
            // Por aquí entra cuando se cambian el Cliente y Contacto seleccionados por Binding
            timer.Stop();
            vm.cargarCliente(selectorCliente.Empresa, selectorCliente.Cliente, selectorCliente.Contacto);
        }


        public void ActualizarPropertyChanged()
        {
            OnPropertyChanged(string.Empty);
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

        private void btnButtonEditar_Click(object sender, RoutedEventArgs e)
        {
            var parameters = new NavigationParameters();
            ClienteDTO curItem = ((ListViewItem)lstClientes.ContainerFromElement((Button)sender)).Content as ClienteDTO;
            parameters.Add("empresaParameter", curItem.empresa);
            parameters.Add("clienteParameter", curItem.cliente);
            parameters.Add("contactoParameter", curItem.contacto);
            regionManager.RequestNavigate("MainRegion", "CrearClienteView", parameters);
        }

        public int NumeroDeDirecciones() => selectorEntrega.listaDireccionesEntrega is null || selectorEntrega.listaDireccionesEntrega.Lista is null ? 
            0 : 
            selectorEntrega.listaDireccionesEntrega.Lista.Count;

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
