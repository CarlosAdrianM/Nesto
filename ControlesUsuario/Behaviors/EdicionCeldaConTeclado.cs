using Microsoft.Xaml.Behaviors;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlesUsuario.Behaviors
{
    /// <summary>
    /// Carlos 29/09/26: editar con el teclado las celdas de un DataGrid cuya plantilla de edición lleva un
    /// TextBox con <see cref="AutocompleteBehavior"/> o <see cref="CuentaContableBehavior"/> (columna Cuenta de
    /// los prepagos, Producto de las líneas...).
    ///
    /// WPF solo empieza a editar al escribir en las DataGridTextColumn: en una DataGridTemplateColumn llegar con
    /// Tab y escribir no hace nada, y con F2 el TextBox aparece pero sin el foco. Aquí:
    /// - al escribir sobre una de esas celdas sin editar, se empieza la edición y el carácter pulsado pasa al
    ///   TextBox (como en una columna de texto: sustituye lo que había);
    /// - al aparecer el TextBox de edición (F2, Enter...), se le da el foco con el texto seleccionado.
    ///
    /// Solo afecta a las columnas cuya plantilla de edición lleva uno de esos behaviors; las demás columnas de
    /// plantilla de la aplicación siguen como estaban. Se registra una vez al arrancar (<see cref="Registrar"/>).
    /// </summary>
    public static class EdicionCeldaConTeclado
    {
        private static readonly object _cerrojo = new object();
        private static bool _registrado;
        private static readonly ConditionalWeakTable<DataGridColumn, object> _columnasConBehavior = new ConditionalWeakTable<DataGridColumn, object>();
        private static WeakReference<DataGridCell> _celdaPendiente;
        private static string _textoPendiente;

        /// <summary>Engancha el manejador global de las celdas. Idempotente.</summary>
        public static void Registrar()
        {
            lock (_cerrojo)
            {
                if (_registrado)
                {
                    return;
                }
                EventManager.RegisterClassHandler(typeof(DataGridCell), UIElement.PreviewTextInputEvent,
                    new TextCompositionEventHandler(OnCeldaPreviewTextInput));
                _registrado = true;
            }
        }

        /// <summary>
        /// Lo llaman los behaviors cuando su TextBox se carga: si está en una celda que se está editando, le da el
        /// foco y, si la edición empezó al escribir, le pasa el carácter. Idempotente (lo llaman los dos behaviors).
        /// </summary>
        public static void AlCargarEditor(TextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }
            DataGridCell celda = BuscarAncestro<DataGridCell>(textBox);
            if (celda == null || !celda.IsEditing)
            {
                return;
            }
            string texto = TomarTextoPendiente(celda);
            if (texto != null)
            {
                _ = textBox.Focus();
                textBox.Text = texto;
                textBox.CaretIndex = textBox.Text.Length;
                return;
            }
            if (celda.IsKeyboardFocusWithin && !textBox.IsKeyboardFocusWithin)
            {
                _ = textBox.Focus();
                textBox.SelectAll();
            }
        }

        private static void OnCeldaPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!(sender is DataGridCell celda) || celda.IsEditing || celda.IsReadOnly
                || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
            {
                return;
            }
            if (!(celda.Column is DataGridTemplateColumn columna) || !TieneBehaviorDeEdicion(columna))
            {
                return;
            }
            DataGrid grid = BuscarAncestro<DataGrid>(celda);
            if (grid == null || grid.IsReadOnly)
            {
                return;
            }
            grid.CurrentCell = new DataGridCellInfo(celda.DataContext, columna);
            _celdaPendiente = new WeakReference<DataGridCell>(celda);
            _textoPendiente = e.Text;
            if (grid.BeginEdit(e))
            {
                e.Handled = true;
            }
            else
            {
                _celdaPendiente = null;
                _textoPendiente = null;
            }
        }

        private static string TomarTextoPendiente(DataGridCell celda)
        {
            if (_celdaPendiente == null || !_celdaPendiente.TryGetTarget(out DataGridCell pendiente) || !ReferenceEquals(pendiente, celda))
            {
                return null;
            }
            string texto = _textoPendiente;
            _celdaPendiente = null;
            _textoPendiente = null;
            return texto;
        }

        /// <summary>
        /// ¿La plantilla de edición de la columna lleva un TextBox con alguno de nuestros behaviors? Se mira una
        /// vez por columna (se instancia la plantilla fuera del árbol visual) y se guarda.
        /// </summary>
        internal static bool TieneBehaviorDeEdicion(DataGridTemplateColumn columna)
        {
            if (_columnasConBehavior.TryGetValue(columna, out object cacheado))
            {
                return (bool)cacheado;
            }
            bool tiene = false;
            try
            {
                if (columna.CellEditingTemplate?.LoadContent() is DependencyObject raiz)
                {
                    tiene = TieneTextBoxConBehavior(raiz);
                }
            }
            catch
            {
                tiene = false; // Una plantilla que no se deja instanciar fuera del árbol: se queda como estaba
            }
            _columnasConBehavior.AddOrUpdate(columna, tiene);
            return tiene;
        }

        private static bool TieneTextBoxConBehavior(DependencyObject nodo)
        {
            if (nodo is TextBox textBox && Interaction.GetBehaviors(textBox)
                .Any(b => b is AutocompleteBehavior || b is CuentaContableBehavior))
            {
                return true;
            }
            foreach (object hijo in LogicalTreeHelper.GetChildren(nodo))
            {
                if (hijo is DependencyObject d && TieneTextBoxConBehavior(d))
                {
                    return true;
                }
            }
            return false;
        }

        private static T BuscarAncestro<T>(DependencyObject desde) where T : DependencyObject
        {
            DependencyObject actual = desde;
            while (actual != null)
            {
                if (actual is T encontrado)
                {
                    return encontrado;
                }
                actual = actual is Visual || actual is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(actual)
                    : LogicalTreeHelper.GetParent(actual);
            }
            return null;
        }
    }
}
