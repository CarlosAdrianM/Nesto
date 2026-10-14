using Nesto.Infrastructure.Contracts;
using System;
using System.Windows;
using System.Windows.Controls.Ribbon;

namespace Nesto.Infrastructure.Cinta
{
    /// <summary>
    /// Nesto#490 (4C.4, la cinta): pone los menús de los módulos en la cinta principal (la <c>Ribbon</c> de MainWindow)
    /// con <see cref="FusionCinta"/>. Sustituye al <c>RibbonRegionAdapter</c> de Prism que cada módulo resolvía.
    /// </summary>
    public class ServicioCinta : IServicioCinta
    {
        private readonly Ribbon _cintaPrincipal;

        public ServicioCinta(Ribbon cintaPrincipal)
        {
            _cintaPrincipal = cintaPrincipal ?? throw new ArgumentNullException(nameof(cintaPrincipal));
        }

        public void AnadirMenu(UIElement menu)
        {
            if (menu == null)
            {
                throw new ArgumentNullException(nameof(menu));
            }
            FusionCinta.Fusionar(menu, _cintaPrincipal);
        }
    }
}
