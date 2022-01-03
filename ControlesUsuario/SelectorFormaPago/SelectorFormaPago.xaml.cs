using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorDireccionEntrega.xaml
    /// </summary>
    public partial class SelectorFormaPago : UserControl, INotifyPropertyChanged
    {
        public SelectorFormaPago()
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
              typeof(SelectorFormaPago),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnClienteChanged)));

        private static void OnClienteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorFormaPago selector = (SelectorFormaPago)d;
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
              typeof(SelectorFormaPago));

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
              typeof(SelectorFormaPago));


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
              typeof(SelectorFormaPago));


        #endregion

        #region "Propiedades"
        private FormaPago _formaPagoSeleccionada;
        public FormaPago formaPagoSeleccionada {
            get
            {
                return _formaPagoSeleccionada;
            }

            set
            {
                _formaPagoSeleccionada = value;
                OnPropertyChanged("formaPagoSeleccionada");
                if (formaPagoSeleccionada != null)
                {
                    Seleccionada = formaPagoSeleccionada.formaPago;
                }
            }
        }
        private ObservableCollection<FormaPago> _listaFormasPago;
        public ObservableCollection<FormaPago> listaFormasPago
        {
            get
            {
                return _listaFormasPago;
            }
            set
            {
                _listaFormasPago = value;
                OnPropertyChanged("listaFormasPago");
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
                    string urlConsulta = "FormasPago?empresa=" + Empresa;
                    if (Cliente != "")
                    {
                        urlConsulta += "&cliente=" + Cliente;
                    }

                    response = await client.GetAsync(urlConsulta);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaFormasPago = JsonConvert.DeserializeObject<ObservableCollection<FormaPago>>(resultado);
                        formaPagoSeleccionada = listaFormasPago.Where(l => l.formaPago == Seleccionada).SingleOrDefault();
                    }                    
                } catch
                {
                    throw new Exception("No se pudieron leer las formas de pago");
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
