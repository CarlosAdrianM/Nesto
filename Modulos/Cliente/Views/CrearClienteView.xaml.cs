using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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
            Loaded += (s, e) =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new Action(delegate () {
                    txtNif.Focus();         // Set Logical Focus
                    Keyboard.Focus(txtNif); // Set Keyboard Focus
                }));
            };
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is CrearClienteViewModel vm))
            {
                return;
            }
            vm.PaginaActual = DatosFiscales;
            // Nesto#480: al borrar la dirección verificada, el foco vuelve al campo: lo hace cada bloque de dirección
            // (BloqueDireccionAltaView), la fiscal y la de entrega (19/11/26).
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
            //txtNif.SelectAll();
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

        private void DatosGenerales_Enter(object sender, RoutedEventArgs e)
        {
            bloqueDireccionFiscal.EnfocarDireccion();
        }

        private void DatosFiscales_Enter(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
            new Action(delegate ()
            {
                // ELMAH 05/10/26 (Lidia): el foco diferido puede llegar con la vista ya cerrada o sin DataContext
                if (!(DataContext is CrearClienteViewModel ccvm))
                {
                    return;
                }
                if (!string.IsNullOrWhiteSpace(ccvm.ClienteNif) && ccvm.NombreIsEnabled)
                {
                    txtNombre.Focus();
                    Keyboard.Focus(txtNombre);
                } else
                {
                    txtNif.Focus();
                    Keyboard.Focus(txtNif);
                }
                if (ccvm.EsUnaModificacion && ccvm.PaginaAnterior == DatosFiscales)
                {
                    ccvm.PaginaActual = DatosGenerales;
                }
            }));
        }

        private void OptRecibo_Checked(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus(txtIban);
        }

        private void DatosComisiones_Enter(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new Action(delegate ()
                {
                    if (!(DataContext is CrearClienteViewModel ccvm))
                    {
                        return; // vista ya cerrada o sin DataContext (mismo caso que DatosFiscales_Enter)
                    }
                    if (ccvm.EsUnaModificacion && ccvm.PaginaAnterior != DatosPago)
                    {
                        ccvm.PaginaActual = DatosPago;
                    }
                    if (ccvm.EsUnaModificacion && ccvm.PaginaAnterior == DatosPago)
                    {
                        ccvm.PaginaActual = DatosGenerales;
                    }
                }));
        }
    }
}
