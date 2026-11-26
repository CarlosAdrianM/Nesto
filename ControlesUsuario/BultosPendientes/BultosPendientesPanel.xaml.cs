using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario.BultosPendientes
{
    /// <summary>
    /// NestoAPI#880 (corte 2): los bultos pendientes dentro de otra pantalla. Sin <see cref="Pedido"/>, todos (Agencias); con él, los de
    /// ese pedido y solo si hay alguno (pestaña «Entrega»). Patrón de los controles de la ficha (FacturaElectronicaClienteView).
    /// </summary>
    public partial class BultosPendientesPanel : UserControl
    {
        public static readonly DependencyProperty EmpresaProperty = DependencyProperty.Register(nameof(Empresa), typeof(string),
            typeof(BultosPendientesPanel), new PropertyMetadata(null, AlCambiar));
        public static readonly DependencyProperty PedidoProperty = DependencyProperty.Register(nameof(Pedido), typeof(int?),
            typeof(BultosPendientesPanel), new PropertyMetadata(null, AlCambiar));
        public static readonly DependencyProperty EncabezadoProperty = DependencyProperty.Register(nameof(Encabezado), typeof(string),
            typeof(BultosPendientesPanel), new PropertyMetadata(null));

        private bool _pendiente = true;

        public BultosPendientesPanel()
        {
            InitializeComponent();
            IResolutorNesto contenedor = Contenedor.Actual;
            if (contenedor != null)
            {
                ViewModel = new BultosPendientesViewModel(contenedor.Resolver<IServicioBultosPendientes>(), contenedor.Resolver<IServicioDialogos>());
                Raiz.DataContext = ViewModel;
            }
            else
            {
                Raiz.Visibility = Visibility.Collapsed;
            }
            IsVisibleChanged += (s, e) => CargarSiHaceFalta();
        }

        public BultosPendientesViewModel ViewModel { get; }

        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty);
            set => SetValue(EmpresaProperty, value);
        }

        public int? Pedido
        {
            get => (int?)GetValue(PedidoProperty);
            set => SetValue(PedidoProperty, value);
        }

        public string Encabezado
        {
            get => (string)GetValue(EncabezadoProperty);
            set => SetValue(EncabezadoProperty, value);
        }

        private static void AlCambiar(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (BultosPendientesPanel)d;
            panel._pendiente = true;
            panel.CargarSiHaceFalta();
        }

        private void CargarSiHaceFalta()
        {
            if (!_pendiente || !IsVisible || ViewModel == null)
            {
                return;
            }
            _pendiente = false;
            _ = ViewModel.CargarAsync(Empresa, Pedido);
        }
    }
}
