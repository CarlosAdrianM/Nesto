using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Control reutilizable para seleccionar facturas de un cliente.
    /// Issue #279 - SelectorFacturas
    /// </summary>
    public partial class SelectorFacturas : UserControl, INotifyPropertyChanged
    {
        public SelectorFacturas()
        {
            InitializeComponent();
            GridPrincipal.DataContext = this;
            LimpiarBusquedaCommand = new DelegateCommand(OnLimpiarBusqueda);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region Dependency Properties - Entrada

        /// <summary>
        /// Empresa del cliente.
        /// </summary>
        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty);
            set => SetValue(EmpresaProperty, value);
        }

        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register(nameof(Empresa), typeof(string),
                typeof(SelectorFacturas),
                new FrameworkPropertyMetadata(null, OnClientePropertyChanged));

        /// <summary>
        /// Numero de cliente.
        /// </summary>
        public string Cliente
        {
            get => (string)GetValue(ClienteProperty);
            set => SetValue(ClienteProperty, value);
        }

        public static readonly DependencyProperty ClienteProperty =
            DependencyProperty.Register(nameof(Cliente), typeof(string),
                typeof(SelectorFacturas),
                new FrameworkPropertyMetadata(null, OnClientePropertyChanged));

        /// <summary>
        /// Contacto del cliente.
        /// </summary>
        public string Contacto
        {
            get => (string)GetValue(ContactoProperty);
            set => SetValue(ContactoProperty, value);
        }

        public static readonly DependencyProperty ContactoProperty =
            DependencyProperty.Register(nameof(Contacto), typeof(string),
                typeof(SelectorFacturas),
                new FrameworkPropertyMetadata(null, OnClientePropertyChanged));

        /// <summary>
        /// Configuracion para las llamadas a la API.
        /// </summary>
        public IConfiguracion Configuracion
        {
            get => (IConfiguracion)GetValue(ConfiguracionProperty);
            set => SetValue(ConfiguracionProperty, value);
        }

        public static readonly DependencyProperty ConfiguracionProperty =
            DependencyProperty.Register(nameof(Configuracion), typeof(IConfiguracion),
                typeof(SelectorFacturas));

        private static async void OnClientePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SelectorFacturas selector)
            {
                // Solo cargar si el nuevo valor de Cliente es valido y diferente
                // Esto evita cargas innecesarias cuando Empresa cambia pero Cliente esta vacio
                if (!string.IsNullOrWhiteSpace(selector.Cliente) && !string.IsNullOrWhiteSpace(selector.Empresa))
                {
                    await selector.CargarFacturasAsync();
                }
                else
                {
                    // Limpiar la lista si no hay cliente valido
                    selector.TodasLasFacturas = new ObservableCollection<FacturaClienteDTO>();
                }
            }
        }

        /// <summary>
        /// Accion para abrir un pedido (empresa, numeroPedido).
        /// Se invoca al hacer doble clic en una fila que proviene de lineas de pedido.
        /// </summary>
        public Action<string, int> AbrirPedidoAction
        {
            get => (Action<string, int>)GetValue(AbrirPedidoActionProperty);
            set => SetValue(AbrirPedidoActionProperty, value);
        }

        public static readonly DependencyProperty AbrirPedidoActionProperty =
            DependencyProperty.Register(nameof(AbrirPedidoAction), typeof(Action<string, int>),
                typeof(SelectorFacturas));

        #endregion

        #region Dependency Properties - Salida

        /// <summary>
        /// Coleccion de facturas seleccionadas (con checkbox marcado).
        /// </summary>
        public ObservableCollection<FacturaClienteDTO> FacturasSeleccionadas
        {
            get => (ObservableCollection<FacturaClienteDTO>)GetValue(FacturasSeleccionadasProperty);
            set => SetValue(FacturasSeleccionadasProperty, value);
        }

        public static readonly DependencyProperty FacturasSeleccionadasProperty =
            DependencyProperty.Register(nameof(FacturasSeleccionadas), typeof(ObservableCollection<FacturaClienteDTO>),
                typeof(SelectorFacturas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// Factura actualmente seleccionada (fila activa en el DataGrid).
        /// </summary>
        public FacturaClienteDTO FacturaSeleccionada
        {
            get => (FacturaClienteDTO)GetValue(FacturaSeleccionadaProperty);
            set => SetValue(FacturaSeleccionadaProperty, value);
        }

        public static readonly DependencyProperty FacturaSeleccionadaProperty =
            DependencyProperty.Register(nameof(FacturaSeleccionada), typeof(FacturaClienteDTO),
                typeof(SelectorFacturas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        #endregion

        #region Dependency Properties - Configuracion

        /// <summary>
        /// Mostrar checkbox en cabecera para seleccionar/deseleccionar todas.
        /// </summary>
        public bool MostrarSeleccionarTodas
        {
            get => (bool)GetValue(MostrarSeleccionarTodasProperty);
            set => SetValue(MostrarSeleccionarTodasProperty, value);
        }

        public static readonly DependencyProperty MostrarSeleccionarTodasProperty =
            DependencyProperty.Register(nameof(MostrarSeleccionarTodas), typeof(bool),
                typeof(SelectorFacturas),
                new PropertyMetadata(true));

        /// <summary>
        /// Mostrar panel con resumen de facturas seleccionadas y total.
        /// </summary>
        public bool MostrarResumenSeleccion
        {
            get => (bool)GetValue(MostrarResumenSeleccionProperty);
            set => SetValue(MostrarResumenSeleccionProperty, value);
        }

        public static readonly DependencyProperty MostrarResumenSeleccionProperty =
            DependencyProperty.Register(nameof(MostrarResumenSeleccion), typeof(bool),
                typeof(SelectorFacturas),
                new PropertyMetadata(true));

        /// <summary>
        /// Mostrar barra de busqueda rapida por documento.
        /// </summary>
        public bool MostrarBusqueda
        {
            get => (bool)GetValue(MostrarBusquedaProperty);
            set => SetValue(MostrarBusquedaProperty, value);
        }

        public static readonly DependencyProperty MostrarBusquedaProperty =
            DependencyProperty.Register(nameof(MostrarBusqueda), typeof(bool),
                typeof(SelectorFacturas),
                new PropertyMetadata(true));

        /// <summary>
        /// Mostrar fondo diferente para facturas rectificativas.
        /// </summary>
        public bool MostrarIndicadorRectificativa
        {
            get => (bool)GetValue(MostrarIndicadorRectificativaProperty);
            set => SetValue(MostrarIndicadorRectificativaProperty, value);
        }

        public static readonly DependencyProperty MostrarIndicadorRectificativaProperty =
            DependencyProperty.Register(nameof(MostrarIndicadorRectificativa), typeof(bool),
                typeof(SelectorFacturas),
                new PropertyMetadata(true));

        /// <summary>
        /// Permitir seleccion multiple con checkboxes.
        /// </summary>
        public bool PermitirSeleccionMultiple
        {
            get => (bool)GetValue(PermitirSeleccionMultipleProperty);
            set => SetValue(PermitirSeleccionMultipleProperty, value);
        }

        public static readonly DependencyProperty PermitirSeleccionMultipleProperty =
            DependencyProperty.Register(nameof(PermitirSeleccionMultiple), typeof(bool),
                typeof(SelectorFacturas),
                new PropertyMetadata(true));

        /// <summary>
        /// Altura maxima del control (para espacios reducidos).
        /// </summary>
        public double AlturaMaxima
        {
            get => (double)GetValue(AlturaMaximaProperty);
            set => SetValue(AlturaMaximaProperty, value);
        }

        public static readonly DependencyProperty AlturaMaximaProperty =
            DependencyProperty.Register(nameof(AlturaMaxima), typeof(double),
                typeof(SelectorFacturas),
                new PropertyMetadata(double.PositiveInfinity));

        #endregion

        #region Propiedades internas

        private ObservableCollection<FacturaClienteDTO> _todasLasFacturas;
        /// <summary>
        /// Lista completa de facturas (sin filtrar).
        /// </summary>
        public ObservableCollection<FacturaClienteDTO> TodasLasFacturas
        {
            get => _todasLasFacturas;
            set
            {
                _todasLasFacturas = value;
                OnPropertyChanged(nameof(TodasLasFacturas));
                AplicarFiltro();
            }
        }

        private ObservableCollection<FacturaClienteDTO> _facturasFiltradas;
        /// <summary>
        /// Lista de facturas filtradas por busqueda.
        /// </summary>
        public ObservableCollection<FacturaClienteDTO> FacturasFiltradas
        {
            get => _facturasFiltradas;
            set
            {
                _facturasFiltradas = value;
                OnPropertyChanged(nameof(FacturasFiltradas));
            }
        }

        private string _textoBusqueda;
        /// <summary>
        /// Texto para filtrar por numero de documento.
        /// </summary>
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                if (_textoBusqueda != value)
                {
                    _textoBusqueda = value;
                    OnPropertyChanged(nameof(TextoBusqueda));
                    OnPropertyChanged(nameof(TieneBusqueda));
                    AplicarFiltro();
                }
            }
        }

        public bool TieneBusqueda => !string.IsNullOrWhiteSpace(TextoBusqueda);

        private FacturaClienteDTO _facturaSeleccionadaInterna;
        /// <summary>
        /// Factura seleccionada internamente (fila activa).
        /// </summary>
        public FacturaClienteDTO FacturaSeleccionadaInterna
        {
            get => _facturaSeleccionadaInterna;
            set
            {
                if (_facturaSeleccionadaInterna != value)
                {
                    _facturaSeleccionadaInterna = value;
                    OnPropertyChanged(nameof(FacturaSeleccionadaInterna));
                    FacturaSeleccionada = value;
                }
            }
        }

        private bool _todasSeleccionadas;
        /// <summary>
        /// Indica si todas las facturas estan seleccionadas.
        /// </summary>
        public bool TodasSeleccionadas
        {
            get => _todasSeleccionadas;
            set
            {
                if (_todasSeleccionadas != value)
                {
                    _todasSeleccionadas = value;
                    OnPropertyChanged(nameof(TodasSeleccionadas));
                }
            }
        }

        /// <summary>
        /// Numero de facturas seleccionadas (documentos unicos).
        /// </summary>
        public int NumeroFacturasSeleccionadas =>
            FacturasFiltradas?.Where(f => f.Seleccionada)
                .Select(f => f.Documento?.Trim())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() ?? 0;

        /// <summary>
        /// Total de las facturas seleccionadas (importe de la factura original, no de lineas individuales).
        /// </summary>
        public decimal TotalFacturasSeleccionadas
        {
            get
            {
                var documentosSeleccionados = FacturasFiltradas?.Where(f => f.Seleccionada)
                    .Select(f => f.Documento?.Trim())
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                if (documentosSeleccionados == null || !documentosSeleccionados.Any())
                {
                    return 0;
                }

                // Buscar importes en TodasLasFacturas (tienen el importe total de la factura)
                if (TodasLasFacturas != null)
                {
                    return TodasLasFacturas
                        .Where(f => documentosSeleccionados.Contains(f.Documento?.Trim(), StringComparer.OrdinalIgnoreCase))
                        .Sum(f => f.Importe);
                }

                // Fallback: agrupar por documento y tomar el primer importe
                return FacturasFiltradas.Where(f => f.Seleccionada)
                    .GroupBy(f => f.Documento?.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Sum(g => g.First().Importe);
            }
        }

        private bool _estaCargando;
        public bool EstaCargando
        {
            get => _estaCargando;
            set
            {
                _estaCargando = value;
                OnPropertyChanged(nameof(EstaCargando));
            }
        }

        #endregion

        #region Commands

        public DelegateCommand LimpiarBusquedaCommand { get; }

        private void OnLimpiarBusqueda()
        {
            TextoBusqueda = string.Empty;
        }

        #endregion

        #region Metodos

        private async Task CargarFacturasAsync()
        {
            // Resolver Configuracion si no esta establecida (necesario para que funcione el binding)
            if (Configuracion == null)
            {
                try
                {
                    Configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
                }
                catch
                {
                    // Si no se puede resolver, no podemos cargar
                    return;
                }
            }

            // Doble verificacion: no cargar si faltan datos esenciales
            if (Configuracion == null || string.IsNullOrWhiteSpace(Cliente) || string.IsNullOrWhiteSpace(Empresa))
            {
                return;
            }

            EstaCargando = true;

            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(Configuracion.servidorAPI);

                    // Determinar rango de fechas segun permisos del usuario
                    bool esAdmin = Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
                    int mesesAtras = esAdmin ? 72 : 6;

                    string urlConsulta = $"ExtractosCliente?empresa={Empresa}&cliente={Cliente}&tipoApunte=1";
                    urlConsulta += $"&fechaDesde={DateTime.Today.AddMonths(-mesesAtras):s}";
                    urlConsulta += $"&fechaHasta={DateTime.Today:s}";

                    var response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var facturas = JsonConvert.DeserializeObject<List<FacturaClienteDTO>>(json);

                        // Suscribirse a cambios en cada factura para actualizar totales
                        var facturasCollection = new ObservableCollection<FacturaClienteDTO>(
                            (facturas ?? new List<FacturaClienteDTO>()).OrderByDescending(f => f.Fecha));

                        foreach (var factura in facturasCollection)
                        {
                            factura.PropertyChanged += Factura_PropertyChanged;
                        }

                        TodasLasFacturas = facturasCollection;
                    }
                    else
                    {
                        TodasLasFacturas = new ObservableCollection<FacturaClienteDTO>();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar facturas: {ex.Message}");
                TodasLasFacturas = new ObservableCollection<FacturaClienteDTO>();
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private void Factura_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FacturaClienteDTO.Seleccionada))
            {
                ActualizarSeleccion();
            }
        }

        private bool _sincronizandoSeleccion;

        private void LineaPedido_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(FacturaClienteDTO.Seleccionada) || _sincronizandoSeleccion)
            {
                return;
            }

            if (!(sender is FacturaClienteDTO lineaPedido))
            {
                return;
            }

            _sincronizandoSeleccion = true;
            try
            {
                bool seleccionada = lineaPedido.Seleccionada;
                string doc = lineaPedido.Documento?.Trim();
                bool tieneDocumento = !string.IsNullOrWhiteSpace(doc);

                bool facturaOriginalEncontrada = false;

                if (tieneDocumento)
                {
                    // Seleccionar/deseleccionar la factura original en TodasLasFacturas
                    // Esto disparara Factura_PropertyChanged -> ActualizarSeleccion
                    if (TodasLasFacturas != null)
                    {
                        var facturaOriginal = TodasLasFacturas.FirstOrDefault(f =>
                            f.Documento?.Trim().Equals(doc, StringComparison.OrdinalIgnoreCase) ?? false);

                        if (facturaOriginal != null && facturaOriginal.Seleccionada != seleccionada)
                        {
                            facturaOriginal.Seleccionada = seleccionada;
                            facturaOriginalEncontrada = true;
                        }
                    }

                    // Seleccionar/deseleccionar todas las lineas del mismo documento en FacturasFiltradas
                    if (FacturasFiltradas != null)
                    {
                        foreach (var otra in FacturasFiltradas)
                        {
                            if (otra != lineaPedido
                                && otra.NumeroPedido != null
                                && (otra.Documento?.Trim().Equals(doc, StringComparison.OrdinalIgnoreCase) ?? false)
                                && otra.Seleccionada != seleccionada)
                            {
                                otra.Seleccionada = seleccionada;
                            }
                        }
                    }
                }

                // Si no se encontro la factura original, actualizar conteo manualmente
                if (!facturaOriginalEncontrada)
                {
                    ActualizarSeleccion();
                }
            }
            finally
            {
                _sincronizandoSeleccion = false;
            }
        }

        private void ActualizarSeleccion()
        {
            OnPropertyChanged(nameof(NumeroFacturasSeleccionadas));
            OnPropertyChanged(nameof(TotalFacturasSeleccionadas));

            // Actualizar la propiedad de salida con facturas unicas seleccionadas.
            // Para cada documento, preferir la factura original (de TodasLasFacturas) sobre las lineas de busqueda.
            var seleccionadas = new List<FacturaClienteDTO>();
            var documentosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var f in FacturasFiltradas?.Where(f => f.Seleccionada) ?? Enumerable.Empty<FacturaClienteDTO>())
            {
                string doc = f.Documento?.Trim();
                if (string.IsNullOrWhiteSpace(doc) || documentosVistos.Add(doc))
                {
                    // Si es linea de pedido, buscar la factura original en TodasLasFacturas
                    if (f.NumeroPedido != null && !string.IsNullOrWhiteSpace(doc) && TodasLasFacturas != null)
                    {
                        var original = TodasLasFacturas.FirstOrDefault(t =>
                            t.Documento?.Trim().Equals(doc, StringComparison.OrdinalIgnoreCase) ?? false);
                        seleccionadas.Add(original ?? f);
                    }
                    else
                    {
                        seleccionadas.Add(f);
                    }
                }
            }

            FacturasSeleccionadas = new ObservableCollection<FacturaClienteDTO>(seleccionadas);

            // Actualizar estado de "todas seleccionadas"
            if (FacturasFiltradas != null && FacturasFiltradas.Any())
            {
                _todasSeleccionadas = FacturasFiltradas.All(f => f.Seleccionada);
                OnPropertyChanged(nameof(TodasSeleccionadas));
            }
        }

        private CancellationTokenSource _busquedaLineasCts;

        private void AplicarFiltro()
        {
            if (TodasLasFacturas == null)
            {
                FacturasFiltradas = new ObservableCollection<FacturaClienteDTO>();
                return;
            }

            if (string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                _busquedaLineasCts?.Cancel();
                FacturasFiltradas = new ObservableCollection<FacturaClienteDTO>(TodasLasFacturas);
            }
            else
            {
                var filtro = TextoBusqueda.Trim().ToLowerInvariant();
                var filtradas = TodasLasFacturas.Where(f =>
                    (f.Documento?.ToLowerInvariant().Contains(filtro) ?? false) ||
                    (f.Concepto?.ToLowerInvariant().Contains(filtro) ?? false));
                FacturasFiltradas = new ObservableCollection<FacturaClienteDTO>(filtradas);

                _ = BuscarEnLineasPedidoConDebounceAsync(TextoBusqueda.Trim());
            }
        }

        private async Task BuscarEnLineasPedidoConDebounceAsync(string texto)
        {
            _busquedaLineasCts?.Cancel();

            if (texto.Length < 3)
            {
                return;
            }

            _busquedaLineasCts = new CancellationTokenSource();
            var token = _busquedaLineasCts.Token;

            try
            {
                await Task.Delay(500, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await BuscarEnLineasPedidoAsync(texto, token);
            }
            catch (TaskCanceledException)
            {
                // Debounce cancelado por nueva pulsacion, es normal
            }
        }

        private async Task BuscarEnLineasPedidoAsync(string texto, CancellationToken token)
        {
            if (Configuracion == null || string.IsNullOrWhiteSpace(Cliente) || string.IsNullOrWhiteSpace(Empresa))
            {
                return;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(Configuracion.servidorAPI);

                    string url = $"PedidosVenta/BuscarLineas?cliente={Cliente}&texto={Uri.EscapeDataString(texto)}";
                    var response = await client.GetAsync(url, token);

                    if (token.IsCancellationRequested || response == null || !response.IsSuccessStatusCode)
                    {
                        return;
                    }

                    string json = await response.Content.ReadAsStringAsync();
                    var lineas = JsonConvert.DeserializeObject<List<LineaPedidoVentaBusquedaDTO>>(json);

                    if (token.IsCancellationRequested || lineas == null || !lineas.Any())
                    {
                        return;
                    }

                    // Convertir lineas de pedido a FacturaClienteDTO para mostrar en el mismo DataGrid
                    var lineasComoFacturas = lineas.Select(l => new FacturaClienteDTO
                    {
                        Empresa = l.Empresa?.Trim(),
                        Cliente = Cliente,
                        Fecha = l.FechaFactura ?? DateTime.MinValue,
                        Documento = l.Factura?.Trim(),
                        Concepto = $"{l.Producto?.Trim()} - {l.Texto?.Trim()} [Pedido {l.Pedido}]",
                        Importe = l.BaseImponible,
                        NumeroPedido = l.Pedido
                    });

                    // Combinar con las facturas filtradas localmente, evitando duplicados por factura
                    var facturasActuales = FacturasFiltradas?.ToList() ?? new List<FacturaClienteDTO>();
                    var documentosExistentes = new HashSet<string>(
                        facturasActuales.Where(f => f.Documento != null).Select(f => f.Documento.Trim()),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var linea in lineasComoFacturas)
                    {
                        if (!string.IsNullOrWhiteSpace(linea.Documento) && documentosExistentes.Contains(linea.Documento))
                        {
                            continue;
                        }
                        linea.PropertyChanged += LineaPedido_PropertyChanged;
                        facturasActuales.Add(linea);
                    }

                    FacturasFiltradas = new ObservableCollection<FacturaClienteDTO>(
                        facturasActuales.OrderByDescending(f => f.Fecha));
                }
            }
            catch (TaskCanceledException)
            {
                // Cancelado, es normal
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al buscar en lineas de pedido: {ex.Message}");
            }
        }

        /// <summary>
        /// Selecciona una factura por su numero de documento.
        /// Util para auto-seleccionar cuando se busca por numero de factura.
        /// </summary>
        public void SeleccionarFacturaPorDocumento(string numeroDocumento)
        {
            if (TodasLasFacturas == null || string.IsNullOrWhiteSpace(numeroDocumento))
            {
                return;
            }

            var factura = TodasLasFacturas.FirstOrDefault(f =>
                f.Documento?.Trim().Equals(numeroDocumento.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

            if (factura != null)
            {
                factura.Seleccionada = true;
                FacturaSeleccionadaInterna = factura;
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Event Handlers

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Configuracion == null)
            {
                Configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
            }

            if (Empresa == null)
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO;
            }
        }

        private void TextBoxBusqueda_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        private void TextBoxBusqueda_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FacturaSeleccionadaInterna?.NumeroPedido != null && AbrirPedidoAction != null)
            {
                AbrirPedidoAction(FacturaSeleccionadaInterna.Empresa, FacturaSeleccionadaInterna.NumeroPedido.Value);
            }
        }

        private void CheckBoxSeleccionarTodas_Changed(object sender, RoutedEventArgs e)
        {
            if (FacturasFiltradas == null)
            {
                return;
            }

            bool seleccionar = ((CheckBox)sender).IsChecked ?? false;

            foreach (var factura in FacturasFiltradas)
            {
                factura.Seleccionada = seleccionar;
            }
        }

        #endregion
    }
}
