using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#523 (NestoAPI#623): «Origen» del cliente en la cabecera de la ficha. Se pone en XAML con el cliente enlazado
    /// (<c>Empresa</c>, <c>Cliente</c>) y crea su ViewModel con los servicios del contenedor (patrón DiaRepartoClienteView).
    /// Pregunta a la API al cambiar de cliente (si se ve). Con la API sin el origen se esconde por dentro.
    /// </summary>
    public partial class OrigenClienteView : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(OrigenClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(nameof(Cliente), typeof(string),
            typeof(OrigenClienteView), new PropertyMetadata(null, AlCambiarCliente));

        private bool _pendiente = true;

        public OrigenClienteView()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            if (contenedor != null)
            {
                ViewModel = new OrigenClienteViewModel(contenedor.Resolver<IOrigenesClienteService>(), contenedor.Resolver<IServicioDialogos>(),
                    contenedor.Resolver<IConfiguracion>());
                Raiz.DataContext = ViewModel;
            }
            IsVisibleChanged += (s, e) => CargarSiHaceFalta();
        }

        public OrigenClienteViewModel ViewModel { get; }

        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty);
            set => SetValue(EmpresaProperty, value);
        }

        public string Cliente
        {
            get => (string)GetValue(ClienteProperty);
            set => SetValue(ClienteProperty, value);
        }

        private static void AlCambiarCliente(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not OrigenClienteView vista)
            {
                return;
            }
            vista._pendiente = true;
            vista.CargarSiHaceFalta();
        }

        private void CargarSiHaceFalta()
        {
            // Null-safe en el teardown: al quitar la vista el DataContext pasa a null y Cliente vuelve a null.
            if (!_pendiente || !IsVisible || ViewModel == null || string.IsNullOrWhiteSpace(Cliente))
            {
                return;
            }
            _pendiente = false;
            _ = ViewModel.CargarAsync(Empresa, Cliente);
        }
    }
}
