using System;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.5, tramo 2): el punto de acceso al contenedor para lo que no puede recibir sus dependencias por el
    /// constructor (controles creados desde XAML, behaviors, código heredado). Sustituye a <c>Prism.Ioc.ContainerLocator</c>:
    /// en los tramos 2-3 lo inicializaba el arranque con un <c>ResolutorPrism</c> (sobre el contenedor de Prism/Unity);
    /// desde el tramo 4, Application.OnStartup lo inicializa con el de Microsoft.Extensions.DependencyInjection (sin tocar a quien lo usa).
    /// <para>
    /// Es <c>null</c> mientras nadie lo inicialice (el diseñador de XAML y la mayoría de los tests), igual que
    /// <c>ContainerLocator.Container</c>: quien lo use fuera de la aplicación tiene que contar con ello.
    /// </para>
    /// </summary>
    public static class Contenedor
    {
        private static readonly object _cerrojo = new object();
        private static IResolutorNesto _actual;

        /// <summary>El contenedor de la aplicación; <c>null</c> si todavía no se ha inicializado.</summary>
        public static IResolutorNesto Actual
        {
            get
            {
                lock (_cerrojo)
                {
                    return _actual;
                }
            }
        }

        /// <summary>Lo llama el arranque de la aplicación, una vez montado el contenedor.</summary>
        public static void Establecer(IResolutorNesto resolutor)
        {
            if (resolutor == null)
            {
                throw new ArgumentNullException(nameof(resolutor));
            }
            lock (_cerrojo)
            {
                _actual = resolutor;
            }
        }

        /// <summary>
        /// Para los tests: pone <paramref name="resolutor"/> (puede ser <c>null</c>, para probar el caso «sin contenedor»)
        /// hasta que se libere lo devuelto, que deja el que hubiera antes.
        /// <code>using (Contenedor.Sustituir(falso)) { ... }</code>
        /// </summary>
        public static IDisposable Sustituir(IResolutorNesto resolutor)
        {
            IResolutorNesto anterior;
            lock (_cerrojo)
            {
                anterior = _actual;
                _actual = resolutor;
            }
            return new Restaurador(anterior);
        }

        /// <summary>Lo deja sin inicializar (para los tests).</summary>
        public static void Restablecer()
        {
            lock (_cerrojo)
            {
                _actual = null;
            }
        }

        private sealed class Restaurador : IDisposable
        {
            private readonly IResolutorNesto _anterior;
            private bool _liberado;

            public Restaurador(IResolutorNesto anterior)
            {
                _anterior = anterior;
            }

            public void Dispose()
            {
                if (_liberado)
                {
                    return;
                }
                _liberado = true;
                lock (_cerrojo)
                {
                    _actual = _anterior;
                }
            }
        }
    }
}
