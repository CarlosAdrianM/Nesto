using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorDireccionEntrega.xaml
    /// </summary>
    public partial class SelectorPlazosPago : UserControl, INotifyPropertyChanged
    {
        private Timer _debounceTimer;
        private const int DEBOUNCE_DELAY = 300; // ms

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
            selector.CargarDatosConDebounce();
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
            selector.CargarDatosConDebounce();
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
            selector.CargarDatosConDebounce();
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

        private InfoDeudaCliente _infoDeuda;
        public InfoDeudaCliente InfoDeuda
        {
            get => _infoDeuda;
            set
            {
                _infoDeuda = value;
                OnPropertyChanged(nameof(InfoDeuda));
                OnPropertyChanged(nameof(MensajeDeuda));
                OnPropertyChanged(nameof(VisibilidadMensajeDeuda));
            }
        }

        private string _mensajePlazoNoPermitido;
        public string MensajePlazoNoPermitido
        {
            get => _mensajePlazoNoPermitido;
            set
            {
                _mensajePlazoNoPermitido = value;
                OnPropertyChanged(nameof(MensajePlazoNoPermitido));
                OnPropertyChanged(nameof(VisibilidadMensajePlazoNoPermitido));
            }
        }

        public Visibility VisibilidadMensajePlazoNoPermitido
        {
            get => !string.IsNullOrEmpty(MensajePlazoNoPermitido)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public string MensajeDeuda
        {
            get
            {
                if (InfoDeuda == null || string.IsNullOrEmpty(InfoDeuda.MotivoRestriccion))
                    return string.Empty;

                var mensaje = "⚠️ Solo se permiten formas de pago al contado debido a: " + InfoDeuda.MotivoRestriccion;

                if (InfoDeuda.TieneImpagados && InfoDeuda.ImporteImpagados.HasValue)
                {
                    mensaje += $"\n   • Impagados: {InfoDeuda.ImporteImpagados.Value:C}";
                }

                if (InfoDeuda.TieneDeudaVencida && InfoDeuda.ImporteDeudaVencida.HasValue && InfoDeuda.DiasVencimiento.HasValue)
                {
                    mensaje += $"\n   • Cartera vencida: {InfoDeuda.ImporteDeudaVencida.Value:C} ({InfoDeuda.DiasVencimiento.Value} días)";
                }

                return mensaje;
            }
        }

        public Visibility VisibilidadMensajeDeuda
        {
            get => InfoDeuda != null && !string.IsNullOrEmpty(InfoDeuda.MotivoRestriccion)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

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

        private void CargarDatosConDebounce()
        {
            // Cancelar el timer previo si existe
            _debounceTimer?.Dispose();

            // Crear nuevo timer que ejecutará cargarDatos() después de DEBOUNCE_DELAY ms
            _debounceTimer = new Timer(
                _ => Dispatcher.Invoke(() => cargarDatos()),
                null,
                DEBOUNCE_DELAY,
                Timeout.Infinite
            );
        }

        private async void cargarDatos()
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(Configuracion.servidorAPI);
            HttpResponseMessage response;

            try
            {
                // Si hay cliente especificado, usar el nuevo endpoint con info de deuda
                if (!string.IsNullOrEmpty(Cliente))
                {
                    string urlConsulta = $"PlazosPago/ConInfoDeuda?empresa={Empresa}&cliente={Cliente}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var respuesta = JsonConvert.DeserializeObject<PlazosPagoResponse>(resultado);

                        listaPlazosPago = new ObservableCollection<PlazosPago>(respuesta.PlazosPago);
                        InfoDeuda = respuesta.InfoDeuda;

                        ValidarYAjustarPlazoSeleccionado();
                    }
                }
                else
                {
                    // Sin cliente, usar el endpoint original
                    string urlConsulta = "PlazosPago?empresa=" + Empresa;
                    if (!string.IsNullOrEmpty(FormaPago) && TotalPedido != 0)
                    {
                        urlConsulta += $"&formaPago={FormaPago}&totalPedido={TotalPedido.ToString(CultureInfo.GetCultureInfo("en-US"))}";
                    }

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaPlazosPago = JsonConvert.DeserializeObject<ObservableCollection<PlazosPago>>(resultado);
                        InfoDeuda = null; // Sin cliente no hay info de deuda

                        ValidarYAjustarPlazoSeleccionado();
                    }
                }
            }
            catch
            {
                _ = MessageBox.Show("No se pudieron leer los plazos de pago");
            }
        }

        /// <summary>
        /// Valida que el plazo seleccionado esté en la lista disponible.
        /// Si no está (ej: pedido antiguo con plazo ya no permitido), auto-selecciona contado y muestra advertencia.
        /// </summary>
        private void ValidarYAjustarPlazoSeleccionado()
        {
            // Limpiar mensaje previo
            MensajePlazoNoPermitido = null;

            if (string.IsNullOrEmpty(Seleccionada) || listaPlazosPago == null || !listaPlazosPago.Any())
            {
                return;
            }

            // Buscar el plazo seleccionado en la lista
            var plazoEncontrado = listaPlazosPago.FirstOrDefault(l => l.plazoPago == Seleccionada);

            if (plazoEncontrado != null)
            {
                // El plazo existe, todo bien
                plazosPagoSeleccionado = plazoEncontrado;
            }
            else
            {
                // El plazo NO existe (ej: pedido con "30 días" pero cliente tiene impagados)
                // Auto-seleccionar primer plazo de contado
                var plazoContado = listaPlazosPago.FirstOrDefault(p =>
                    p.numeroPlazos == 1 &&
                    p.diasPrimerPlazo == 0 &&
                    p.mesesPrimerPlazo == 0);

                if (plazoContado != null)
                {
                    string plazoAnterior = Seleccionada;
                    plazosPagoSeleccionado = plazoContado;

                    // Generar mensaje de advertencia
                    string motivoRestriccion = InfoDeuda?.MotivoRestriccion ?? "restricciones del cliente";
                    MensajePlazoNoPermitido = $"⚠️ Se cambió de '{plazoAnterior}' a '{plazoContado.descripcion}' debido a: {motivoRestriccion}";
                }
                else
                {
                    // No hay plazos de contado disponibles (caso raro)
                    plazosPagoSeleccionado = null;
                    MensajePlazoNoPermitido = $"⚠️ El plazo '{Seleccionada}' ya no está permitido y no hay plazos al contado disponibles";
                }
            }
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion

    }
}
