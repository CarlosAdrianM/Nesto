using Nesto.Infrastructure.Contracts;
using Prism.Regions;
using System;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4, paso 1): <see cref="IServicioNavegacion"/> que delega en el IRegionManager de
    /// Prism. Mismo comportamiento que llamar a RequestNavigate directamente; solo traduce los
    /// parámetros a <see cref="NavigationParameters"/>, con las mismas claves y valores.
    /// </summary>
    public class ServicioNavegacionPrism : IServicioNavegacion
    {
        private readonly IRegionManager _regionManager;

        public ServicioNavegacionPrism(IRegionManager regionManager)
        {
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        }

        public void RequestNavigate(string regionName, string source)
            => _regionManager.RequestNavigate(regionName, source);

        public void RequestNavigate(string regionName, string source, ParametrosNavegacion parameters)
            => _regionManager.RequestNavigate(regionName, source, AParametrosPrism(parameters));

        internal static NavigationParameters AParametrosPrism(ParametrosNavegacion parametros)
        {
            var prism = new NavigationParameters();
            if (parametros != null)
            {
                foreach (var entrada in parametros)
                {
                    prism.Add(entrada.Key, entrada.Value);
                }
            }
            return prism;
        }
    }
}
