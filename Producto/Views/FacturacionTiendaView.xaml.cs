using Nesto.Modules.Producto.ViewModels;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Sugerencia 648 (NestoAPI#658): el Excel de lo facturado en la tienda. La lógica está en
    /// <see cref="FacturacionTiendaViewModel"/> (al llegar lee la tienda del usuario, IReceptorNavegacion).
    /// Nesto#559: en el comparativo, las columnas de cada año anterior («Base», «Var. €» y «Var. %») dependen de cuántos años se
    /// piden; se ponen aquí cuando cambian los años del resultado (<see cref="FacturacionTiendaViewModel.AniosAnteriores"/>).
    /// </summary>
    public partial class FacturacionTiendaView : UserControl
    {
        /// <summary>Las columnas fijas de la tabla (Mes, Facturas, Base, Cursos, Acumulado): las demás son de los años anteriores.</summary>
        private const int COLUMNAS_FIJAS = 5;

        private FacturacionTiendaViewModel _viewModel;

        public FacturacionTiendaView(FacturacionTiendaViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += (_, _) => Suscribir();
            Unloaded += (_, _) => Desuscribir();
        }

        private void Suscribir()
        {
            Desuscribir();
            _viewModel = DataContext as FacturacionTiendaViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += AlCambiarViewModel;
                PonerColumnasAnteriores(_viewModel.AniosAnteriores);
            }
        }

        private void Desuscribir()
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= AlCambiarViewModel;
                _viewModel = null;
            }
        }

        private void AlCambiarViewModel(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FacturacionTiendaViewModel.AniosAnteriores) && sender is FacturacionTiendaViewModel vm)
            {
                PonerColumnasAnteriores(vm.AniosAnteriores);
            }
        }

        private void PonerColumnasAnteriores(IReadOnlyList<int> anios)
        {
            DataGrid tabla = TablaAnual;
            if (tabla == null)
            {
                return;
            }
            while (tabla.Columns.Count > COLUMNAS_FIJAS)
            {
                tabla.Columns.RemoveAt(tabla.Columns.Count - 1);
            }
            Style numero = TryFindResource("CeldaNumero") as Style;
            List<int> lista = anios?.ToList() ?? new List<int>();
            for (int i = 0; i < lista.Count; i++)
            {
                tabla.Columns.Add(Columna($"Base {lista[i]}", $"Anteriores[{i}].BaseImponible", "N2", numero));
            }
            for (int i = 0; i < lista.Count; i++)
            {
                tabla.Columns.Add(Columna($"Var. € vs {lista[i]}", $"Anteriores[{i}].VariacionImporte", "N2", numero));
                tabla.Columns.Add(Columna($"Var. % vs {lista[i]}", $"Anteriores[{i}].VariacionPorcentaje", "N2", numero, "—"));
            }
        }

        private static DataGridTextColumn Columna(string cabecera, string ruta, string formato, Style estilo, string sinValor = null)
        {
            var binding = new Binding(ruta) { StringFormat = formato };
            if (sinValor != null)
            {
                binding.TargetNullValue = sinValor;
            }
            return new DataGridTextColumn { Header = cabecera, Binding = binding, ElementStyle = estilo };
        }
    }
}
