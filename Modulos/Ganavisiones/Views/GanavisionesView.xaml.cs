using Nesto.Modulos.Ganavisiones.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Nesto.Modulos.Ganavisiones.Views
{
    public partial class GanavisionesView : UserControl
    {
        private GanavisionWrapper _itemPendienteFoco;

        public GanavisionesView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            dgGanavisiones.LoadingRow += OnLoadingRow;

            // Prism establece el DataContext durante InitializeComponent (AutoWireViewModel),
            // así que verificamos aquí si ya está establecido
            if (DataContext is GanavisionesViewModel vm)
            {
                vm.NuevoGanavisionCreado += OnNuevoGanavisionCreado;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is GanavisionesViewModel oldVm)
            {
                oldVm.NuevoGanavisionCreado -= OnNuevoGanavisionCreado;
            }

            if (e.NewValue is GanavisionesViewModel newVm)
            {
                newVm.NuevoGanavisionCreado += OnNuevoGanavisionCreado;
            }
        }

        private void OnNuevoGanavisionCreado(GanavisionWrapper nuevoItem)
        {
            // El LoadingRow ya se disparó antes de este evento, así que la fila ya existe.
            // Intentamos obtenerla directamente.
            dgGanavisiones.UpdateLayout();
            dgGanavisiones.ScrollIntoView(nuevoItem);
            dgGanavisiones.UpdateLayout();

            var row = dgGanavisiones.ItemContainerGenerator.ContainerFromItem(nuevoItem) as DataGridRow;

            if (row != null)
            {
                // La fila ya está generada, poner foco directamente
                if (row.IsLoaded)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        PonerFocoEnCeldaProducto(row);
                    }));
                }
                else
                {
                    row.Loaded += Row_Loaded_ParaFoco;
                }
            }
            else
            {
                // La fila no está generada aún, guardar para LoadingRow
                _itemPendienteFoco = nuevoItem;
            }
        }

        private void Row_Loaded_ParaFoco(object sender, RoutedEventArgs e)
        {
            var row = sender as DataGridRow;
            if (row == null) return;

            row.Loaded -= Row_Loaded_ParaFoco;

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                PonerFocoEnCeldaProducto(row);
            }));
        }

        private void OnLoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Verificar si esta es la fila que necesita foco (caso raro donde LoadingRow llega después)
            if (_itemPendienteFoco != null && e.Row.Item == _itemPendienteFoco)
            {
                _itemPendienteFoco = null;
                e.Row.Loaded += Row_Loaded_ParaFoco;
            }
        }

        private void PonerFocoEnCeldaProducto(DataGridRow row)
        {
            try
            {
                // Seleccionar la fila
                dgGanavisiones.SelectedItem = row.Item;
                row.IsSelected = true;

                // Obtener la primera celda (columna Producto)
                var cell = GetCell(dgGanavisiones, row, 0);

                if (cell != null)
                {
                    // Dar foco a la celda
                    cell.Focus();

                    // Comenzar edición
                    dgGanavisiones.CurrentCell = new DataGridCellInfo(row.Item, dgGanavisiones.Columns[0]);
                    dgGanavisiones.BeginEdit();

                    // Dar foco al TextBox dentro de la celda después de que entre en modo edición
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        var textBox = FindVisualChild<TextBox>(cell);

                        if (textBox != null)
                        {
                            textBox.Focus();
                            Keyboard.Focus(textBox);
                            textBox.SelectAll();
                        }
                    }));
                }
            }
            catch
            {
                // Ignorar errores si el visual tree cambió
            }
        }

        /// <summary>
        /// Entrar en modo edicion con un solo clic para DataGridTemplateColumn
        /// </summary>
        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var cell = sender as DataGridCell;
            if (cell == null || cell.IsEditing || cell.IsReadOnly)
                return;

            // Si el clic es sobre un boton, dejarlo pasar
            if (e.OriginalSource is ButtonBase || FindVisualParent<ButtonBase>(e.OriginalSource as DependencyObject) != null)
                return;

            var dataGrid = FindVisualParent<DataGrid>(cell);
            if (dataGrid == null)
                return;

            // Seleccionar la fila
            var row = FindVisualParent<DataGridRow>(cell);
            if (row != null && !row.IsSelected)
            {
                row.IsSelected = true;
            }

            // Dar foco a la celda
            if (!cell.IsFocused)
            {
                cell.Focus();
            }

            // Comenzar edicion
            if (!cell.IsEditing)
            {
                dataGrid.BeginEdit(e);

                // Buscar y dar foco al control editable dentro de la celda
                cell.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    var textBox = FindVisualChild<TextBox>(cell);
                    if (textBox != null)
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }));
            }
        }

        private static DataGridCell GetCell(DataGrid dataGrid, DataGridRow row, int columnIndex)
        {
            if (row == null) return null;

            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null)
            {
                // Forzar la generacion del visual
                row.ApplyTemplate();
                presenter = FindVisualChild<DataGridCellsPresenter>(row);
            }

            if (presenter != null)
            {
                var cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
                if (cell == null)
                {
                    // La celda puede no estar generada, forzar scroll
                    dataGrid.ScrollIntoView(row, dataGrid.Columns[columnIndex]);
                    cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
                }
                return cell;
            }

            return null;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null)
                return null;

            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindVisualParent<T>(parentObject);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
