using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Lógica de interacción para ClienteView.xaml
    /// </summary>
    public partial class CrearClienteView : UserControl
    {
        public CrearClienteView(CrearClienteViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CrearClienteViewModel vm = (CrearClienteViewModel)DataContext;
            vm.PaginaActual = DatosFiscales;
            Keyboard.Focus(txtNif);
        }

        private void TxtNif_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.OriginalSource is UIElement uiElement)
            {
                e.Handled = true;
                uiElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void TxtNombre_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.OriginalSource is UIElement uiElement)
            {
                e.Handled = true;
                uiElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void TxtNif_GotFocus(object sender, RoutedEventArgs e)
        {
            txtNif.SelectAll();
        }
        
        private void TxtNombre_GotFocus(object sender, RoutedEventArgs e)
        {
            txtNombre.SelectAll();
        }

        private void TxtNif_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            txtNif.SelectAll();
        }

        private void TxtNombre_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            txtNombre.SelectAll();
        }
    }
}
