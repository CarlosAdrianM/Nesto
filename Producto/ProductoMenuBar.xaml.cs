using System;
using System.Collections.Generic;
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
using Nesto.Modules.Producto.ViewModels;

namespace Nesto.Modulos.Producto
{
    /// <summary>
    /// Lógica de interacción para ProductoMenuBar.xaml
    /// </summary>
    public partial class ProductoMenuBar : UserControl
    {
        public ProductoMenuBar()
        {
            InitializeComponent();
        }

        public ProductoMenuBar(ProductoViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
