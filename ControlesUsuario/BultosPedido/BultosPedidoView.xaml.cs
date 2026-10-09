using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Nesto#522: los bultos del packing de Ariadna de un pedido (número, peso, quién y cuándo) con «Ver foto»,
    /// «Descargar» y «Copiar enlace para el cliente». El DataContext es un <see cref="BultosPedido.BultosPedidoViewModel"/>.
    /// </summary>
    public partial class BultosPedidoView : UserControl
    {
        public BultosPedidoView()
        {
            InitializeComponent();
        }
    }
}
