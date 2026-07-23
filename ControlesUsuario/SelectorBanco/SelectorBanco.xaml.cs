using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Nesto#425: combo de bancos por empresa (muestra el nombre, guarda el código).
    /// Patrón calcado de SelectorFormaPago; datos de GET api/Bancos/Selector.
    /// </summary>
    public partial class SelectorBanco : UserControl, INotifyPropertyChanged
    {
        // Nesto#369: el HttpClient debe adjuntar el JWT para que el usuario salga en ELMAH.
        private readonly IClienteApiFactory _clienteApiFactory;

        public SelectorBanco()
        {
            InitializeComponent();

            GridPrincipal.DataContext = this;

            try
            {
                _clienteApiFactory = ContainerLocator.Container.Resolve<IClienteApiFactory>();
            }
            catch
            {
                // Modo diseñador / sin contenedor: se usa el fallback en CrearClienteApi().
            }
        }

        private HttpClient CrearClienteApi()
        {
            return _clienteApiFactory != null
                ? _clienteApiFactory.Crear()
                : new HttpClient { BaseAddress = new Uri(Configuracion.servidorAPI) };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region Dependency Properties

        /// <summary>
        /// Gets or sets the Configuracion para las llamadas a la API
        /// </summary>
        public IConfiguracion Configuracion
        {
            get { return (IConfiguracion)GetValue(ConfiguracionProperty); }
            set { SetValue(ConfiguracionProperty, value); }
        }

        public static readonly DependencyProperty ConfiguracionProperty =
            DependencyProperty.Register(nameof(Configuracion), typeof(IConfiguracion),
              typeof(SelectorBanco));

        /// <summary>
        /// Gets or sets the EMPRESA para las llamadas a la API (recarga la lista al cambiar)
        /// </summary>
        public string Empresa
        {
            get { return (string)GetValue(EmpresaProperty); }
            set { SetValue(EmpresaProperty, value); }
        }

        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register(nameof(Empresa), typeof(string),
              typeof(SelectorBanco),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnEmpresaChanged)));

        private static async void OnEmpresaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorBanco selector = (SelectorBanco)d;
            await selector.cargarDatos();
        }

        /// <summary>
        /// Etiqueta sobre el combo (ocultable con VisibilidadEtiqueta)
        /// </summary>
        public string Etiqueta
        {
            get { return (string)GetValue(EtiquetaProperty); }
            set { SetValue(EtiquetaProperty, value); }
        }

        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register(nameof(Etiqueta), typeof(string),
              typeof(SelectorBanco),
              new UIPropertyMetadata("Seleccione un banco:"));

        public Visibility VisibilidadEtiqueta
        {
            get { return (Visibility)GetValue(VisibilidadEtiquetaProperty); }
            set { SetValue(VisibilidadEtiquetaProperty, value); }
        }

        public static readonly DependencyProperty VisibilidadEtiquetaProperty =
            DependencyProperty.Register(nameof(VisibilidadEtiqueta), typeof(Visibility),
              typeof(SelectorBanco));

        /// <summary>
        /// Código del banco elegido (Bancos.Número sin padding), bindable en dos direcciones:
        /// el VM sigue trabajando con el código (ej. "5"), el usuario ve el nombre.
        /// </summary>
        public string Seleccionado
        {
            get { return (string)GetValue(SeleccionadoProperty); }
            set { SetValue(SeleccionadoProperty, value); }
        }

        public static readonly DependencyProperty SeleccionadoProperty =
            DependencyProperty.Register(nameof(Seleccionado), typeof(string),
              typeof(SelectorBanco),
              new FrameworkPropertyMetadata(null,
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnSeleccionadoChanged)));

        private static void OnSeleccionadoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorBanco selector = (SelectorBanco)d;
            if (selector == null || selector.listaBancos == null || !selector.listaBancos.Any()
                || (selector.bancoSeleccionado != null && selector.bancoSeleccionado.Numero == ValorSinPadding(e.NewValue)))
            {
                return;
            }
            selector.bancoSeleccionado = selector.listaBancos.SingleOrDefault(b => b.Numero == ValorSinPadding(e.NewValue));
        }

        // El VM puede traer el código con el padding del char de BD ("5  "): normalizar siempre.
        private static string ValorSinPadding(object valor) => (valor as string)?.Trim();

        #endregion

        #region "Propiedades"

        private BancoItem _bancoSeleccionado;
        public BancoItem bancoSeleccionado
        {
            get { return _bancoSeleccionado; }
            set
            {
                if (_bancoSeleccionado != value && _bancoSeleccionado?.Numero != value?.Numero)
                {
                    _bancoSeleccionado = value;
                    if (_bancoSeleccionado != null && ValorSinPadding(Seleccionado) != _bancoSeleccionado.Numero)
                    {
                        Seleccionado = _bancoSeleccionado.Numero;
                    }
                    OnPropertyChanged(nameof(bancoSeleccionado));
                }
            }
        }

        private ObservableCollection<BancoItem> _listaBancos;
        public ObservableCollection<BancoItem> listaBancos
        {
            get => _listaBancos;
            set
            {
                _listaBancos = value;
                OnPropertyChanged(nameof(listaBancos));
            }
        }

        #endregion

        #region "Funciones Auxiliares"

        private async Task cargarDatos()
        {
            if (Configuracion == null)
            {
                Debug.WriteLine("[SelectorBanco] Configuracion=null, no se carga la lista");
                return;
            }

            using (HttpClient client = CrearClienteApi())
            {
                string urlConsulta = null;
                try
                {
                    urlConsulta = "Bancos/Selector?empresa=" + Uri.EscapeDataString(Empresa?.Trim() ?? string.Empty);
                    HttpResponseMessage response = await client.GetAsync(urlConsulta);
                    string resultado = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        listaBancos = JsonConvert.DeserializeObject<ObservableCollection<BancoItem>>(resultado);
                        if (bancoSeleccionado?.Numero != ValorSinPadding(Seleccionado))
                        {
                            bancoSeleccionado = listaBancos.SingleOrDefault(b => b.Numero == ValorSinPadding(Seleccionado));
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[SelectorBanco] Status={(int)response.StatusCode}, lista NO actualizada");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SelectorBanco] EXCEPCIÓN al cargar (url={urlConsulta}): {ex}");
                    MessageBox.Show("No se pudieron leer los bancos");
                }
            }
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Configuracion is null)
            {
                Configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
            }
            if (Empresa is null)
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO;
            }
            if (listaBancos is null || !listaBancos.Any())
            {
                _ = cargarDatos();
            }
        }
    }
}
