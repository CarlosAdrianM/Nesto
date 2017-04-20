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
    /// Lógica de interacción para SelectorEmpresa.xaml
    /// </summary>
    public partial class SelectorEmpresa : UserControl, INotifyPropertyChanged
    {
        public SelectorEmpresa()
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
              typeof(SelectorEmpresa),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnConfiguracionChanged)));
        private static void OnConfiguracionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorEmpresa selector = (SelectorEmpresa)d;
            selector.cargarDatos();
        }

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
              typeof(SelectorEmpresa),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnSeleccionadaChanged)));

        private static void OnSeleccionadaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorEmpresa selector = (SelectorEmpresa)d;
            if (selector.listaEmpresas == null)
            {
                return;
            }
            if (selector.Seleccionada == null)
            {
                selector.empresaSeleccionada = null;
            }
            else
            {
                selector.empresaSeleccionada = selector.listaEmpresas.Where(l => l.empresa == selector.Seleccionada.Trim()).SingleOrDefault();
            }

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
              typeof(SelectorEmpresa),
              new UIPropertyMetadata("Seleccione una empresa:"));


        #endregion

        #region "Propiedades"
        private Empresa _empresaSeleccionada;
        public Empresa empresaSeleccionada
        {
            get
            {
                return _empresaSeleccionada;
            }

            set
            {
                _empresaSeleccionada = value;
                OnPropertyChanged("empresaSeleccionada");
                if (empresaSeleccionada != null)
                {
                    Seleccionada = empresaSeleccionada.empresa;
                }
            }
        }
        private ObservableCollection<Empresa> _listaEmpresas;
        public ObservableCollection<Empresa> listaEmpresas
        {
            get
            {
                return _listaEmpresas;
            }
            set
            {
                _listaEmpresas = value;
                OnPropertyChanged("listaEmpresas");
            }
        }

        public Visibility visibilidad
        {
            get
            {
                return Etiqueta == "" ? Visibility.Collapsed : Visibility.Visible;
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
                    string urlConsulta = "Empresas";


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaEmpresas = JsonConvert.DeserializeObject<ObservableCollection<Empresa>>(resultado);
                        foreach (Empresa empresa in listaEmpresas)
                        {
                            empresa.empresa = empresa.empresa.Trim();
                        }
                        if (Seleccionada != null)
                        {
                            empresaSeleccionada = listaEmpresas.Where(l => l.empresa == Seleccionada.Trim()).SingleOrDefault();
                        }
                    }
                }
                catch
                {
                    throw new Exception("No se pudieron leer las empresas");
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
