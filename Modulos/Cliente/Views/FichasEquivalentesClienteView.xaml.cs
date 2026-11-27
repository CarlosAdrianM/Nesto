using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#876 (27/11/26): la pestaña «Fichas equivalentes» de la ficha. Se pone en XAML con el cliente enlazado (<c>Empresa</c>,
    /// <c>Cliente</c>) y crea su ViewModel con los servicios del contenedor. Lee la API al enseñarse y al cambiar de cliente (patrón de
    /// <see cref="FacturaElectronicaClienteView"/>).
    /// </summary>
    public partial class FichasEquivalentesClienteView : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(FichasEquivalentesClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(nameof(Cliente), typeof(string),
            typeof(FichasEquivalentesClienteView), new PropertyMetadata(null, AlCambiarCliente));

        private bool _pendiente = true;

        public FichasEquivalentesClienteView()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            if (contenedor != null)
            {
                ViewModel = new FichasEquivalentesViewModel(contenedor.Resolver<IServicioClientesEquivalentes>(),
                    contenedor.Resolver<IServicioDialogos>(), contenedor.Resolver<IServicioNavegacion>());
                Raiz.DataContext = ViewModel;
            }
            else
            {
                Raiz.Visibility = Visibility.Collapsed;
            }
            IsVisibleChanged += (s, e) => CargarSiHaceFalta();
        }

        public FichasEquivalentesViewModel ViewModel { get; }

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
            if (d is not FichasEquivalentesClienteView vista)
            {
                return;
            }
            vista._pendiente = true;
            vista.CargarSiHaceFalta();
        }

        private void CargarSiHaceFalta()
        {
            // Null-safe en el desmontaje (LimpiarVistaAlQuitar anula el DataContext y los bindings vuelven a null)
            if (!_pendiente || !IsVisible || ViewModel == null || string.IsNullOrWhiteSpace(Cliente))
            {
                return;
            }
            _pendiente = false;
            _ = ViewModel.CargarAsync(Empresa, Cliente);
        }
    }
}
