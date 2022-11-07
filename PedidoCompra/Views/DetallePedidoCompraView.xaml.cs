using Nesto.Modulos.PedidoCompra.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Nesto.Modulos.PedidoCompra.Views
{
    /// <summary>
    /// Lógica de interacción para DetallePedidoCompraView.xaml
    /// </summary>
    public partial class DetallePedidoCompraView : UserControl
    {
        public DetallePedidoCompraView()
        {
            InitializeComponent();
        }
    }

    public class LineaMaximaConverter : IMultiValueConverter
    {        
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            //DataRowView drv = values[0] as DataRowView;
            string producto = values[0] as string;
            LineaPedidoCompraWrapper linea = values[1] as LineaPedidoCompraWrapper;
            if (!string.IsNullOrEmpty(producto) && linea != null)
            {
                return producto == linea.Producto;
            }
            return false;
        }


        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
