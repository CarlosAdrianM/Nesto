using Nesto.Infrastructure.Contracts;
using Prism.Ioc;
using System;

namespace Nesto.Infrastructure.Composicion
{
    /// <summary>
    /// Nesto#490 (4C.5, tramos 1-3): <see cref="IResolutorNesto"/> sobre el <see cref="IContainerProvider"/> de Prism (Unity).
    /// </summary>
    public sealed class ResolutorPrism : IResolutorNesto
    {
        public ResolutorPrism(IContainerProvider proveedor)
        {
            Proveedor = proveedor ?? throw new ArgumentNullException(nameof(proveedor));
        }

        /// <summary>El contenedor de Prism de debajo, para lo que todavía lo necesita (se va en el tramo 3).</summary>
        public IContainerProvider Proveedor { get; }

        public object Resolver(Type tipo) => Proveedor.Resolve(tipo);

        public T Resolver<T>() => (T)Proveedor.Resolve(typeof(T));

        public object ResolverVista(string nombre) => Proveedor.Resolve(typeof(object), nombre);

        public bool TieneVista(string nombre)
            => !string.IsNullOrWhiteSpace(nombre)
               && Proveedor is IContainerRegistry registro
               && registro.IsRegistered(typeof(object), nombre);

        public DialogoRegistrado Dialogo(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)
                || (Proveedor is IContainerRegistry registro && !registro.IsRegistered(typeof(DialogoRegistrado), nombre)))
            {
                return null;
            }
            return Proveedor.Resolve(typeof(DialogoRegistrado), nombre) as DialogoRegistrado;
        }
    }
}
