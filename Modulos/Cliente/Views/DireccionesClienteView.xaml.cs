using Nesto.Infrastructure.Contracts;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#879 (corte 3): pestaña «Direcciones» de la ficha del cliente. Se pone en XAML con el cliente enlazado (<c>Empresa</c>,
    /// <c>Cliente</c>) y crea su ViewModel con los servicios del contenedor. Lee la API al enseñarse y al cambiar de cliente.
    /// </summary>
    public partial class DireccionesClienteView : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(DireccionesClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(nameof(Cliente), typeof(string),
            typeof(DireccionesClienteView), new PropertyMetadata(null, AlCambiarCliente));

        private bool _pendiente = true;

        public DireccionesClienteView()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            if (contenedor != null)
            {
                ViewModel = new DireccionesClienteViewModel(contenedor.Resolver<IDireccionesClienteService>(), contenedor.Resolver<IServicioDialogos>());
                Raiz.DataContext = ViewModel;
            }
            IsVisibleChanged += (s, e) => CargarSiHaceFalta();
        }

        public DireccionesClienteViewModel ViewModel { get; }

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
            var vista = (DireccionesClienteView)d;
            vista._pendiente = true;
            vista.CargarSiHaceFalta();
        }

        private void CargarSiHaceFalta()
        {
            if (!_pendiente || !IsVisible || ViewModel == null)
            {
                return;
            }
            _pendiente = false;
            _ = ViewModel.CargarAsync(Empresa, Cliente);
        }
    }
}
