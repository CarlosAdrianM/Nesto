using Nesto.Infrastructure.Contracts;
using Prism.Regions;
using System;
using System.Linq;

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

        public object VistaActiva(string regionName)
        {
            try
            {
                return _regionManager.Regions.ContainsRegionWithName(regionName)
                    ? _regionManager.Regions[regionName].ActiveViews.FirstOrDefault()
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Igual que se hacía a mano en CrearCliente y PlantillaVenta: Deactivate y luego Remove.
        public void CerrarVistaActiva(string regionName)
        {
            IRegion region = _regionManager.Regions[regionName];
            object vista = region.ActiveViews.FirstOrDefault();
            if (vista != null)
            {
                region.Deactivate(vista);
                region.Remove(vista);
            }
        }

        public void QuitarVistas(string regionName)
        {
            if (!_regionManager.Regions.ContainsRegionWithName(regionName))
            {
                return;
            }
            IRegion region = _regionManager.Regions[regionName];
            foreach (object vista in region.Views.ToList())
            {
                region.Remove(vista);
            }
        }

        // Lo que hacía MenuBarViewModel.NavegarAVista: nombre único (Clientes, Clientes2…), Add y Activate
        public void AbrirVistaNueva(string regionName, object vista, string nombre)
        {
            IRegion region = _regionManager.Regions[regionName];
            string unico = nombre;
            int contador = 2;
            while (region.GetView(unico) != null)
            {
                unico = nombre + contador;
                contador++;
            }
            region.Add(vista, unico);
            region.Activate(vista);
        }

        /// <summary>Lo contrario de <see cref="AParametrosPrism"/>: para entregar la navegación sin tipos de Prism.</summary>
        internal static ParametrosNavegacion DesdeParametrosPrism(NavigationParameters prism)
        {
            var parametros = new ParametrosNavegacion();
            if (prism != null)
            {
                foreach (var entrada in prism)
                {
                    parametros.Add(entrada.Key, entrada.Value);
                }
            }
            return parametros;
        }

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
