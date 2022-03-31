using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Prism.Ioc;
using Prism.Events;
using Nesto.Infrastructure.Events;
using Nesto.Models.Nesto.Models;
using System.Threading.Tasks;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorDireccionEntrega.xaml
    /// </summary>
    public partial class SelectorDireccionEntrega : UserControl, INotifyPropertyChanged
    {
        private readonly IRegionManager regionManager;
        private readonly IEventAggregator eventAggregator;

        public SelectorDireccionEntrega()
        {
            InitializeComponent();

            GridPrincipal.DataContext = this;

            regionManager = ContainerLocator.Container.Resolve<IRegionManager>();
            eventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();
        }

        private async void OnClienteCreado(Clientes clienteCreado)
        {
            if (Empresa == null)
            {
                Empresa = clienteCreado.Empresa.Trim();
            }
            if (Cliente == null)
            {
                Cliente = clienteCreado.Nº_Cliente.Trim();
            }
            await cargarDatos();
            direccionEntregaSeleccionada = listaDireccionesEntrega.Where(l => l.contacto == clienteCreado.Contacto.Trim()).Single();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region Dependency Properties

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
            DependencyProperty.Register("Cliente", typeof(string),
              typeof(SelectorDireccionEntrega),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnClienteChanged)));

        private static void OnClienteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorDireccionEntrega selector = (SelectorDireccionEntrega)d;
            selector.cargarDatos();
        }


        /// <summary>
        /// Gets or sets the Configuracion para las llamadas a la API
        /// </summary>
        public IConfiguracion Configuracion
        {
            get { return (IConfiguracion)GetValue(ConfiguracionProperty); }
            set {
                SetValue(ConfiguracionProperty, value);
            }
        }

        /// <summary>
        /// Identified the Configuracion dependency property
        /// </summary>
        public static readonly DependencyProperty ConfiguracionProperty =
            DependencyProperty.Register("Configuracion", typeof(IConfiguracion),
              typeof(SelectorDireccionEntrega),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnConfiguracionChanged)));

        private static void OnConfiguracionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorDireccionEntrega selector = (SelectorDireccionEntrega)d;
            if (selector != null && selector.Configuracion != null)
            {
                selector.cargarDatos();
            }
        }


        /// <summary>
        /// Gets or sets the EMPRESA para las llamadas a la API
        /// </summary>
        public DireccionesEntregaCliente DireccionCompleta
        {
            get { return (DireccionesEntregaCliente)GetValue(DireccionCompletaProperty); }
            set
            {
                SetValue(DireccionCompletaProperty, value);
            }
        }

        /// <summary>
        /// Identified the EMPRESA dependency property
        /// </summary>
        public static readonly DependencyProperty DireccionCompletaProperty =
            DependencyProperty.Register(nameof(DireccionCompleta), typeof(DireccionesEntregaCliente),
              typeof(SelectorDireccionEntrega),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnDireccionCompletaChanged)));

        private static void OnDireccionCompletaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
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
            DependencyProperty.Register("Empresa", typeof(string),
              typeof(SelectorDireccionEntrega),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnEmpresaChanged)));

        private static void OnEmpresaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorDireccionEntrega selector = (SelectorDireccionEntrega)d;
            if (selector != null && selector.Empresa != null)
            {
                selector.cargarDatos();
            }
        }


        /// <summary>
        /// Gets or sets the SELECCIONADA para las llamadas a la API
        /// </summary>
        public string Seleccionada
        {
            get { return (string)GetValue(SeleccionadaProperty); }
            set
            {
                SetValue(SeleccionadaProperty, value);
            }
        }

        /// <summary>
        /// Identified the SELECCIONADA dependency property
        /// </summary>
        public static readonly DependencyProperty SeleccionadaProperty =
            DependencyProperty.Register("Seleccionada", typeof(string),
              typeof(SelectorDireccionEntrega));
        

        #endregion

        #region "Propiedades"
        private DireccionesEntregaCliente _direccionEntregaSeleccionada;
        public DireccionesEntregaCliente direccionEntregaSeleccionada {
            get
            {
                return _direccionEntregaSeleccionada;
            }

            set
            {
                _direccionEntregaSeleccionada = value;
                OnPropertyChanged("direccionEntregaSeleccionada");
                if (direccionEntregaSeleccionada != null)
                {
                    Seleccionada = direccionEntregaSeleccionada.contacto;
                }
                DireccionCompleta = direccionEntregaSeleccionada;
            }
        }
        private ObservableCollection<DireccionesEntregaCliente> _listaDireccionesEntrega;
        public ObservableCollection<DireccionesEntregaCliente> listaDireccionesEntrega {
            get
            {
                return _listaDireccionesEntrega;
            }
            set
            {
                _listaDireccionesEntrega = value;
                OnPropertyChanged("listaDireccionesEntrega");
            }
        }

        #endregion

        #region "Funciones Auxiliares"
        private async Task cargarDatos()
        {
            if (Configuracion == null || Empresa == null || Cliente == null)
            {
                return;
            }
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(Configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    response = await client.GetAsync("PlantillaVentas/DireccionesEntrega?empresa=" + Empresa + "&clienteDirecciones=" + Cliente);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaDireccionesEntrega = JsonConvert.DeserializeObject<ObservableCollection<DireccionesEntregaCliente>>(resultado); 
                        if (direccionEntregaSeleccionada == null && Seleccionada != null)
                        {
                            direccionEntregaSeleccionada = listaDireccionesEntrega.Where(l => l.contacto == Seleccionada).SingleOrDefault();
                        }
                        if (direccionEntregaSeleccionada == null && Seleccionada == null)
                        {
                            direccionEntregaSeleccionada = listaDireccionesEntrega.Where(l => l.esDireccionPorDefecto).SingleOrDefault();
                        }
                    }
                } catch
                {
                    throw new Exception("No se pudieron leer las direcciones de entrega");
                }
            }
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

        private void btnButtonEditar_Click(object sender, RoutedEventArgs e)
        {
            var parameters = new NavigationParameters();
            DireccionesEntregaCliente curItem = ((ListViewItem)lstDirecciones.ContainerFromElement((Button)sender)).Content as DireccionesEntregaCliente;
            parameters.Add("empresaParameter", Empresa);
            parameters.Add("clienteParameter", Cliente.Trim());
            parameters.Add("contactoParameter", curItem.contacto);
            regionManager.RequestNavigate("MainRegion", "CrearClienteView", parameters);
        }

        private void btnCrearContacto_Click(object sender, RoutedEventArgs e)
        {
            var parameters = new NavigationParameters();
            parameters.Add("nifParameter", DireccionCompleta.nif);
            parameters.Add("nombreParameter", DireccionCompleta.nombre);
            regionManager.RequestNavigate("MainRegion", "CrearClienteView", parameters);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            eventAggregator.GetEvent<ClienteCreadoEvent>().Subscribe(OnClienteCreado);
            eventAggregator.GetEvent<ClienteModificadoEvent>().Subscribe(OnClienteCreado); //hacemos lo mismo que al crear
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            eventAggregator.GetEvent<ClienteCreadoEvent>().Unsubscribe(OnClienteCreado);
            eventAggregator.GetEvent<ClienteModificadoEvent>().Unsubscribe(OnClienteCreado); //hacemos lo mismo que al crear
        }
    }
}
