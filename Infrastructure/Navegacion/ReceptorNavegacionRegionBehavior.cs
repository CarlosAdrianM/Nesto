using Nesto.Infrastructure.Contracts;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4): entrega cada navegación terminada a la vista de destino (o a su DataContext) que
    /// implemente <see cref="IReceptorNavegacion"/>. Así los ViewModels reciben la navegación sin depender de
    /// Prism; cuando Nesto deje Prism, quien navegue llamará a <see cref="ReceptorNavegacion.Entregar"/> igual.
    /// Registrado de forma global en <c>Application.ConfigureDefaultRegionBehaviors</c>.
    /// </summary>
    public class ReceptorNavegacionRegionBehavior : RegionBehavior
    {
        public const string BehaviorKey = "ReceptorNavegacion";

        protected override void OnAttach()
        {
            Region.NavigationService.Navigated += AlNavegar;
        }

        private void AlNavegar(object sender, RegionNavigationEventArgs e)
        {
            ReceptorNavegacion.Entregar(Region.ActiveViews, Region.Views, e?.Uri?.OriginalString,
                ServicioNavegacionPrism.DesdeParametrosPrism(e?.NavigationContext?.Parameters));
        }
    }

    /// <summary>La parte pura (sin Prism) de <see cref="ReceptorNavegacionRegionBehavior"/>, para poder probarla.</summary>
    public static class ReceptorNavegacion
    {
        /// <summary>
        /// Busca la vista de destino como Prism (por el nombre de su tipo, o el completo), primero entre las
        /// activas, y le entrega la navegación a ella o a su DataContext. Devuelve a quién se ha entregado (null
        /// si a nadie: vista que no es receptora o que no está en la región).
        /// </summary>
        public static IReceptorNavegacion Entregar(IEnumerable<object> vistasActivas, IEnumerable<object> vistas,
            string destino, ParametrosNavegacion parametros)
        {
            string contrato = NombreDelDestino(destino);
            if (contrato == null)
            {
                return null;
            }
            object vista = Buscar(vistasActivas, contrato) ?? Buscar(vistas, contrato);
            IReceptorNavegacion receptor = vista as IReceptorNavegacion ?? (vista as FrameworkElement)?.DataContext as IReceptorNavegacion;
            receptor?.AlLlegar(parametros ?? new ParametrosNavegacion());
            return receptor;
        }

        // «ExtractoClienteView?cliente=1» → «ExtractoClienteView» (los parámetros llegan aparte)
        private static string NombreDelDestino(string destino)
        {
            if (string.IsNullOrWhiteSpace(destino))
            {
                return null;
            }
            int consulta = destino.IndexOf('?');
            string contrato = (consulta >= 0 ? destino.Substring(0, consulta) : destino).Trim().TrimStart('/');
            return contrato.Length == 0 ? null : contrato;
        }

        // Como RegionNavigationContentLoader de Prism: el nombre del tipo de la vista, o el completo
        private static object Buscar(IEnumerable<object> vistas, string contrato)
        {
            if (vistas == null)
            {
                return null;
            }
            foreach (object vista in vistas)
            {
                Type tipo = vista?.GetType();
                if (tipo != null && (string.Equals(tipo.Name, contrato, StringComparison.Ordinal)
                                     || string.Equals(tipo.FullName, contrato, StringComparison.Ordinal)))
                {
                    return vista;
                }
            }
            return null;
        }
    }
}
