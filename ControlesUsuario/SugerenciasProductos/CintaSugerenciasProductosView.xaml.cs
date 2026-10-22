using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Nesto#537 / Nesto#544: la cinta «Puede ofrecer» (los productos sugeridos con sus botones). El DataContext es un
    /// <see cref="SugerenciasProductos.CintaSugerenciasProductosViewModel"/>. La usan la plantilla de venta (fichas en una fila)
    /// y el rapport (fichas una debajo de otra, <see cref="EnColumna"/>, porque el panel del rapport es estrecho).
    /// </summary>
    public partial class CintaSugerenciasProductosView : UserControl
    {
        public static readonly DependencyProperty EnColumnaProperty =
            DependencyProperty.Register(nameof(EnColumna), typeof(bool), typeof(CintaSugerenciasProductosView), new PropertyMetadata(false));

        public CintaSugerenciasProductosView()
        {
            InitializeComponent();
        }

        /// <summary>True: una ficha debajo de otra. False (por defecto): todas en una fila.</summary>
        public bool EnColumna
        {
            get => (bool)GetValue(EnColumnaProperty);
            set => SetValue(EnColumnaProperty, value);
        }
    }
}
