using Nesto.Infrastructure.Contracts;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#517: las inscripciones a eventos del cliente, para la pestaña «Eventos» de la ficha (ClienteComercial). Se crea
    /// desde XAML, así que el ViewModel se resuelve aquí del contenedor (como SelectorCliente). La ficha le pasa el cliente
    /// por <see cref="Empresa"/>, <see cref="Cliente"/>, <see cref="Contacto"/> y <see cref="Nombre"/>.
    /// </summary>
    public partial class InscripcionesClienteView : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(
            nameof(Empresa), typeof(string), typeof(InscripcionesClienteView), new PropertyMetadata(null, AlCambiarCliente));

        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(
            nameof(Cliente), typeof(string), typeof(InscripcionesClienteView), new PropertyMetadata(null, AlCambiarCliente));

        public static readonly DependencyProperty ContactoProperty = DependencyProperty.Register(
            nameof(Contacto), typeof(string), typeof(InscripcionesClienteView), new PropertyMetadata(null, AlCambiarCliente));

        public static readonly DependencyProperty NombreProperty = DependencyProperty.Register(
            nameof(Nombre), typeof(string), typeof(InscripcionesClienteView), new PropertyMetadata(null, AlCambiarCliente));

        private readonly InscripcionesClienteViewModel _viewModel;

        public InscripcionesClienteView()
            : this(Contenedor.Actual?.Resolver<InscripcionesClienteViewModel>())
        {
        }

        public InscripcionesClienteView(InscripcionesClienteViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            Raiz.DataContext = viewModel;
            IsVisibleChanged += (s, e) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.ALaVista = IsVisible;
                }
            };
        }

        public string Empresa { get => (string)GetValue(EmpresaProperty); set => SetValue(EmpresaProperty, value); }
        public string Cliente { get => (string)GetValue(ClienteProperty); set => SetValue(ClienteProperty, value); }
        public string Contacto { get => (string)GetValue(ContactoProperty); set => SetValue(ContactoProperty, value); }
        public string Nombre { get => (string)GetValue(NombreProperty); set => SetValue(NombreProperty, value); }

        private static void AlCambiarCliente(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InscripcionesClienteView vista)
            {
                vista._viewModel?.CambiarCliente(vista.Empresa, vista.Cliente, vista.Contacto, vista.Nombre);
            }
        }
    }
}
