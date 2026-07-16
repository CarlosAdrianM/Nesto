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
        public CrearClienteView()
        {
            InitializeComponent();
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
            CrearClienteViewModel vm = (CrearClienteViewModel)DataContext;
            vm.PaginaActual = DatosFiscales;
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
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
            new Action(delegate ()
            {
                txtDireccion.Focus();         // Set Logical Focus
                Keyboard.Focus(txtDireccion); // Set Keyboard Focus
            }));
        }

        private void DatosFiscales_Enter(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
            new Action(delegate ()
            {
                CrearClienteViewModel ccvm = (CrearClienteViewModel)DataContext;
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

        // Nesto#409: navegación por teclado del combo de sugerencias SIN sacar el foco del
        // TextBox: ↑/↓ mueven el resaltado, Intro aplica la resaltada, Escape cierra.
        private void TxtDireccion_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(DataContext is CrearClienteViewModel vm) || !vm.HaySugerenciasDireccion)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Down:
                    vm.MoverSeleccionSugerencias(1);
                    lstSugerenciasDireccion.ScrollIntoView(vm.SugerenciaDireccionSeleccionada);
                    e.Handled = true;
                    break;
                case Key.Up:
                    vm.MoverSeleccionSugerencias(-1);
                    lstSugerenciasDireccion.ScrollIntoView(vm.SugerenciaDireccionSeleccionada);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    vm.CerrarSugerenciasDireccion();
                    e.Handled = true;
                    break;
                case Key.Enter:
                    // Aplica la resaltada; el KeyUp de siempre moverá el foco al siguiente campo
                    _ = vm.AplicarSugerenciaSeleccionadaAsync();
                    break;
            }
        }

        // Nesto#409: el clic en una sugerencia la aplica (la selección por sí sola es solo resaltado)
        private async void LstSugerenciasDireccion_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is CrearClienteViewModel vm)
            {
                _ = await vm.AplicarSugerenciaSeleccionadaAsync();
                Keyboard.Focus(txtDireccion); // devolver el teclado al TextBox
            }
        }

        private void TxtDireccion_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.OriginalSource is UIElement uiElement)
            {
                e.Handled = true;
                uiElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void TxtCodigoPostal_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.OriginalSource is UIElement uiElement)
            {
                e.Handled = true;
                uiElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void TxtTelefono_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.OriginalSource is UIElement uiElement)
            {
                e.Handled = true;
                uiElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void OptRecibo_Checked(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus(txtIban);
        }

        private void txtDireccionAdicional_GotFocus(object sender, RoutedEventArgs e)
        {
            txtDireccionAdicional.SelectAll();
        }

        private void txtDireccionAdicional_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.OriginalSource is UIElement uiElement)
            {
                e.Handled = true;
                uiElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void DatosComisiones_Enter(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new Action(delegate ()
                {
                    CrearClienteViewModel ccvm = (CrearClienteViewModel)DataContext;
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
