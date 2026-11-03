using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#606 (NestoAPI#799): «Día de reparto propio» del contacto en la cabecera de la ficha del cliente. Se pone en XAML con
    /// el cliente enlazado (<c>Empresa</c>, <c>Cliente</c>, <c>Contacto</c>) y crea su ViewModel con los servicios del contenedor.
    /// Pregunta a la API al cambiar de contacto (si se ve). Con la API vieja (404) se esconde por dentro.
    /// </summary>
    public partial class DiaRepartoClienteView : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(DiaRepartoClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(nameof(Cliente), typeof(string),
            typeof(DiaRepartoClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ContactoProperty = DependencyProperty.Register(nameof(Contacto), typeof(string),
            typeof(DiaRepartoClienteView), new PropertyMetadata(null, AlCambiarCliente));

        private bool _pendiente = true;

        public DiaRepartoClienteView()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            if (contenedor != null)
            {
                ViewModel = new DiaRepartoClienteViewModel(contenedor.Resolver<IDiaRepartoClienteService>(), contenedor.Resolver<IServicioDialogos>(),
                    contenedor.Resolver<IConfiguracion>());
                Raiz.DataContext = ViewModel;
            }
            IsVisibleChanged += (s, e) => CargarSiHaceFalta();
        }

        public DiaRepartoClienteViewModel ViewModel { get; }

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
            var vista = (DiaRepartoClienteView)d;
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
