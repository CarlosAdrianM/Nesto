using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#518: pestaña «Cupones» de la ficha del cliente. Se pone en XAML con el cliente enlazado
    /// (<c>Empresa</c>, <c>Cliente</c>, <c>Contacto</c>) y crea su ViewModel con los servicios del contenedor. Solo pregunta a la
    /// API cuando se ve: si cambia el cliente con la pestaña escondida, lo hace al enseñarla.
    /// </summary>
    public partial class CuponesClienteView : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(CuponesClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(nameof(Cliente), typeof(string),
            typeof(CuponesClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ContactoProperty = DependencyProperty.Register(nameof(Contacto), typeof(string),
            typeof(CuponesClienteView), new PropertyMetadata(null, AlCambiarCliente));

        private bool _pendiente = true;

        public CuponesClienteView()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            if (contenedor != null)
            {
                ViewModel = new CuponesClienteViewModel(contenedor.Resolver<IServicioCupones>(), contenedor.Resolver<IServicioDialogos>(),
                    contenedor.Resolver<IConfiguracion>());
                Raiz.DataContext = ViewModel;
            }
            IsVisibleChanged += (s, e) => CargarSiHaceFalta();
        }

        public CuponesClienteViewModel ViewModel { get; }

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
            var vista = (CuponesClienteView)d;
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
