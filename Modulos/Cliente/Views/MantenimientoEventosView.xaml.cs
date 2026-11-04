using ControlesUsuario.Behaviors;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#591: mantenimiento de eventos con señal reembolsable.</summary>
    public partial class MantenimientoEventosView : UserControl
    {
        public MantenimientoEventosView(MantenimientoEventosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>
        /// Nesto#608: al elegir un producto de la lista del autocompletado (con el ratón o con Intro) se añade a «Hay que
        /// comprar», igual que si se hubiera escrito su referencia y pulsado Intro.
        /// </summary>
        private void ProductoRequerido_ItemSeleccionado(object sender, AutocompleteSeleccionEventArgs e)
        {
            if (DataContext is MantenimientoEventosViewModel viewModel && viewModel.AnadirProductoRequeridoCommand.CanExecute(null))
            {
                viewModel.AnadirProductoRequeridoCommand.Execute(null);
            }
        }
    }
}
