using Nesto.Modulos.CanalesExternos.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Interfaces
{
    public interface ICanalesExternosPagosService
    {
        Task<ObservableCollection<PagoCanalExterno>> BuscarAsientos(ObservableCollection<PagoCanalExterno> pagos);
    }
}
