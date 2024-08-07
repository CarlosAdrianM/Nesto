﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para ArqueoEfectivo.xaml
    /// </summary>
    public partial class ArqueoEfectivo : UserControl
    {
        public ArqueoEfectivo()
        {
            InitializeComponent();
        }

        private void txtRecuento_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        private void txtRecuento_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                TextBox textBox = sender as TextBox;
                if (textBox != null && !textBox.IsKeyboardFocusWithin)
                {
                    textBox.Focus();
                    e.Handled = true; // Evita que se pierda el clic
                }
            }
        }

        private void txtRecuento_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox textBox = Keyboard.FocusedElement as TextBox;
                if (textBox != null)
                {
                    textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control) && e.Key == Key.B)
            {
                MessageBoxResult result = MessageBox.Show("¿Desea vaciar el arqueo?", "Confirmación", MessageBoxButton.YesNo, MessageBoxImage.Question);

                e.Handled = true;

                if (result == MessageBoxResult.No)
                {
                    return;
                }

                var arqueo = DataContext as ArqueoEfectivoModel;
                arqueo.VaciarArqueo();
            }
        }


        #region Dependency Properties
        
        #endregion
    }
}
