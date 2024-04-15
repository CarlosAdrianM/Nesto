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
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Windows.Threading;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorDireccionEntrega.xaml
    /// </summary>
    public partial class SelectorDireccionEntrega : UserControl, INotifyPropertyChanged
    {
        private readonly IRegionManager regionManager;
        private readonly IEventAggregator eventAggregator;
        private readonly IConfiguracion _configuracion;
        private DispatcherTimer timer;


        public SelectorDireccionEntrega()
        {
            InitializeComponent();

            GridPrincipal.DataContext = this;

            listaDireccionesEntrega = new();
            listaDireccionesEntrega.TieneDatosIniciales = true;
            listaDireccionesEntrega.VaciarAlSeleccionar = false;
            listaDireccionesEntrega.SeleccionarPrimerElemento = false;
            
            try
            {
                regionManager = ContainerLocator.Container.Resolve<IRegionManager>();
                eventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();
                _configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();

                listaDireccionesEntrega.ElementoSeleccionadoChanged += (sender, args) =>
                {
                    if (listaDireccionesEntrega is not null && listaDireccionesEntrega.ElementoSeleccionado is not null && DireccionCompleta != listaDireccionesEntrega.ElementoSeleccionado)
                    {
                        //DireccionCompleta = listaDireccionesEntrega.ElementoSeleccionado as DireccionesEntregaCliente; // ¿SetValue?
                        this.SetValue(DireccionCompletaProperty, listaDireccionesEntrega.ElementoSeleccionado as DireccionesEntregaCliente); 
                    }                    
                };
            }
            catch
            {
                // Se usa solo para poder testar controles que incluyan un SelectorDireccionEntrega
            }
        }

        public SelectorDireccionEntrega(IRegionManager regionManager, IEventAggregator eventAggregator, IConfiguracion configuracion)
        {
            // Este constructor lo usamos para poder hacer tests
            InitializeComponent();

            GridPrincipal.DataContext = this;

            listaDireccionesEntrega = new();
            listaDireccionesEntrega.TieneDatosIniciales = true;
            listaDireccionesEntrega.VaciarAlSeleccionar = false;

            this.regionManager = regionManager;
            this.eventAggregator = eventAggregator;
            this._configuracion = configuracion;
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
            DireccionCompleta = (DireccionesEntregaCliente)listaDireccionesEntrega.Lista.Single(l => (l as DireccionesEntregaCliente).contacto == clienteCreado.Contacto.Trim());
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
            if (e.NewValue != e.OldValue)
            {
                //selector.cargarDatos();
                selector.ResetTimer();
            }            
        }


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
              typeof(SelectorDireccionEntrega),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnConfiguracionChanged)));

        private static void OnConfiguracionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorDireccionEntrega selector = (SelectorDireccionEntrega)d;
            //if (selector != null && selector.Configuracion != null)
            //{
            //    selector.cargarDatos();
            //}
        }


        /// <summary>
        /// Gets or sets the DireccionCompleta para las llamadas a la API
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
        /// Identified the DireccionCompleta dependency property
        /// </summary>
        public static readonly DependencyProperty DireccionCompletaProperty =
            DependencyProperty.Register(nameof(DireccionCompleta), typeof(DireccionesEntregaCliente),
              typeof(SelectorDireccionEntrega),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnDireccionCompletaChanged)));

        private static void OnDireccionCompletaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorDireccionEntrega selector = d as SelectorDireccionEntrega;
            if (selector.DireccionCompleta != null && selector.Seleccionada != selector.DireccionCompleta.contacto)
            {
                selector.SetValue(SeleccionadaProperty, selector.DireccionCompleta.contacto);
            }
            if (selector.listaDireccionesEntrega.Lista is not null && selector.listaDireccionesEntrega.ElementoSeleccionado is null)
            {
                selector.listaDireccionesEntrega.ElementoSeleccionado = selector.listaDireccionesEntrega.Lista
                    .Single(c => (c as DireccionesEntregaCliente).contacto == selector.Seleccionada);
            }
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
            set { SetValue(SeleccionadaProperty, value); }
        }

        /// <summary>
        /// Identified the SELECCIONADA dependency property
        /// </summary>
        public static readonly DependencyProperty SeleccionadaProperty =
            DependencyProperty.Register("Seleccionada", typeof(string),
              typeof(SelectorDireccionEntrega),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnSeleccionadaChanged)));

        private static void OnSeleccionadaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SelectorDireccionEntrega selector)
            {
                string newValue = (string)e.NewValue;
                if (newValue != null && newValue != newValue.Trim())
                {
                    newValue = newValue.Trim(); 
                    selector.SetValue(SeleccionadaProperty, newValue);
                }
                if (selector.listaDireccionesEntrega.Lista is not null && selector.listaDireccionesEntrega.ElementoSeleccionado is null)
                {
                    selector.listaDireccionesEntrega.ElementoSeleccionado = selector.listaDireccionesEntrega.Lista
                        .SingleOrDefault(c => (c as DireccionesEntregaCliente).contacto == selector.Seleccionada);
                }
                //if (selector.direccionEntregaSeleccionada != null && selector.Seleccionada != selector.direccionEntregaSeleccionada.contacto)
                //if (selector.DireccionCompleta != null && selector.Seleccionada != selector.DireccionCompleta.contacto)
                //{
                //    selector.SetValue(DireccionCompletaProperty, selector.listaDireccionesEntrega.Lista.SingleOrDefault(l => (l as DireccionesEntregaCliente).contacto == selector.Seleccionada) as DireccionesEntregaCliente);
                //}
            }
        }

        /// <summary>
        /// Gets or sets the TotalPedido para las llamadas a la API
        /// </summary>
        public decimal TotalPedido
        {
            get { return (decimal)GetValue(TotalPedidoProperty); }
            set
            {
                SetValue(TotalPedidoProperty, value);
            }
        }
        /// <summary>
        /// Identified the TotalPedido dependency property
        /// </summary>
        public static readonly DependencyProperty TotalPedidoProperty =
            DependencyProperty.Register(nameof(TotalPedido), typeof(decimal),
              typeof(SelectorDireccionEntrega),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnTotalPedidoChanged)));

        private static void OnTotalPedidoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorDireccionEntrega selector = (SelectorDireccionEntrega)d;
            if (e.NewValue != e.OldValue)
            {
                selector.cargarDatos();
            }
        }

        #endregion

        #region "Propiedades"
        //private DireccionesEntregaCliente _direccionEntregaSeleccionada;
        //public DireccionesEntregaCliente direccionEntregaSeleccionada {
        //    get
        //    {
        //        return _direccionEntregaSeleccionada;
        //    }

        //    set
        //    {
        //        _direccionEntregaSeleccionada = value;
        //        OnPropertyChanged("direccionEntregaSeleccionada");
        //        if (direccionEntregaSeleccionada != null)
        //        {
        //            Seleccionada = direccionEntregaSeleccionada.contacto;
        //        }
        //        DireccionCompleta = direccionEntregaSeleccionada;
        //    }
        //}
        private ColeccionFiltrable _listaDireccionesEntrega;
        public ColeccionFiltrable listaDireccionesEntrega {
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
            timer.Stop();
            cargarDatos();
        }

        private async Task cargarDatos()
        {
            if (Configuracion is null && _configuracion is not null)
            {
                Configuracion = _configuracion;
            }

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
                    string urlConsulta = "PlantillaVentas/DireccionesEntrega?empresa=" + Empresa + "&clienteDirecciones=" + Cliente;
                    if (TotalPedido != 0)
                    {
                        urlConsulta += $"&totalPedido={TotalPedido.ToString(CultureInfo.GetCultureInfo("en-US"))}";
                    }
                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaDireccionesEntrega.ListaOriginal = new ObservableCollection<IFiltrableItem>(JsonConvert.DeserializeObject<ObservableCollection<DireccionesEntregaCliente>>(resultado)); 
                        if (DireccionCompleta == null && Seleccionada != null)
                        {
                            DireccionCompleta = (DireccionesEntregaCliente)listaDireccionesEntrega.Lista.SingleOrDefault(l => (l as DireccionesEntregaCliente).contacto == Seleccionada);
                        }
                        if (DireccionCompleta == null && Seleccionada == null)
                        {
                            DireccionCompleta = (DireccionesEntregaCliente)listaDireccionesEntrega.Lista.SingleOrDefault(l => (l as DireccionesEntregaCliente).esDireccionPorDefecto);
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
            if (DireccionCompleta == null)
            {
                return;
            }
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
