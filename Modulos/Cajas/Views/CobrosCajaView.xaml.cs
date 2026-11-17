using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace Nesto.Modulos.Cajas.Views
{
    /// <summary>
    /// Nesto#645: la pestaña «Cobros» de Cajas, aparte para usarla también en el diálogo de cobro de la factura recién creada.
    /// El DataContext es el <see cref="CajasViewModel"/>.
    /// </summary>
    public partial class CobrosCajaView : UserControl
    {
        private CajasViewModel _vm;

        public CobrosCajaView()
        {
            InitializeComponent();
            DataContextChanged += CobrosCajaView_DataContextChanged;
        }

        /// <summary>Pone el foco en «Contabilizar» (lo usa también la pestaña de gastos de Cajas, como antes).</summary>
        public void EnfocarContabilizar() => _ = btnContabilizarCobro.Focus();

        private void CobrosCajaView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.DeudasPreseleccionadas -= Vm_DeudasPreseleccionadas;
            }
            _vm = DataContext as CajasViewModel;
            if (_vm != null)
            {
                _vm.DeudasPreseleccionadas += Vm_DeudasPreseleccionadas;
            }
        }

        /// <summary>Nesto#645: marca en la lista las deudas de la factura (la selección de la lista es lo que se cobra).</summary>
        private void Vm_DeudasPreseleccionadas(object sender, IReadOnlyList<ExtractoClienteDTO> deudas)
        {
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                dgrListaDeudas.SelectedItems.Clear();
                foreach (var deuda in deudas)
                {
                    dgrListaDeudas.SelectedItems.Add(deuda);
                }
                if (deudas.Count > 0)
                {
                    dgrListaDeudas.ScrollIntoView(deudas[0]);
                }
                _ = (txtEntregaCliente.IsVisible ? (IInputElement)txtEntregaCliente : btnContabilizarCobro).Focus();
            }));
        }

        private void DecimalUpDown_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                EnfocarContabilizar();
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

        /// <summary>
        /// Nesto#511: el menú contextual copia de la fila bajo el cursor. Se marca como manejado para que el clic derecho
        /// no cambie la selección (las filas seleccionadas son las deudas a cobrar); el menú se abre igual, al soltar.
        /// </summary>
        private void dgrListaDeudas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not CajasViewModel vm)
            {
                return;
            }
            DependencyObject elemento = e.OriginalSource as DependencyObject;
            while (elemento != null && elemento is not DataGridRow)
            {
                elemento = elemento is System.Windows.Media.Visual
                    ? System.Windows.Media.VisualTreeHelper.GetParent(elemento)
                    : LogicalTreeHelper.GetParent(elemento);
            }
            if (elemento is DataGridRow fila && fila.Item is ExtractoClienteDTO deuda)
            {
                vm.EstablecerDeudaBajoCursor(deuda);
                e.Handled = true;
            }
            else
            {
                vm.EstablecerDeudaBajoCursor(null);
            }
        }
    }
}
