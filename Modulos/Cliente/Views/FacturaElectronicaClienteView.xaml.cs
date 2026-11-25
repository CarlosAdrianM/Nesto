using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#652 (NestoAPI#903): los códigos DIR3 del cliente en la pestaña «Direcciones» de la ficha. Se pone en XAML con el cliente
    /// enlazado (<c>Empresa</c>, <c>Cliente</c>, <c>Contacto</c>) y crea su ViewModel con los servicios del contenedor. Lee la API al
    /// enseñarse y al cambiar de cliente (patrón de <see cref="DireccionesClienteView"/>).
    /// </summary>
    public partial class FacturaElectronicaClienteView : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(FacturaElectronicaClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(nameof(Cliente), typeof(string),
            typeof(FacturaElectronicaClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ContactoProperty = DependencyProperty.Register(nameof(Contacto), typeof(string),
            typeof(FacturaElectronicaClienteView), new PropertyMetadata(null, AlCambiarCliente));

        private bool _pendiente = true;

        public FacturaElectronicaClienteView()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            if (contenedor != null)
            {
                ViewModel = new FacturaElectronicaClienteViewModel(contenedor.Resolver<IServicioFacturae>(), contenedor.Resolver<IConfiguracion>());
                Raiz.DataContext = ViewModel;
            }
            else
            {
                Raiz.Visibility = Visibility.Collapsed;
            }
            IsVisibleChanged += (s, e) => CargarSiHaceFalta();
        }

        public FacturaElectronicaClienteViewModel ViewModel { get; }

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

        public string Contacto
        {
            get => (string)GetValue(ContactoProperty);
            set => SetValue(ContactoProperty, value);
        }

        private static void AlCambiarCliente(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var vista = (FacturaElectronicaClienteView)d;
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
            _ = ViewModel.CargarAsync(Empresa, Cliente, Contacto);
        }
    }
}
