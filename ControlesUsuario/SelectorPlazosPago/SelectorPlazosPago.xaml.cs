using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorDireccionEntrega.xaml
    /// </summary>
    public partial class SelectorPlazosPago : UserControl, INotifyPropertyChanged
    {
        public SelectorPlazosPago()
        {
            InitializeComponent();

            GridPrincipal.DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region Dependency Properties

        /// <summary>
        /// Gets or sets the CLIENTE para las llamadas a la API
        /// </summary>
        public string Cliente
        {
            get => (string)GetValue(ClienteProperty); set => SetValue(ClienteProperty, value);
        }

        /// <summary>
        /// Identified the CLIENTE dependency property
        /// </summary>
        public static readonly DependencyProperty ClienteProperty =
            DependencyProperty.Register("Cliente", typeof(string),
              typeof(SelectorPlazosPago),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnClienteChanged)));

        private static void OnClienteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorPlazosPago selector = (SelectorPlazosPago)d;
            selector.cargarDatos();
        }
        /// <summary>
        /// Gets or sets the Configuracion para las llamadas a la API
        /// </summary>
        public IConfiguracion Configuracion
        {
            get => (IConfiguracion)GetValue(ConfiguracionProperty); set => SetValue(ConfiguracionProperty, value);
        }

        /// <summary>
        /// Identified the Configuracion dependency property
        /// </summary>
        public static readonly DependencyProperty ConfiguracionProperty =
            DependencyProperty.Register("Configuracion", typeof(IConfiguracion),
              typeof(SelectorPlazosPago));

        /// <summary>
        /// Gets or sets the EMPRESA para las llamadas a la API
        /// </summary>
        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty); set => SetValue(EmpresaProperty, value);
        }

        /// <summary>
        /// Identified the EMPRESA dependency property
        /// </summary>
        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register("Empresa", typeof(string),
              typeof(SelectorPlazosPago));


        /// <summary>
        /// Gets or sets the SELECCIONADA para las llamadas a la API
        /// </summary>
        public string Seleccionada
        {
            get => (string)GetValue(SeleccionadaProperty); set => SetValue(SeleccionadaProperty, value);
        }

        /// <summary>
        /// Identified the SELECCIONADA dependency property
        /// </summary>
        public static readonly DependencyProperty SeleccionadaProperty =
            DependencyProperty.Register(nameof(Seleccionada), typeof(string),
              typeof(SelectorPlazosPago),
                new FrameworkPropertyMetadata(new PropertyChangedCallback(OnSeleccionadaChanged)));
        private static void OnSeleccionadaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorPlazosPago selector = (SelectorPlazosPago)d;
            if (selector.listaPlazosPago != null && !string.IsNullOrEmpty(e.NewValue?.ToString()))
            {
                selector.plazosPagoSeleccionado = selector.listaPlazosPago
                    .Where(l => l.plazoPago == e.NewValue.ToString())
                    .SingleOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the DESCUENTO para las llamadas a la API
        /// </summary>
        public decimal Descuento
        {
            get => (decimal)GetValue(DescuentoProperty); set => SetValue(DescuentoProperty, value);
        }

        /// <summary>
        /// Identified the SELECCIONADA dependency property
        /// </summary>
        public static readonly DependencyProperty DescuentoProperty =
            DependencyProperty.Register("Descuento", typeof(decimal),
              typeof(SelectorPlazosPago));



        /// <summary>
        /// Gets or sets the FormaPago para las llamadas a la API
        /// </summary>
        public string FormaPago
        {
            get => (string)GetValue(FormaPagoProperty); set => SetValue(FormaPagoProperty, value);
        }
        /// <summary>
        /// Identified the FormaPago dependency property
        /// </summary>
        public static readonly DependencyProperty FormaPagoProperty =
            DependencyProperty.Register(nameof(FormaPago), typeof(string),
              typeof(SelectorPlazosPago),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnFormaPagoChanged)));

        private static void OnFormaPagoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorPlazosPago selector = (SelectorPlazosPago)d;
            selector.cargarDatos();
        }

        /// <summary>
        /// Gets or sets the TotalPedido para las llamadas a la API
        /// </summary>
        public decimal TotalPedido
        {
            get => (decimal)GetValue(TotalPedidoProperty); set => SetValue(TotalPedidoProperty, value);
        }
        /// <summary>
        /// Identified the TotalPedido dependency property
        /// </summary>
        public static readonly DependencyProperty TotalPedidoProperty =
            DependencyProperty.Register(nameof(TotalPedido), typeof(decimal),
              typeof(SelectorPlazosPago),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnTotalPedidoChanged)));

        private static void OnTotalPedidoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorPlazosPago selector = (SelectorPlazosPago)d;
            selector.cargarDatos();
        }


        /// <summary>
        /// Gets or sets the PlazoPagoCompleto para las llamadas a la API
        /// </summary>
        public PlazosPago PlazoPagoCompleto
        {
            get => (PlazosPago)GetValue(PlazoPagoCompletoProperty); set => SetValue(PlazoPagoCompletoProperty, value);
        }

        /// <summary>
        /// Identified the PlazoPagoCompleto dependency property
        /// </summary>
        public static readonly DependencyProperty PlazoPagoCompletoProperty =
            DependencyProperty.Register(nameof(PlazoPagoCompleto), typeof(PlazosPago),
              typeof(SelectorPlazosPago));



        public string Etiqueta
        {
            get => (string)GetValue(EtiquetaProperty); set => SetValue(EtiquetaProperty, value);
        }

        /// <summary>
        /// Identified the ETIQUETA dependency property
        /// </summary>
        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register(nameof(Etiqueta), typeof(string),
              typeof(SelectorPlazosPago),
              new UIPropertyMetadata("Seleccione unos plazos de pago:"));

        public Visibility VisibilidadEtiqueta
        {
            get => (Visibility)GetValue(VisibilidadEtiquetaProperty); set => SetValue(VisibilidadEtiquetaProperty, value);
        }

        /// <summary>
        /// Identified the VISIBILIDADETIQUETA dependency property
        /// </summary>
        public static readonly DependencyProperty VisibilidadEtiquetaProperty =
            DependencyProperty.Register(nameof(VisibilidadEtiqueta), typeof(Visibility),
              typeof(SelectorPlazosPago));


        #endregion

        #region "Propiedades"
        private bool _actualizandoPlazosPago = false;

        private PlazosPago _plazosPagoSeleccionado;
        public PlazosPago plazosPagoSeleccionado
        {
            get => _plazosPagoSeleccionado;

            set
            {
                if (_plazosPagoSeleccionado != value && !_actualizandoPlazosPago)
                {
                    _actualizandoPlazosPago = true;
                    _plazosPagoSeleccionado = value;
                    OnPropertyChanged(nameof(plazosPagoSeleccionado));

                    if (plazosPagoSeleccionado != null)
                    {
                        Seleccionada = plazosPagoSeleccionado.plazoPago;
                        Descuento = plazosPagoSeleccionado.descuentoPP;
                    }
                    PlazoPagoCompleto = plazosPagoSeleccionado;
                    _actualizandoPlazosPago = false;
                }
            }
        }
        private ObservableCollection<PlazosPago> _listaPlazosPago;
        public ObservableCollection<PlazosPago> listaPlazosPago
        {
            get => _listaPlazosPago;
            set
            {
                _listaPlazosPago = value;
                OnPropertyChanged("listaPlazosPago");
            }
        }

        #endregion

        #region "Funciones Auxiliares"
        private async void cargarDatos()
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(Configuracion.servidorAPI);
            HttpResponseMessage response;

            try
            {
                string urlConsulta = "PlazosPago?empresa=" + Empresa;
                if (Cliente != string.Empty)
                {
                    urlConsulta += "&cliente=" + Cliente;
                }
                if (!string.IsNullOrEmpty(FormaPago) && TotalPedido != 0)
                {
                    urlConsulta += $"&formaPago={FormaPago}&totalPedido={TotalPedido.ToString(CultureInfo.GetCultureInfo("en-US"))}";
                }

                response = await client.GetAsync(urlConsulta);

                if (response.IsSuccessStatusCode)
                {
                    string resultado = await response.Content.ReadAsStringAsync();
                    listaPlazosPago = JsonConvert.DeserializeObject<ObservableCollection<PlazosPago>>(resultado);
                    if (!string.IsNullOrEmpty(Seleccionada))
                    {
                        plazosPagoSeleccionado = listaPlazosPago.Where(l => l.plazoPago == Seleccionada).SingleOrDefault();
                    }
                }
            }
            catch
            {
                _ = MessageBox.Show("No se pudieron leer los plazos de pago");
            }
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion

    }
}
