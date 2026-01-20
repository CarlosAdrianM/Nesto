using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Prism.Ioc;
using Nesto.Infrastructure.Shared;
using System.Collections.Generic;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorFormaPago.xaml
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

        private static async void OnClienteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorFormaPago selector = (SelectorFormaPago)d;
            await selector.cargarDatos();
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
        /// Gets or sets the Etiqueta para las llamadas a la API
        /// </summary>
        public string Etiqueta
        {
            get { return (string)GetValue(EtiquetaProperty); }
            set
            {
                SetValue(EtiquetaProperty, value);
            }
        }

        /// <summary>
        /// Identified the ETIQUETA dependency property
        /// </summary>
        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register("Etiqueta", typeof(string),
              typeof(SelectorFormaPago),
              new UIPropertyMetadata("Seleccione una forma pago:"));


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
            DependencyProperty.Register(nameof(Seleccionada), typeof(string),
              typeof(SelectorFormaPago), 
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnSeleccionadaChanged)));

        private static void OnSeleccionadaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorFormaPago selector = (SelectorFormaPago)d;
            if (selector == null || selector.listaFormasPago == null || !selector.listaFormasPago.Any() || e.NewValue == null || (selector.formaPagoSeleccionada != null && selector.formaPagoSeleccionada.formaPago == e.NewValue.ToString()))
            {
                return;
            }
            selector.formaPagoSeleccionada = selector.listaFormasPago.SingleOrDefault(f => f.formaPago == e.NewValue.ToString());
        }


        public Visibility VisibilidadEtiqueta
        {
            get { return (Visibility)GetValue(VisibilidadEtiquetaProperty); }
            set
            {
                SetValue(VisibilidadEtiquetaProperty, value);
            }
        }

        /// <summary>
        /// Identified the VISIBILIDADETIQUETA dependency property
        /// </summary>
        public static readonly DependencyProperty VisibilidadEtiquetaProperty =
            DependencyProperty.Register(nameof(VisibilidadEtiqueta), typeof(Visibility),
              typeof(SelectorFormaPago));


        /// <summary>
        /// Gets or sets the TotalPedido para las llamadas a la API
        /// </summary>
        public decimal TotalPedido
        {
            get { return (decimal)GetValue(TotalPedidoProperty); }
            set
            {
                SetValue(TotalPedidoProperty, value);
            }
        }
        /// <summary>
        /// Identified the TotalPedido dependency property
        /// </summary>
        public static readonly DependencyProperty TotalPedidoProperty =
            DependencyProperty.Register(nameof(TotalPedido), typeof(decimal),
              typeof(SelectorFormaPago),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnTotalPedidoChanged)));

        private static async void OnTotalPedidoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorFormaPago selector = (SelectorFormaPago)d;
            await selector.cargarDatos();
        }

        /// <summary>
        /// Gets or sets the FormaPagoCompleta para las llamadas a la API
        /// </summary>
        public FormaPago FormaPagoCompleta
        {
            get { return (FormaPago)GetValue(FormaPagoCompletaProperty); }
            set
            {
                SetValue(FormaPagoCompletaProperty, value);
            }
        }

        /// <summary>
        /// Identified the FormaPagoCompleta dependency property
        /// </summary>
        public static readonly DependencyProperty FormaPagoCompletaProperty =
            DependencyProperty.Register(nameof(FormaPagoCompleta), typeof(FormaPago),
              typeof(SelectorFormaPago),
              new FrameworkPropertyMetadata(null,
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnFormaPagoCompletaChanged)));

        private static void OnFormaPagoCompletaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorFormaPago selector = (SelectorFormaPago)d;
            if (selector == null || selector.FormaPagoCompleta == null)
            {
                return;
            }

            if (selector.FormaPagoCompleta.formaPago != selector.Seleccionada)
            {
                selector.Seleccionada = selector.FormaPagoCompleta.formaPago;
            }

            //if (selector.DataContext != null)
            //{
            //    BindingExpression bindingExpr = selector.GetBindingExpression(FormaPagoCompletaProperty);
            //    if (bindingExpr != null)
            //    {
            //        bindingExpr.UpdateTarget(); // Forzar la actualización del binding
            //    }
            //}
        }



        /// <summary>
        /// Gets or sets the TipoIva para las llamadas a la API
        /// </summary>
        public string TipoIva
        {
            get { return (string)GetValue(TipoIvaProperty); }
            set
            {
                SetValue(TipoIvaProperty, value);
            }
        }

        /// <summary>
        /// Identified the TipoIva dependency property
        /// </summary>
        public static readonly DependencyProperty TipoIvaProperty =
            DependencyProperty.Register(nameof(TipoIva), typeof(string),
              typeof(SelectorFormaPago),
              new FrameworkPropertyMetadata(new PropertyChangedCallback(OnTipoIvaChanged)));

        private static async void OnTipoIvaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorFormaPago selector = (SelectorFormaPago)d;
            await selector.cargarDatos();
        }


        /// <summary>
        /// Gets or sets the ListaFormasPago para las llamadas a la API
        /// </summary>
        public List<FormaPago> ListaFormasPago
        {
            get => (List<FormaPago>)GetValue(ListaFormasPagoProperty); 
            set => SetValue(ListaFormasPagoProperty, value);
        }

        /// <summary>
        /// Identified the ListaFormasPago dependency property
        /// </summary>
        public static readonly DependencyProperty ListaFormasPagoProperty =
            DependencyProperty.Register(nameof(ListaFormasPago), typeof(List<FormaPago>),
              typeof(SelectorFormaPago),
              new FrameworkPropertyMetadata(null,
                  FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                  new PropertyChangedCallback(OnListaFormasPagoChanged)));

        private static async void OnListaFormasPagoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SelectorFormaPago selector = (SelectorFormaPago)d;
        }


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
                if (_formaPagoSeleccionada != value && _formaPagoSeleccionada?.formaPago != value?.formaPago)
                {
                    _formaPagoSeleccionada = value;
                    
                    if (formaPagoSeleccionada != null && Seleccionada != formaPagoSeleccionada.formaPago)
                    {
                        Seleccionada = formaPagoSeleccionada.formaPago;
                    }
                    FormaPagoCompleta = formaPagoSeleccionada;
                    OnPropertyChanged(nameof(formaPagoSeleccionada));
                    //OnPropertyChanged(nameof(Seleccionada));
                    //OnPropertyChanged(nameof(FormaPagoCompleta));
                }
            }
        }


        private ObservableCollection<FormaPago> _listaFormasPago;
        public ObservableCollection<FormaPago> listaFormasPago
        {
            get => _listaFormasPago;
            set
            {
                _listaFormasPago = value;
                OnPropertyChanged("listaFormasPago");
                ListaFormasPago = _listaFormasPago.ToList();
            }
        }

        #endregion

        #region "Funciones Auxiliares"
        private async Task cargarDatos()
        {
            // Issue #274: Verificar que Configuracion no sea null antes de cargar datos
            if (Configuracion == null)
            {
                return;
            }

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(Configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "FormasPago?empresa=" + Empresa;
                    if (!string.IsNullOrEmpty(Cliente))
                    {
                        urlConsulta += "&cliente=" + Cliente;
                        if (TotalPedido > 0)
                        {
                            urlConsulta += $"&totalPedido={TotalPedido.ToString(CultureInfo.GetCultureInfo("en-US"))}&tipoIva={TipoIva}";
                        }
                    }

                    response = await client.GetAsync(urlConsulta);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        listaFormasPago = JsonConvert.DeserializeObject<ObservableCollection<FormaPago>>(resultado);
                        if (formaPagoSeleccionada?.formaPago != Seleccionada) {
                            formaPagoSeleccionada = listaFormasPago.Where(l => l.formaPago == Seleccionada).SingleOrDefault();
                        }
                    }                    
                } catch
                {
                    MessageBox.Show("No se pudieron leer las formas de pago");
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
            if (listaFormasPago is null || !listaFormasPago.Any())
            {
                cargarDatos();
            }
        }
    }
}
