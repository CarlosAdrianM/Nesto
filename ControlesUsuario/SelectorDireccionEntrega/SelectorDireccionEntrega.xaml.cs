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
    /// Lógica de interacción para SelectorDireccionEntrega.xaml
    /// </summary>
    public partial class SelectorDireccionEntrega : UserControl, INotifyPropertyChanged
    {
        public SelectorDireccionEntrega()
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
              typeof(SelectorDireccionEntrega));

        

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
              typeof(SelectorDireccionEntrega));



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
        private async void cargarDatos()
        {
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
                        direccionEntregaSeleccionada = listaDireccionesEntrega.Where(l => l.contacto == Seleccionada).SingleOrDefault();
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

    }
}
