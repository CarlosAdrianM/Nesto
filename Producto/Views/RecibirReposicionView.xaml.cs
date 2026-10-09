using ControlesUsuario.Behaviors;
using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;
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
    }
}
