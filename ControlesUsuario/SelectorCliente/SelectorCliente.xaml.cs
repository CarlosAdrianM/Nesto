using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

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
            listaClientes = new();
            listaClientes.ElementoSeleccionadoChanged += (sender, args) => {
                ClienteCompleto = listaClientes.ElementoSeleccionado as ClienteDTO;
                Cliente = ClienteCompleto?.cliente;
                Contacto = ClienteCompleto?.contacto;
                
                OnPropertyChanged(nameof(visibilidadDatosCliente));
                listaClientes.ListaOriginal = null;
            };
            listaClientes.HayQueCargarDatos += () => { cargarCliente(); };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private string vendedor;
        private string empresaPorDefecto = "1";

        #region "Campos de la Vista"
                
        private void brdCliente_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Border controlSender = (Border)sender;
                listaClientes.ElementoSeleccionado = (ClienteDTO)controlSender.DataContext;
                listaClientes.Filtro = ClienteCompleto.cliente;
                listaClientes.ListaOriginal?.Clear();
                txtFiltro.Focus();
            } catch
            {
                listaClientes.Filtro = "Desconectado";
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
                //if (string.IsNullOrEmpty(listaClientes.Filtro))
                //{
                //    listaClientes.ListaOriginal = null;
                //    //listaClientes.Lista = null;
                //    listaClientes.ElementoSeleccionado = null;
                //    return;
                //}
                if (listaClientes.ListaOriginal == null)
                {
                    //cargarCliente();
                    listaClientes.FijarFiltroCommand.Execute(txtFiltro.Text);
                } else
                {
                    //listaClientes.ListaOriginal = listaClientes.Lista;
                    listaClientes.FijarFiltroCommand.Execute(txtFiltro.Text);
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
        private bool _cargando;
        public bool cargando
        {
            get { return _cargando; }
            set { 
                if (_cargando != value)
                {
                    _cargando = value;
                    OnPropertyChanged("cargando");
                    OnPropertyChanged(nameof(visibilidadCargando));
                }
            }
        }
        //private ClienteDTO _clienteSeleccionado;
        //public ClienteDTO clienteSeleccionado
        //{
        //    get {
        //        return _clienteSeleccionado;
        //    }
        //    set
        //    {
        //        if (_clienteSeleccionado != value)
        //        {
        //            _clienteSeleccionado = value;
        //            Cliente = value?.cliente;
        //            Contacto = value?.contacto;
        //            ClienteCompleto = _clienteSeleccionado;
        //            OnPropertyChanged(nameof(clienteSeleccionado));
        //            OnPropertyChanged(nameof(visibilidadDatosCliente));
        //            listaClientes.ListaOriginal = null;
        //        }
        //    }

        //}

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
                if (ClienteCompleto != null && contactoSeleccionado != null && ClienteCompleto.contacto.Trim() != contactoSeleccionado.Trim())
                {
                    ClienteCompleto.contacto = contactoSeleccionado;
                    cargarCliente();
                }
            }
        }

        //private string _filtro;
        //public string filtro { 
        //    get
        //    {
        //        return _filtro;
        //    }
        //    set
        //    {
        //        if (_filtro != value)
        //        {
        //            _filtro = value.ToLower();
        //            OnPropertyChanged("filtro");
        //            if (listaClientes != null && listaClientes.Any())
        //            {
        //                listaClientes = new ObservableCollection<ClienteDTO>(_listaClienteOriginal.Where(l =>
        //                    (
        //                    (l.nombre != null && l.nombre.ToLower().Contains(filtro)) ||
        //                    (l.direccion != null && l.direccion.ToLower().Contains(filtro)) ||
        //                    (l.telefono != null && l.telefono.ToLower().Contains(filtro)) ||
        //                    (l.poblacion != null && l.poblacion.ToLower().Contains(filtro))
        //                    )
        //                ));
        //            }
        //        }
        //    }
        //}

        private ColeccionFiltrable _listaClientes;
        public ColeccionFiltrable listaClientes
        {
            get
            {
                return _listaClientes;
            }
            set
            {
                _listaClientes = value;
                OnPropertyChanged(nameof(listaClientes));
                OnPropertyChanged(nameof(visibilidadListaClientes));
            }
        }
        //private ObservableCollection<ClienteDTO> _listaClienteOriginal;

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
                if (listaClientes.ElementoSeleccionado != null)
                {
                    return Visibility.Visible;
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }
        }
        public Visibility visibilidadListaClientes
        {
            get
            {
                if (listaClientes == null || listaClientes.Lista == null || !listaClientes.Lista.Any())
                {
                    return Visibility.Collapsed;
                }
                else
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
                OnPropertyChanged(nameof(visibilidadSelectorEntrega));
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
                selector.listaClientes.ElementoSeleccionado = null;
            }
            else if(selector.listaClientes.Filtro != selector.Cliente.Trim())
            {
                //if (selector.contactoSeleccionado == null && selector.Contacto != null)
                //{
                //    selector.contactoSeleccionado = selector.Contacto.Trim();
                //}
                selector.listaClientes.Filtro = selector.Cliente.Trim();
            }
            if (selector.ClienteCompleto == null)
            {
                selector.cargarCliente();
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
            if (listaClientes == null || Empresa == null || Configuracion == null)
            {
                return;
            }

            //listaClientes.ElementoSeleccionado = null;

            if (string.IsNullOrEmpty(txtFiltro.Text))
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
                    string urlConsulta = "Clientes?empresa=" + Empresa+ "&vendedor="+ vendedor + "&filtro=" + txtFiltro.Text;


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaClientes.ListaOriginal = new ObservableCollection<IFiltrableItem>(JsonConvert.DeserializeObject<ObservableCollection<ClienteDTO>>(resultado));
                        OnPropertyChanged(nameof(visibilidadListaClientes));
                        //listaClientes = _listaClienteOriginal;
                        if (Cliente != null)
                        {
                            //vendedorSeleccionado = listaVendedores.Where(l => l.vendedor == Seleccionado.Trim()).SingleOrDefault();
                        }
                    }
                    else
                    {
                        if (listaClientes.ListaOriginal == null || !listaClientes.ListaOriginal.Any())
                        {
                            listaClientes.FiltrosPuestos.Clear();
                        }
                    }
                }
                catch
                {
                    throw new Exception("No se encontró ningún cliente con el texto " + listaClientes.Filtro);
                } finally
                {
                    mostrarCargando(false);
                }
            }
        }

        private async void cargarCliente()
        {
            if (txtFiltro.Text == null || Empresa == null || Configuracion == null)
            {
                return;
            }
            listaClientes.Lista = null;
            visibilidadSelectorEntrega = Visibility.Collapsed;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(Configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes?empresa=" + Empresa + "&cliente=" + txtFiltro.Text + "&contacto=" + contactoSeleccionado; //contacto en blanco para que coja clientePrincipal

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaClientes.ElementoSeleccionado = JsonConvert.DeserializeObject<ClienteDTO>(resultado);
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

        private async void itmChips_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            await Task.Delay(200);
            Keyboard.Focus(txtFiltro);
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
