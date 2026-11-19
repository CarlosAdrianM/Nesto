using ControlesUsuario.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#591: mantenimiento de eventos con señal reembolsable.</summary>
    public partial class MantenimientoEventosView : UserControl
    {
        // Sugerencia 1362: lo que el usuario deja con los separadores, para la sesión (al volver a abrir Eventos se ve igual).
        // Es estado de la vista, no del ViewModel; null = como viene en el XAML.
        private static GridLength? _altoEventos;
        private static GridLength? _altoDetalle;
        private static double? _anchoFicha;

        public MantenimientoEventosView(MantenimientoEventosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            RestaurarSeparadores();
        }

        private void RestaurarSeparadores()
        {
            if (_altoEventos.HasValue && _altoDetalle.HasValue && filaEventos != null && filaDetalle != null)
            {
                filaEventos.Height = _altoEventos.Value;
                filaDetalle.Height = _altoDetalle.Value;
            }
            if (_anchoFicha.HasValue && columnaFicha != null)
            {
                columnaFicha.MaxWidth = double.PositiveInfinity;
                columnaFicha.Width = new GridLength(_anchoFicha.Value);
            }
        }

        /// <summary>La ficha nace con 480 como mucho; al mover el separador se puede ensanchar todo lo que se quiera.</summary>
        private void SeparadorInscritos_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (columnaFicha == null)
            {
                return;
            }
            double actual = columnaFicha.ActualWidth;
            columnaFicha.MaxWidth = double.PositiveInfinity;
            if (actual > 0)
            {
                columnaFicha.Width = new GridLength(actual);
            }
        }

        private void Separador_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (filaEventos != null && filaDetalle != null)
            {
                _altoEventos = filaEventos.Height;
                _altoDetalle = filaDetalle.Height;
            }
            if (columnaFicha != null && sender == separadorInscritos && columnaFicha.ActualWidth > 0)
            {
                _anchoFicha = columnaFicha.ActualWidth;
            }
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
