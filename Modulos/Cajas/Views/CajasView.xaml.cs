using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace Nesto.Modulos.Cajas.Views
{
    /// <summary>
    /// Lógica de interacción para CajasView.xaml
    /// </summary>
    public partial class CajasView : UserControl
    {
        public CajasView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = txtImporte.Focus();
        }

        private void txtImporte_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {

                TextBox textBox = Keyboard.FocusedElement as TextBox;
                _ = textBox?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                // Mover el foco al botón después de completar el evento KeyUp
                _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _ = btnContabilizarTraspaso.Focus();
                }));
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift) && e.Key == Key.E)
            {
                var vm = DataContext as CajasViewModel;
                vm.CambiarEmpresaTraspasoCommand.Execute(null);
            }
        }

        private void txtImporte_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            TextBox textBox = sender as TextBox;

            // Verificar si la tecla presionada es la tecla del punto en el teclado numérico
            if (e.Key == Key.Decimal)
            {
                e.Handled = true;

                int caretIndex = textBox.CaretIndex;
                textBox.Text = textBox.Text.Insert(caretIndex, ",");
                textBox.CaretIndex = caretIndex + 1;
            }
        }

        private void txtImporte_GotFocus(object sender, RoutedEventArgs e)
        {
            txtImporte.SelectAll();
        }

        private void txtImporte_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            txtImporte.SelectAll();
        }

        private void DecimalUpDown_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _ = btnContabilizarCobro.Focus();
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift) && e.Key == Key.E)
            {
                var vm = DataContext as CajasViewModel;
                vm.CambiarEmpresaTraspasoCommand.Execute(null);
            }
        }



        private void DecimalUpDown_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Verificar si el texto que se va a agregar es un punto
            if (e.Text == ".")
            {
                // Reemplazar el punto por una coma
                e.Handled = true; // Para evitar que el punto se inserte
                TextBox textBox = ((DecimalUpDown)sender).Template.FindName("PART_TextBox", (DecimalUpDown)sender) as TextBox;
                int caretIndex = textBox.CaretIndex;
                textBox.Text = textBox.Text.Insert(caretIndex, ",");
                textBox.CaretIndex = caretIndex + 1;
            }
        }

        private void drgListaDeudas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _ = txtTotalCobrado.Focus();
        }
    }
}
