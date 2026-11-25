using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Nesto.Modulos.Cajas.Views
{
    /// <summary>Proyectos (gemela de NestoAPI#901): proyectos, apuntes por imputar y cuenta de explotación.</summary>
    public partial class ProyectosView : UserControl
    {
        /// <summary>Ocupado si lo está cualquiera de las tres pestañas.</summary>
        public static readonly IMultiValueConverter CualquieraOcupado = new ConvertidorCualquieraVerdadero();
        public static readonly IValueConverter Negar = new ConvertidorNegar();

        public ProyectosView(ProyectosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        // SelectedItems de la rejilla no se puede enlazar: la vista le pasa al ViewModel lo elegido.
        private void grdApuntes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ProyectosViewModel vm && sender is DataGrid rejilla)
            {
                vm.Apuntes.CambiarSeleccion(rejilla.SelectedItems?.OfType<FilaApunteProyecto>().ToList());
            }
        }

        // Doble clic en una fila de proyecto: su cuenta de explotación.
        private void grdProyectos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not ProyectosViewModel vm || e.OriginalSource is not DependencyObject origen)
            {
                return;
            }
            DependencyObject? actual = origen;
            while (actual != null && actual is not DataGridRow)
            {
                actual = actual is Visual || actual is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(actual) : null;
            }
            if (actual is DataGridRow && vm.VerCuentaCommand.CanExecute(null))
            {
                vm.VerCuentaCommand.Execute(null);
            }
        }

        // La comparativa tiene una columna por proyecto (P0, P1…): la cabecera es su código, que va en el Caption.
        private void grdComparativa_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (sender is DataGrid rejilla && rejilla.ItemsSource is DataView vista && vista.Table != null
                && vista.Table.Columns.Contains(e.PropertyName))
            {
                e.Column.Header = vista.Table.Columns[e.PropertyName]!.Caption;
            }
        }

        private sealed class ConvertidorCualquieraVerdadero : IMultiValueConverter
        {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
                => values?.OfType<bool>().Any(v => v) ?? false;

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }

        private sealed class ConvertidorNegar : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
        }
    }
}
