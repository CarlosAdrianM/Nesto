using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
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
    /// Nesto#428 / NestoAPI#355: combo de países (muestra el nombre, guarda el código ISO-2).
    /// Patrón de SelectorBanco pero SIN dependencia de empresa (los países son globales).
    /// Datos de GET api/Paises.
    /// </summary>
    public partial class SelectorPais : UserControl, INotifyPropertyChanged
    {
        // Nesto#369: el HttpClient debe adjuntar el JWT para que el usuario salga en ELMAH.
        private readonly IClienteApiFactory _clienteApiFactory;

        public SelectorPais()
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

        public IConfiguracion Configuracion
        {
            get { return (IConfiguracion)GetValue(ConfiguracionProperty); }
            set { SetValue(ConfiguracionProperty, value); }
        }

        public static readonly DependencyProperty ConfiguracionProperty =
            DependencyProperty.Register(nameof(Configuracion), typeof(IConfiguracion),
              typeof(SelectorPais));

        public string Etiqueta
        {
            get { return (string)GetValue(EtiquetaProperty); }
            set { SetValue(EtiquetaProperty, value); }
        }

        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register(nameof(Etiqueta), typeof(string),
              typeof(SelectorPais),
              new UIPropertyMetadata("Seleccione un país:"));

        public Visibility VisibilidadEtiqueta
        {
            get { return (Visibility)GetValue(VisibilidadEtiquetaProperty); }
            set { SetValue(VisibilidadEtiquetaProperty, value); }
        }

        public static readonly DependencyProperty VisibilidadEtiquetaProperty =
            DependencyProperty.Register(nameof(VisibilidadEtiqueta), typeof(Visibility),
              typeof(SelectorPais));

        /// <summary>
        /// Código ISO-2 del país elegido (ej. "ES"), bindable en dos direcciones: el VM sigue
        /// trabajando con el código, el usuario ve el nombre.
        /// </summary>
        public string Seleccionado
        {
            get { return (string)GetValue(SeleccionadoProperty); }
            set { SetValue(SeleccionadoProperty, value); }
        }

        public static readonly DependencyProperty SeleccionadoProperty =
            DependencyProperty.Register(nameof(Seleccionado), typeof(string),
              typeof(SelectorPais),
              new FrameworkPropertyMetadata(null,
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnSeleccionadoChanged)));

        private static void OnSeleccionadoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorPais selector = (SelectorPais)d;
            if (selector == null || selector.listaPaises == null || !selector.listaPaises.Any()
                || (selector.paisSeleccionado != null && selector.paisSeleccionado.Codigo == ValorNormalizado(e.NewValue)))
            {
                return;
            }
            selector.paisSeleccionado = selector.listaPaises.FirstOrDefault(p => p.Codigo == ValorNormalizado(e.NewValue));
        }

        private static string ValorNormalizado(object valor) => (valor as string)?.Trim().ToUpperInvariant();

        #endregion

        #region "Propiedades"

        private PaisItem _paisSeleccionado;
        public PaisItem paisSeleccionado
        {
            get { return _paisSeleccionado; }
            set
            {
                if (_paisSeleccionado != value && _paisSeleccionado?.Codigo != value?.Codigo)
                {
                    _paisSeleccionado = value;
                    if (_paisSeleccionado != null && ValorNormalizado(Seleccionado) != _paisSeleccionado.Codigo)
                    {
                        Seleccionado = _paisSeleccionado.Codigo;
                    }
                    OnPropertyChanged(nameof(paisSeleccionado));
                }
            }
        }

        private ObservableCollection<PaisItem> _listaPaises;
        public ObservableCollection<PaisItem> listaPaises
        {
            get => _listaPaises;
            set
            {
                _listaPaises = value;
                OnPropertyChanged(nameof(listaPaises));
            }
        }

        #endregion

        #region "Funciones Auxiliares"

        private async Task cargarDatos()
        {
            if (Configuracion == null)
            {
                Debug.WriteLine("[SelectorPais] Configuracion=null, no se carga la lista");
                return;
            }

            using (HttpClient client = CrearClienteApi())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync("Paises");
                    string resultado = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        listaPaises = JsonConvert.DeserializeObject<ObservableCollection<PaisItem>>(resultado);
                        if (paisSeleccionado?.Codigo != ValorNormalizado(Seleccionado))
                        {
                            paisSeleccionado = listaPaises.FirstOrDefault(p => p.Codigo == ValorNormalizado(Seleccionado));
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[SelectorPais] Status={(int)response.StatusCode}, lista NO actualizada");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SelectorPais] EXCEPCIÓN al cargar: {ex}");
                    MessageBox.Show("No se pudieron leer los países");
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
            if (listaPaises is null || !listaPaises.Any())
            {
                _ = cargarDatos();
            }
        }
    }
}
