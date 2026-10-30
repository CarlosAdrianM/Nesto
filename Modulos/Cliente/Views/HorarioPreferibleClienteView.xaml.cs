using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#584: pestaña «Horario preferible» de la ficha del cliente. Se pone en XAML con el cliente enlazado
    /// (<c>Empresa</c>, <c>Cliente</c>, <c>Contacto</c>) y crea su ViewModel con los servicios del contenedor. Solo pregunta a la
    /// API cuando se ve (como la de Cupones).
    /// </summary>
    public partial class HorarioPreferibleClienteView : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(HorarioPreferibleClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(nameof(Cliente), typeof(string),
            typeof(HorarioPreferibleClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ContactoProperty = DependencyProperty.Register(nameof(Contacto), typeof(string),
            typeof(HorarioPreferibleClienteView), new PropertyMetadata(null, AlCambiarCliente));

        private bool _pendiente = true;

        public HorarioPreferibleClienteView()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            if (contenedor != null)
            {
                ViewModel = new HorarioPreferibleClienteViewModel(contenedor.Resolver<IHorarioPreferibleService>(), contenedor.Resolver<IServicioDialogos>());
                Raiz.DataContext = ViewModel;
            }
            IsVisibleChanged += (s, e) => CargarSiHaceFalta();
        }

        public HorarioPreferibleClienteViewModel ViewModel { get; }

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
            var vista = (HorarioPreferibleClienteView)d;
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
