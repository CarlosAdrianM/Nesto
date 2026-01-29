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
        /// Numero de facturas seleccionadas.
        /// </summary>
        public int NumeroFacturasSeleccionadas =>
            TodasLasFacturas?.Count(f => f.Seleccionada) ?? 0;

        /// <summary>
        /// Total de las facturas seleccionadas.
        /// </summary>
        public decimal TotalFacturasSeleccionadas =>
            TodasLasFacturas?.Where(f => f.Seleccionada).Sum(f => f.Importe) ?? 0;

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

        private void ActualizarSeleccion()
        {
            OnPropertyChanged(nameof(NumeroFacturasSeleccionadas));
            OnPropertyChanged(nameof(TotalFacturasSeleccionadas));

            // Actualizar la propiedad de salida
            FacturasSeleccionadas = new ObservableCollection<FacturaClienteDTO>(
                TodasLasFacturas?.Where(f => f.Seleccionada) ?? new List<FacturaClienteDTO>());

            // Actualizar estado de "todas seleccionadas"
            if (TodasLasFacturas != null && TodasLasFacturas.Any())
            {
                _todasSeleccionadas = TodasLasFacturas.All(f => f.Seleccionada);
                OnPropertyChanged(nameof(TodasSeleccionadas));
            }
        }

        private void AplicarFiltro()
        {
            if (TodasLasFacturas == null)
            {
                FacturasFiltradas = new ObservableCollection<FacturaClienteDTO>();
                return;
            }

            if (string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                FacturasFiltradas = new ObservableCollection<FacturaClienteDTO>(TodasLasFacturas);
            }
            else
            {
                var filtro = TextoBusqueda.Trim().ToLowerInvariant();
                var filtradas = TodasLasFacturas.Where(f =>
                    (f.Documento?.ToLowerInvariant().Contains(filtro) ?? false) ||
                    (f.Concepto?.ToLowerInvariant().Contains(filtro) ?? false));
                FacturasFiltradas = new ObservableCollection<FacturaClienteDTO>(filtradas);
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

        private void CheckBoxSeleccionarTodas_Changed(object sender, RoutedEventArgs e)
        {
            if (TodasLasFacturas == null)
            {
                return;
            }

            bool seleccionar = ((CheckBox)sender).IsChecked ?? false;

            foreach (var factura in TodasLasFacturas)
            {
                factura.Seleccionada = seleccionar;
            }
        }

        #endregion
    }
}
