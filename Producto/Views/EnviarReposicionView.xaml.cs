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
                    _ = await vm.RecargarAlVolverAsync();
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

        private async void AlCambiarVisibilidad(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not true)
            {
                // Incidencia 687 (Paloma): al irse a otra pestaña no se deja ninguna celda a medio editar (se perdía al
                // volver y las bajadas siguientes no llegaban al servidor)
                TerminarEdicion(grdLineas);
                TerminarEdicion(grdPuntual);
                return;
            }
            if (_volverAlLectorAlVerse)
            {
                _volverAlLectorAlVerse = false;
                EnfocarLector();
            }
            // Incidencia 687: con una reposición cargada se rehace con lo que tiene el servidor (sin nada en marcha)
            if (DataContext is EnviarReposicionViewModel vm && vm.HayReposicion)
            {
                _ = await vm.RecargarAlVolverAsync();
            }
        }

        /// <summary>
        /// Confirma lo que haya a medio escribir en la rejilla (en la de la reposición eso lo manda al servidor; en la de la
        /// puntual se queda en la línea, que solo está en la pantalla). Si no se puede confirmar (lo tecleado no es un número),
        /// se descarta solo esa edición: la línea se queda con la cantidad que tenía.
        /// </summary>
        private static void TerminarEdicion(DataGrid rejilla)
        {
            if (rejilla == null)
            {
                return;
            }
            try
            {
                if (!rejilla.CommitEdit(DataGridEditingUnit.Row, true))
                {
                    _ = rejilla.CancelEdit(DataGridEditingUnit.Row);
                }
            }
            catch (System.InvalidOperationException)
            {
                // La rejilla ya no está en condiciones de editar (se está descargando): nada que confirmar
            }
        }
    }
}
