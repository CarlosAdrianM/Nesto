using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#876 (corte nuevo): «Copiar del cliente anterior…» y «Pasar pedidos pendientes…» en la ficha. Se pone en XAML con el
    /// cliente enlazado (<c>Empresa</c>, <c>Cliente</c>, <c>ClienteAnterior</c>) y crea su ViewModel con los servicios del contenedor
    /// (patrón de <see cref="FacturaElectronicaClienteView"/>). Sin cliente anterior no se ve. No lee la API hasta que se pulsa.
    /// </summary>
    public partial class CambioFiscalClienteView : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(CambioFiscalClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ClienteProperty = DependencyProperty.Register(nameof(Cliente), typeof(string),
            typeof(CambioFiscalClienteView), new PropertyMetadata(null, AlCambiarCliente));
        public static readonly DependencyProperty ClienteAnteriorProperty = DependencyProperty.Register(nameof(ClienteAnterior), typeof(string),
            typeof(CambioFiscalClienteView), new PropertyMetadata(null, AlCambiarCliente));

        public CambioFiscalClienteView()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            if (contenedor != null)
            {
                ViewModel = new CambioFiscalClienteViewModel(contenedor.Resolver<IServicioCambioFiscal>(), contenedor.Resolver<IServicioDialogos>(),
                    contenedor.Resolver<IConfiguracion>());
                Raiz.DataContext = ViewModel;
            }
            else
            {
                Raiz.Visibility = Visibility.Collapsed;
            }
        }

        public CambioFiscalClienteViewModel ViewModel { get; }

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

        public string ClienteAnterior
        {
            get => (string)GetValue(ClienteAnteriorProperty);
            set => SetValue(ClienteAnteriorProperty, value);
        }

        private static void AlCambiarCliente(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var vista = (CambioFiscalClienteView)d;
            vista.ViewModel?.Cargar(vista.Empresa, vista.Cliente, vista.ClienteAnterior);
        }
    }
}
