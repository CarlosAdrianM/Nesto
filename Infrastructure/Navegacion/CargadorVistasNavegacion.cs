using Prism.Ioc;
using Prism.Regions;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4): el cargador de vistas de la navegación de Prism (<see cref="IRegionNavigationContentLoader"/>),
    /// que decide si una navegación reutiliza una vista abierta de la región o crea otra. Hace lo mismo que el de
    /// Prism salvo con las vistas cuyo ViewModel (o ellas mismas) es un
    /// <see cref="Contracts.IReceptorNavegacionPestanaNueva"/>: esas no se reutilizan nunca, así que cada navegación
    /// abre una nueva, igual que hacía <c>INavigationAware.IsNavigationTarget = false</c> sin que el ViewModel tenga
    /// que conocer los tipos de Prism. Registrado en <c>Application.RegisterTypes</c> en lugar del de Prism.
    /// </summary>
    public class CargadorVistasNavegacion : RegionNavigationContentLoader
    {
        public CargadorVistasNavegacion(IContainerExtension container) : base(container)
        {
        }

        protected override IEnumerable<object> GetCandidatesFromRegion(IRegion region, string candidateNavigationContract)
        {
            return base.GetCandidatesFromRegion(region, candidateNavigationContract)
                .Where(vista => !ReceptorNavegacion.QuierePestanaNueva(vista))
                .ToList();
        }
    }
}
