using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario.Behaviors
{
    /// <summary>
    /// Nesto#609 (sugerencia 970, Aida): en las rejillas de solo lectura que seleccionan filas enteras, Ctrl+C copia la fila
    /// entera y no hay manera de copiar solo un dato (el nº de envío para pegarlo en la web de la agencia). Con
    /// <c>CopiarCeldaDataGrid.Activo="True"</c> Ctrl+C copia solo el valor de la celda en la que se ha pinchado (la celda
    /// actual); si no hay celda actual o es de una columna sin valor que copiar (botones, plantillas), copia la fila como siempre.
    /// </summary>
    public static class CopiarCeldaDataGrid
    {
        public static readonly DependencyProperty ActivoProperty = DependencyProperty.RegisterAttached(
            "Activo", typeof(bool), typeof(CopiarCeldaDataGrid), new PropertyMetadata(false, OnActivoCambiado));

        public static bool GetActivo(DependencyObject obj) => (bool)obj.GetValue(ActivoProperty);

        public static void SetActivo(DependencyObject obj, bool value) => obj.SetValue(ActivoProperty, value);

        private static void OnActivoCambiado(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid rejilla)
            {
                return;
            }
            rejilla.CopyingRowClipboardContent -= OnCopiandoFila;
            if ((bool)e.NewValue)
            {
                rejilla.CopyingRowClipboardContent += OnCopiandoFila;
            }
        }

        private static void OnCopiandoFila(object sender, DataGridRowClipboardEventArgs e)
        {
            if (sender is DataGrid rejilla && !e.IsColumnHeadersRow)
            {
                DejarSoloLaColumna(e.ClipboardRowContent, rejilla.CurrentCell.Column);
            }
        }

        /// <summary>
        /// Deja en lo que se va a copiar de la fila solo la celda de <paramref name="columna"/>. No toca nada si no hay
        /// columna, si no se puede copiar (sin <see cref="DataGridColumn.ClipboardContentBinding"/>) o si no está en la fila.
        /// Devuelve true si ha dejado solo esa celda.
        /// </summary>
        public static bool DejarSoloLaColumna(List<DataGridClipboardCellContent> contenidoFila, DataGridColumn columna)
        {
            if (contenidoFila == null || columna?.ClipboardContentBinding == null)
            {
                return false;
            }
            List<DataGridClipboardCellContent> deLaColumna = contenidoFila.Where(c => c.Column == columna).ToList();
            if (deLaColumna.Count == 0)
            {
                return false;
            }
            contenidoFila.Clear();
            contenidoFila.AddRange(deLaColumna);
            return true;
        }
    }
}
