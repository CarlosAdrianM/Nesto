using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#584: lista de horarios preferibles por revisar. Lee la API al abrirse.</summary>
    public partial class HorariosPorRevisarView : UserControl
    {
        public HorariosPorRevisarView(HorariosPorRevisarViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += (s, e) => _ = viewModel.CargarAsync();
        }
    }
}
