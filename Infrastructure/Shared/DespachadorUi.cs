using System;
using System.Windows;
using System.Windows.Threading;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#490 (4C.1): el IEventAggregator de Prism con <c>ThreadOption.UIThread</c> entregaba el
    /// evento en el hilo de la UI, y siempre ENCOLADO (SynchronizationContext.Post), aunque se
    /// publicara desde ese mismo hilo. El <c>IMessenger</c> de CommunityToolkit entrega en el hilo de
    /// quien envía. Los receptores que antes usaban UIThread pasan por aquí para conservar ese
    /// comportamiento.
    /// </summary>
    public static class DespachadorUi
    {
        /// <summary>
        /// Encola <paramref name="accion"/> en el hilo de la UI (como ThreadOption.UIThread de Prism).
        /// Sin aplicación WPF (tests) se ejecuta directamente.
        /// </summary>
        public static void EnHiloUi(Action accion)
        {
            EnHiloUi(Application.Current?.Dispatcher, accion);
        }

        public static void EnHiloUi(Dispatcher dispatcher, Action accion)
        {
            if (accion == null)
            {
                return;
            }
            if (dispatcher == null)
            {
                accion();
                return;
            }
            _ = dispatcher.BeginInvoke(accion);
        }
    }
}
