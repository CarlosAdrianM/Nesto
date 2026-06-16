using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Prism.Ioc;


namespace ControlesUsuario
{
    public partial class SelectorSubgrupoProducto : UserControl
    {
        // Nesto#369: el HttpClient debe adjuntar el JWT para que el usuario salga en ELMAH.
        private readonly IClienteApiFactory _clienteApiFactory;

        public SelectorSubgrupoProducto()
        {
            InitializeComponent();
            Loaded += OnLoaded; // Cargar datos al inicializar el control

            try
            {
                _clienteApiFactory = ContainerLocator.Container.Resolve<IClienteApiFactory>();
            }
            catch
            {
                // Modo diseñador / sin contenedor: se usa el fallback en CrearClienteApi().
            }
        }

        // Nesto#369: usa la factoría (HttpClient con JWT) si hay contenedor; si no (diseñador),
        // cae al cliente sin token resolviendo la BaseAddress de IConfiguracion.
        private HttpClient CrearClienteApi()
        {
            if (_clienteApiFactory != null)
            {
                return _clienteApiFactory.Crear();
            }
            IConfiguracion configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
            return new HttpClient { BaseAddress = new Uri(configuracion.servidorAPI) };
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ListaSubgrupos == null) // Evitar recarga si ya se han cargado los datos
            {
                await CargarDatos();
            }
        }

        private async Task CargarDatos()
        {
            using (HttpClient client = CrearClienteApi())
            {
                try
                {
                    string urlConsulta = "Productos/Subgrupos";
                    HttpResponseMessage response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var subgrupos = JsonConvert.DeserializeObject<List<SubgrupoProductoDTO>>(resultado);


                        // Agregar la opción "(Todos los subgrupos)"
                        var opcionTodos = new SubgrupoProductoDTO { Grupo = string.Empty, Subgrupo = string.Empty, Nombre = "(Todos los subgrupos)" };
                        subgrupos.Insert(0, opcionTodos);
                        
                        // Asignar la lista completa a la DependencyProperty
                        ListaSubgrupos = subgrupos;

                        // Seleccionar la opción "(Todos los subgrupos)" por defecto
                        GrupoSubgrupoSeleccionado = opcionTodos.GrupoSubgrupo;
                    }
                    else
                    {
                        MessageBox.Show("No se pudieron leer los subgrupos.");
                    }
                }
                catch
                {
                    MessageBox.Show("Ocurrió un error al cargar los datos.");
                }
            }
        }





        public List<SubgrupoProductoDTO> ListaSubgrupos
        {
            get { return (List<SubgrupoProductoDTO>)GetValue(ListaSubgruposProperty); }
            set { SetValue(ListaSubgruposProperty, value); }
        }

        public static readonly DependencyProperty ListaSubgruposProperty =
            DependencyProperty.Register(nameof(ListaSubgrupos), typeof(List<SubgrupoProductoDTO>), typeof(SelectorSubgrupoProducto), new PropertyMetadata(null));

        public static readonly DependencyProperty GrupoSubgrupoSeleccionadoProperty =
            DependencyProperty.Register(
                nameof(GrupoSubgrupoSeleccionado),
                typeof(string),
                typeof(SelectorSubgrupoProducto),
                new FrameworkPropertyMetadata(
                    default(string),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)); // TwoWay por defecto

        public string GrupoSubgrupoSeleccionado
        {
            get => (string)GetValue(GrupoSubgrupoSeleccionadoProperty);
            set => SetValue(GrupoSubgrupoSeleccionadoProperty, value);
        }

    }
}
