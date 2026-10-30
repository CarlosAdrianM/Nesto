using ControlesUsuario.Behaviors;
using Nesto.Modules.Producto.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>Recibir reposición (NestoAPI#553). Sin lógica: todo en <see cref="RecibirReposicionViewModel"/>.</summary>
    public partial class RecibirReposicionView : UserControl
    {
        public RecibirReposicionView(RecibirReposicionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            IsVisibleChanged += AlCambiarVisibilidad;
            viewModel.PedirFocoEnLector += EnfocarLector;
            Loaded += async (_, _) =>
            {
                if (DataContext is RecibirReposicionViewModel vm && vm.Pendientes.Count == 0)
                {
                    await vm.CargarAsync();
                }
            };
        }

        /// <summary>Tras cada lectura el cursor vuelve al lector (también después de elegir en el buscador).</summary>
        private void EnfocarLector()
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (txtLectura?.IsVisible == true)
                {
                    _ = txtLectura.Focus();
                }
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// Sugerencia 545: el producto elegido en el buscador del «Lector» cuenta como una lectura. Con Intro, el Intro
        /// llega después al LeerLecturaCommand con el cuadro ya vacío, así que no suma dos veces.
        /// </summary>
        private void Lector_ProductoElegido(object sender, AutocompleteSeleccionEventArgs e)
        {
            if (DataContext is RecibirReposicionViewModel vm && e?.Item != null)
            {
                vm.LeerProductoElegido(e.Item.Id, e.Item.Texto);
            }
        }

        /// <summary>NestoAPI#762: la línea elegida tecleando su referencia (o con «Sin etiqueta») se ve en la rejilla.</summary>
        private void GrdLineas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid rejilla && rejilla.SelectedItem != null)
            {
                rejilla.ScrollIntoView(rejilla.SelectedItem);
            }
        }

        /// <summary>Tras abrir la ficha desde la rejilla, al volver a esta pestaña el cursor vuelve al lector.</summary>
        private bool _volverAlLectorAlVerse;

        /// <summary>
        /// Sugerencia 599 (Paloma): doble clic en la referencia abre la ficha del producto. Solo en esa celda (en la fila, el
        /// doble clic en la cantidad es para escribirla). Al volver a esta pestaña, el cursor vuelve al lector.
        /// </summary>
        private void Referencia_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridCell celda || DataContext is not RecibirReposicionViewModel vm)
            {
                return;
            }
            string producto = celda.DataContext switch
            {
                LineaRecibirReposicion linea => linea.Producto,
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
            // Ariadna#51: al volver a la pestaña, lo leído en los demás equipos (por si se perdió algún aviso)
            if (e.NewValue is true && DataContext is RecibirReposicionViewModel vm)
            {
                _ = vm.RecargarLecturasAsync();
            }
            if (_volverAlLectorAlVerse && e.NewValue is true)
            {
                _volverAlLectorAlVerse = false;
                EnfocarLector();
            }
        }
    }
}
