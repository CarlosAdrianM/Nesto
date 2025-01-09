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
        public SelectorSubgrupoProducto()
        {
            InitializeComponent();
            Loaded += OnLoaded; // Cargar datos al inicializar el control
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
            using (HttpClient client = new HttpClient())
            {
                IConfiguracion configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
                client.BaseAddress = new Uri(configuracion.servidorAPI);

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
