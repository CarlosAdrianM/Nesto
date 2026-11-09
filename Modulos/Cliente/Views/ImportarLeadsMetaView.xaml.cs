using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#623 corte 4: importar el fichero de leads de Meta. Lee los orígenes al abrirse.</summary>
    public partial class ImportarLeadsMetaView : UserControl
    {
        public ImportarLeadsMetaView(ImportarLeadsMetaViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += (s, e) => _ = viewModel.CargarAsync();
        }
    }
}
