using Nesto.Modulos.OfertasCombinadas.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Nesto.Modulos.OfertasCombinadas.Views
{
    public partial class OfertasCombinadasView : UserControl
    {
        private OfertaCombinadaWrapper _itemPendienteFocoOfertas;
        private OfertaPermitidaFamiliaWrapper _itemPendienteFocoFamilia;

        public OfertasCombinadasView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            dgOfertasCombinadas.LoadingRow += OnLoadingRowOfertas;
            dgOfertasFamilia.LoadingRow += OnLoadingRowFamilia;

            if (DataContext is OfertasCombinadasViewModel vm)
            {
                SuscribirEventos(vm);
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is OfertasCombinadasViewModel oldVm)
            {
                DesuscribirEventos(oldVm);
            }

            if (e.NewValue is OfertasCombinadasViewModel newVm)
            {
                SuscribirEventos(newVm);
            }
        }

        private void SuscribirEventos(OfertasCombinadasViewModel vm)
        {
            vm.NuevaOfertaCombinadaCreada += OnNuevaOfertaCombinadaCreada;
            vm.NuevaOfertaFamiliaCreada += OnNuevaOfertaFamiliaCreada;
        }

        private void DesuscribirEventos(OfertasCombinadasViewModel vm)
        {
            vm.NuevaOfertaCombinadaCreada -= OnNuevaOfertaCombinadaCreada;
            vm.NuevaOfertaFamiliaCreada -= OnNuevaOfertaFamiliaCreada;
        }

        #region Foco Ofertas Combinadas

        private void OnNuevaOfertaCombinadaCreada(OfertaCombinadaWrapper nuevoItem)
        {
            ScrollYFoco(dgOfertasCombinadas, nuevoItem, ref _itemPendienteFocoOfertas);
        }

        private void OnLoadingRowOfertas(object sender, DataGridRowEventArgs e)
        {
            if (_itemPendienteFocoOfertas != null && e.Row.Item == _itemPendienteFocoOfertas)
            {
                _itemPendienteFocoOfertas = null;
                e.Row.Loaded += Row_Loaded_ParaFoco;
            }
        }

        #endregion

        #region Foco Ofertas Familia

        private void OnNuevaOfertaFamiliaCreada(OfertaPermitidaFamiliaWrapper nuevoItem)
        {
            ScrollYFoco(dgOfertasFamilia, nuevoItem, ref _itemPendienteFocoFamilia);
        }

        private void OnLoadingRowFamilia(object sender, DataGridRowEventArgs e)
        {
            if (_itemPendienteFocoFamilia != null && e.Row.Item == _itemPendienteFocoFamilia)
            {
                _itemPendienteFocoFamilia = null;
                e.Row.Loaded += Row_Loaded_ParaFoco;
            }
        }

        #endregion

        #region Helpers de foco

        private void ScrollYFoco<T>(DataGrid dataGrid, T nuevoItem, ref T itemPendiente)
        {
            dataGrid.UpdateLayout();
            dataGrid.ScrollIntoView(nuevoItem);
            dataGrid.UpdateLayout();

            var row = dataGrid.ItemContainerGenerator.ContainerFromItem(nuevoItem) as DataGridRow;

            if (row != null)
            {
                if (row.IsLoaded)
                {
                    var dg = dataGrid;
                    Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        PonerFocoEnPrimeraCelda(dg, row);
                    }));
                }
                else
                {
                    row.Loaded += Row_Loaded_ParaFoco;
                }
            }
            else
            {
                itemPendiente = nuevoItem;
            }
        }

        private void Row_Loaded_ParaFoco(object sender, RoutedEventArgs e)
        {
            var row = sender as DataGridRow;
            if (row == null) return;

            row.Loaded -= Row_Loaded_ParaFoco;

            var dataGrid = FindVisualParent<DataGrid>(row);
            if (dataGrid == null) return;

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                PonerFocoEnPrimeraCelda(dataGrid, row);
            }));
        }

        private void PonerFocoEnPrimeraCelda(DataGrid dataGrid, DataGridRow row)
        {
            try
            {
                dataGrid.SelectedItem = row.Item;
                row.IsSelected = true;

                var cell = GetCell(dataGrid, row, 0);

                if (cell != null)
                {
                    cell.Focus();
                    dataGrid.CurrentCell = new DataGridCellInfo(row.Item, dataGrid.Columns[0]);
                    dataGrid.BeginEdit();

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
                // Ignorar errores si el visual tree cambio
            }
        }

        #endregion

        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var cell = sender as DataGridCell;
            if (cell == null || cell.IsEditing || cell.IsReadOnly)
                return;

            if (e.OriginalSource is ButtonBase || FindVisualParent<ButtonBase>(e.OriginalSource as DependencyObject) != null)
                return;

            var dataGrid = FindVisualParent<DataGrid>(cell);
            if (dataGrid == null)
                return;

            var row = FindVisualParent<DataGridRow>(cell);
            if (row != null && !row.IsSelected)
            {
                row.IsSelected = true;
            }

            if (!cell.IsFocused)
            {
                cell.Focus();
            }

            if (!cell.IsEditing)
            {
                dataGrid.BeginEdit(e);

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
                row.ApplyTemplate();
                presenter = FindVisualChild<DataGridCellsPresenter>(row);
            }

            if (presenter != null)
            {
                var cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
                if (cell == null)
                {
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
