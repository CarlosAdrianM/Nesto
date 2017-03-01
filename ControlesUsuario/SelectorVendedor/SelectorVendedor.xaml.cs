using Nesto.Contratos;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using ControlesUsuario.Models;
using System.Collections.ObjectModel;
using System.Net.Http;
using Newtonsoft.Json;


namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorVendedor.xaml
    /// </summary>
    public partial class SelectorVendedor : UserControl, INotifyPropertyChanged
    {
        public SelectorVendedor()
        {
            InitializeComponent();
            GridPrincipal.DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region Dependency Properties

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
            DependencyProperty.Register("Configuracion", typeof(IConfiguracion),
              typeof(SelectorVendedor));

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
              typeof(SelectorVendedor),
                new FrameworkPropertyMetadata(new PropertyChangedCallback(OnEmpresaChanged)));

        private static void OnEmpresaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorVendedor selector = (SelectorVendedor)d;
            selector.cargarDatos();
        }


        /// <summary>
        /// Gets or sets the SELECCIONADA para las llamadas a la API
        /// </summary>
        public string Seleccionado
        {
            get { return (string)GetValue(SeleccionadoProperty); }
            set
            {
                SetValue(SeleccionadoProperty, value);
            }
        }

        /// <summary>
        /// Identified the SELECCIONADA dependency property
        /// </summary>
        public static readonly DependencyProperty SeleccionadoProperty =
            DependencyProperty.Register("Seleccionado", typeof(string),
              typeof(SelectorVendedor),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnSeleccionadoChanged)));

        private static void OnSeleccionadoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorVendedor selector = (SelectorVendedor)d;
            if (selector.listaVendedores == null)
            {
                return;
            }
            selector.vendedorSeleccionado = selector.listaVendedores.Where(l => l.vendedor == selector.Seleccionado.Trim()).SingleOrDefault();
        }



        public string Etiqueta
        {
            get { return (string)GetValue(EtiquetaProperty); }
            set
            {
                SetValue(EtiquetaProperty, value);
            }
        }

        /// <summary>
        /// Identified the EMPRESA dependency property
        /// </summary>
        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register("Etiqueta", typeof(string),
              typeof(SelectorVendedor),
              new UIPropertyMetadata("Seleccione un vendedor:"));
        

        #endregion

        #region "Propiedades"
        private Vendedor _vendedorSeleccionado;
        public Vendedor vendedorSeleccionado
        {
            get
            {
                return _vendedorSeleccionado;
            }

            set
            {
                _vendedorSeleccionado = value;
                OnPropertyChanged("vendedorSeleccionado");
            }
        }
        private ObservableCollection<Vendedor> _listaVendedores;
        public ObservableCollection<Vendedor> listaVendedores
        {
            get
            {
                return _listaVendedores;
            }
            set
            {
                _listaVendedores = value;
                OnPropertyChanged("listaVendedores");
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
                    string urlConsulta = "Vendedores?empresa=" + Empresa;
                    

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaVendedores = JsonConvert.DeserializeObject<ObservableCollection<Vendedor>>(resultado);
                        vendedorSeleccionado = listaVendedores.Where(l => l.vendedor == Seleccionado.Trim()).SingleOrDefault();
                    }
                }
                catch
                {
                    throw new Exception("No se pudieron leer los vendedores");
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
