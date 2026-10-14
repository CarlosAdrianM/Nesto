using Nesto.Modules.Producto.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Enviar reposición a Algete (NestoAPI#553). La lógica está en <see cref="EnviarReposicionViewModel"/>; aquí solo
    /// se pone el foco en la cantidad de la línea que ha leído el lector y, al terminar de escribirla con Intro (o si lo
    /// leído no entra), se vuelve al cuadro del lector para la siguiente lectura.
    /// </summary>
    public partial class EnviarReposicionView : UserControl
    {
        public EnviarReposicionView(EnviarReposicionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            IsVisibleChanged += AlCambiarVisibilidad;
            viewModel.PedirFocoEnCantidad += EnfocarCantidad;
            viewModel.PedirFocoEnLector += EnfocarLector;
            Loaded += async (_, _) =>
            {
                if (DataContext is EnviarReposicionViewModel vm && vm.Lineas.Count == 0)
                {
                    await vm.CargarAsync();
                }
            };
        }

        private void EnfocarLector()
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (txtLectura?.IsVisible == true)
                {
                    _ = txtLectura.Focus();
                    txtLectura.SelectAll();
                }
            }, DispatcherPriority.Background);
        }

        /// <summary>Intro en la cantidad: se guarda (como siempre) y el cursor vuelve al lector para la siguiente lectura.</summary>
        private void GrdLineas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e?.Key != Key.Enter || grdLineas == null || grdLineas.CurrentColumn != colCantidad)
            {
                return;
            }
            _ = grdLineas.CommitEdit(DataGridEditingUnit.Row, true);
            e.Handled = true;
            EnfocarLector();
        }

        /// <summary>
        /// Sugerencia 581, reposición puntual: Intro en la cantidad la guarda y vuelve al lector; Supr (sin estar escribiendo)
        /// quita la línea seleccionada.
        /// </summary>
        private void GrdPuntual_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e == null || grdPuntual == null || DataContext is not EnviarReposicionViewModel vm)
            {
                return;
            }
            if (e.Key == Key.Enter && grdPuntual.CurrentColumn == colCantidadPuntual)
            {
                _ = grdPuntual.CommitEdit(DataGridEditingUnit.Row, true);
                e.Handled = true;
                EnfocarLector();
                return;
            }
            bool editando = e.OriginalSource is TextBox;
            if (e.Key == Key.Delete && !editando && grdPuntual.SelectedItem is LineaPuntualReposicion linea
                && vm.QuitarLineaPuntualCommand.CanExecute(linea))
            {
                vm.QuitarLineaPuntualCommand.Execute(linea);
                e.Handled = true;
            }
        }

        private void EnfocarCantidad(LineaEnviarReposicion linea)
        {
            if (linea == null)
            {
                return;
            }
            grdLineas.ScrollIntoView(linea, colCantidad);
            _ = Dispatcher.InvokeAsync(() =>
            {
                grdLineas.Focus();
                grdLineas.CurrentCell = new DataGridCellInfo(linea, colCantidad);
                grdLineas.BeginEdit();
            }, DispatcherPriority.Background);
        }

        /// <summary>Tras abrir la ficha desde la rejilla, al volver a esta pestaña el cursor vuelve al lector.</summary>
        private bool _volverAlLectorAlVerse;

        /// <summary>
        /// Sugerencia 599 (Paloma): doble clic en la referencia abre la ficha del producto. Solo en esa celda (en la fila, el
        /// doble clic en la cantidad es para escribirla). Al volver a esta pestaña, el cursor vuelve al lector.
        /// </summary>
        private void Referencia_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridCell celda || DataContext is not EnviarReposicionViewModel vm)
            {
                return;
            }
            string producto = celda.DataContext switch
            {
                LineaEnviarReposicion linea => linea.Producto,
                LineaPuntualReposicion linea => linea.Producto,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(producto) || !vm.AbrirFichaProductoCommand.CanExecute(producto))
            {
                return;
            }
            e.Handled = true;
            _volverAlLectorAlVerse = true;
            vm.AbrirFichaProductoCommand.Execute(producto);
        }

        private void AlCambiarVisibilidad(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_volverAlLectorAlVerse && e.NewValue is true)
            {
                _volverAlLectorAlVerse = false;
                EnfocarLector();
            }
        }
    }
}
