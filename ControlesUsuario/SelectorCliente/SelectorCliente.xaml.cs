using ControlesUsuario.Models;
using Nesto.Contratos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorCliente.xaml
    /// </summary>
    public partial class SelectorCliente : UserControl, INotifyPropertyChanged
    {
        public SelectorCliente()
        {
            InitializeComponent();
            ControlPrincipal.DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private string vendedor;
        private bool cargando;
        private string empresaPorDefecto = "1";

        #region "Campos de la Vista"
                
        private void brdCliente_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Border controlSender = (Border)sender;
                clienteSeleccionado = (ClienteDTO)controlSender.DataContext;
                filtro = clienteSeleccionado.cliente;
                listaClientes = null;
                txtFiltro.Focus();
            } catch
            {
                filtro = "Desconectado";
            }
            
        }
        private void pnlDatosCliente_MouseUp(object sender, MouseButtonEventArgs e)
        {
            visibilidadSelectorEntrega = visibilidadSelectorEntrega == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }
        private void txtFiltro_GotFocus(object sender, RoutedEventArgs e)
        {
            txtFiltro.SelectAll();
        }

        private void txtFiltro_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement keyboardFocus = Keyboard.FocusedElement as UIElement;
                UIElement originalUIE = keyboardFocus;

                if (keyboardFocus != null)
                {
                    keyboardFocus.MoveFocus(tRequest);
                }

                e.Handled = true;

                //if (originalUIE == Keyboard.FocusedElement)
                //{
                //    BindingExpression exp = this.txtFiltro.GetBindingExpression(TextBox.TextProperty);
                //    exp.UpdateSource();
                //    txtFiltro.Focus();
                //    txtFiltro.SelectAll();
                //}
                
            }
        }

        private void txtFiltro_MouseUp(object sender, MouseButtonEventArgs e)
        {
            txtFiltro.SelectAll();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Empresa != null)
            {
                vendedor = await Configuracion.leerParametro(Empresa, "Vendedor");
            } else
            {
                vendedor = await Configuracion.leerParametro(empresaPorDefecto, "Vendedor");
            }

            

            // Para poner el foco en el primer control
            //TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
            //this.MoveFocus(tRequest);
            txtFiltro.Focus();
        }

        #endregion

        #region "Propiedades"
        private ClienteDTO _clienteSeleccionado;
        public ClienteDTO clienteSeleccionado
        {
            get {
                return _clienteSeleccionado;
            }
            set
            {
                if (_clienteSeleccionado != value)
                {
                    _clienteSeleccionado = value;
                    this.Cliente = value != null ? value.cliente : null;
                    this.Contacto = value != null ? value.contacto : null;
                    this.ClienteCompleto = _clienteSeleccionado;
                    OnPropertyChanged("clienteSeleccionado");
                    OnPropertyChanged("visibilidadDatosCliente");
                }
            }

        }

        private string _contactoSeleccionado;
        public string contactoSeleccionado
        {
            get
            {
                return _contactoSeleccionado;
            }
            set
            {
                _contactoSeleccionado = value;
                OnPropertyChanged("contactoSeleccionado");
                if (clienteSeleccionado != null && contactoSeleccionado != null && clienteSeleccionado.contacto.Trim() != contactoSeleccionado.Trim())
                {
                    clienteSeleccionado.contacto = contactoSeleccionado;
                    cargarCliente();
                }
            }
        }

        private string _filtro;
        public string filtro { 
            get
            {
                return _filtro;
            }
            set
            {
                if (_filtro != value)
                {
                    _filtro = value;
                    OnPropertyChanged("filtro");
                    cargarCliente();
                }
            }

        }

        private ObservableCollection<ClienteDTO> _listaClientes;
        public ObservableCollection<ClienteDTO> listaClientes
        {
            get
            {
                return _listaClientes;
            }
            set
            {
                _listaClientes = value;
                OnPropertyChanged("listaClientes");
                OnPropertyChanged("visibilidadListaClientes");
            }
        }

        public Visibility visibilidadCargando
        {
            get
            {
                if (cargando)
                {
                    return Visibility.Visible;
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }
        }
        public Visibility visibilidadDatosCliente
        {
            get
            {
                if (clienteSeleccionado != null)
                {
                    return Visibility.Visible;
                }else
                {
                    return Visibility.Collapsed;
                }
            }
        }
        public Visibility visibilidadListaClientes
        {
            get
            {
                if (listaClientes == null || listaClientes.Count == 0)
                {
                    return Visibility.Collapsed;
                } else
                {
                    return Visibility.Visible;
                }

            }
        }

        private Visibility _visibilidadSelectorEntrega = Visibility.Collapsed;
        public Visibility visibilidadSelectorEntrega
        {
            get
            {
                return _visibilidadSelectorEntrega;
            }
            set
            {
                _visibilidadSelectorEntrega = value;
                OnPropertyChanged("visibilidadSelectorEntrega");
            }
        }
        #endregion

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
            DependencyProperty.Register("Configuracion", typeof(IConfiguracion),
              typeof(SelectorCliente));


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
            DependencyProperty.Register("Empresa", typeof(string),
              typeof(SelectorCliente),
                new FrameworkPropertyMetadata(new PropertyChangedCallback(OnEmpresaChanged)));

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
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnClienteChanged)));

        private static void OnClienteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;

            if (selector == null)
            {
                return;
            }
            if (selector.Cliente == null)
            {
                selector.clienteSeleccionado = null;
            }
            else if(selector.filtro != selector.Cliente.Trim())
            {
                //if (selector.contactoSeleccionado == null && selector.Contacto != null)
                //{
                //    selector.contactoSeleccionado = selector.Contacto.Trim();
                //}
                selector.filtro = selector.Cliente.Trim();
            }

        }

        /// <summary>
        /// Gets or sets the CLIENTE para las llamadas a la API
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
        /// Identified the SELECCIONADA dependency property
        /// </summary>
        public static readonly DependencyProperty ContactoProperty =
            DependencyProperty.Register("Contacto", typeof(string),
              typeof(SelectorCliente),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnContactoChanged)));

        private static void OnContactoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;

            
            if (selector == null)
            {
                return;
            }
            if (selector.Contacto == null)
            {
                selector.contactoSeleccionado = null;
            }
            else
            {
                if (selector.contactoSeleccionado == null || (selector.contactoSeleccionado.Trim() != selector.Contacto.Trim()))
                {
                    selector.contactoSeleccionado = selector.Contacto.Trim();
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
        /// Identified the EMPRESA dependency property
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
        /// Identified the EMPRESA dependency property
        /// </summary>
        public static readonly DependencyProperty ClienteCompletoProperty =
            DependencyProperty.Register("ClienteCompleto", typeof(ClienteDTO),
              typeof(SelectorCliente),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnClienteCompletoChanged)));

        private static void OnClienteCompletoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorCliente selector = (SelectorCliente)d;


            //if (selector == null)
            //{
            //    return;
            //}
            //if (selector.ClienteCompleto == null)
            //{
            //    return;
            //}
            //else
            //{
            //    if (selector.contactoSeleccionado == null || (selector.contactoSeleccionado.Trim() != selector.Contacto.Trim()))
            //    {
            //        selector.contactoSeleccionado = selector.Contacto.Trim();
            //    }
            //}

        }

        ///// <summary>
        ///// Identified the ClienteCompleto dependency property
        ///// </summary>
        //private static readonly DependencyPropertyKey ClienteCompletoPropertyKey
        //= DependencyProperty.RegisterReadOnly("ClienteCompleto", typeof(ClienteDTO), typeof(SelectorCliente),
        //    new FrameworkPropertyMetadata((ClienteDTO)new ClienteDTO(),
        //        FrameworkPropertyMetadataOptions.None));

        //public static readonly DependencyProperty ClienteCompletoProperty
        //    = ClienteCompletoPropertyKey.DependencyProperty;

        //public ClienteDTO ClienteCompleto
        //{
        //    get { return (ClienteDTO)GetValue(ClienteCompletoProperty); }
        //    protected set { SetValue(ClienteCompletoPropertyKey, value); }
        //}

        #endregion

        #region "Funciones Auxiliares"
        private async Task buscarClientes()
        {
            if (filtro == null || Empresa == null || Configuracion == null)
            {
                return;
            }

            clienteSeleccionado = null;

            if (filtro.Trim() == "")
            {
                return;
            }
            
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(Configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    mostrarCargando(true);
                    string urlConsulta = "Clientes?empresa=" + Empresa+ "&vendedor="+ vendedor + "&filtro=" + filtro;


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaClientes = JsonConvert.DeserializeObject<ObservableCollection<ClienteDTO>>(resultado);
                        if (Cliente != null)
                        {
                            //vendedorSeleccionado = listaVendedores.Where(l => l.vendedor == Seleccionado.Trim()).SingleOrDefault();
                        }
                    }
                }
                catch
                {
                    throw new Exception("No se encontró ningún cliente con el texto " + filtro);
                } finally
                {
                    mostrarCargando(false);
                }
            }
        }
        private async void cargarCliente()
        {
            if (filtro == null || Empresa == null || Configuracion == null)
            {
                return;
            }
            listaClientes = null;
            visibilidadSelectorEntrega = Visibility.Collapsed;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(Configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes?empresa=" + Empresa + "&cliente=" + filtro + "&contacto=" + contactoSeleccionado; //contacto en blanco para que coja clientePrincipal


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        clienteSeleccionado = JsonConvert.DeserializeObject<ClienteDTO>(resultado);
                        if (Cliente != null)
                        {
                            //vendedorSeleccionado = listaVendedores.Where(l => l.vendedor == Seleccionado.Trim()).SingleOrDefault();
                        }
                    }
                    else
                    {
                        await buscarClientes();
                    }
                }
                catch (Exception)
                {
                    await buscarClientes();
                }
            }
        }
        private void mostrarCargando(bool estado)
        {
            cargando = estado;
            OnPropertyChanged("cargando");
        }
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }



        #endregion

        private void txtFiltro_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            txtFiltro.SelectAll();
        }
    }

}
