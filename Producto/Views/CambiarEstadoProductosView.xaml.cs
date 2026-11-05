using Nesto.Modules.Producto.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Nesto#624 (NestoAPI#738, sugerencia 793 de Manuel): cambiar el estado de muchos productos. La lógica está en
    /// <see cref="CambiarEstadoProductosViewModel"/> (al llegar lee los estados, IReceptorNavegacion).
    /// </summary>
    public partial class CambiarEstadoProductosView : UserControl
    {
        public CambiarEstadoProductosView(CambiarEstadoProductosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            // Una columna de Excel pegada en la casilla de una línea se quedaría en la primera: se añaden todas
            DataObject.AddPastingHandler(txtReferencia, AlPegarReferencias);
        }

        private void AlPegarReferencias(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                return;
            }
            string texto = e.SourceDataObject.GetData(DataFormats.UnicodeText, true) as string;
            if (texto == null || texto.IndexOfAny(new[] { '\r', '\n', '\t' }) < 0 || DataContext is not CambiarEstadoProductosViewModel vm)
            {
                return;
            }
            e.CancelCommand();
            vm.AnnadirReferencias((vm.ReferenciaNueva ?? string.Empty) + " " + texto);
        }
    }
}
