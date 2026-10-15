using Nesto.Infrastructure.Contracts;
using Prism.Ioc;
using System;

namespace Nesto.Infrastructure.Composicion
{
    /// <summary>
    /// Nesto#490 (4C.5, tramos 1-3): <see cref="IRegistroNesto"/> sobre el <see cref="IContainerRegistry"/> de Prism (Unity).
    /// Cada método hace exactamente el registro que se hacía antes a mano con Prism.
    /// </summary>
    public sealed class RegistroPrism : RegistroNestoBase
    {
        private readonly IContainerRegistry _registro;

        public RegistroPrism(IContainerRegistry registro)
        {
            _registro = registro ?? throw new ArgumentNullException(nameof(registro));
        }

        public override void Transitorio(Type servicio, Type implementacion)
            => _ = _registro.Register(servicio, implementacion);

        public override void Singleton(Type servicio, Type implementacion)
            => _ = _registro.RegisterSingleton(servicio, implementacion);

        public override void Singleton(Type servicio, Func<IResolutorNesto, object> fabrica)
        {
            if (fabrica == null)
            {
                throw new ArgumentNullException(nameof(fabrica));
            }
            _ = _registro.RegisterSingleton(servicio, (IContainerProvider proveedor) => fabrica(new ResolutorPrism(proveedor)));
        }

        public override void Instancia(Type servicio, object instancia)
            => _ = _registro.RegisterInstance(servicio, instancia);

        public override void Vista(string nombre, Type tipoVista)
        {
            ComprobarNombre(nombre, "La vista");
            _ = _registro.Register(typeof(object), tipoVista, nombre);
        }

        public override void Dialogo(string nombre, DialogoRegistrado dialogo)
        {
            ComprobarNombre(nombre, "El diálogo");
            _ = _registro.RegisterInstance(typeof(DialogoRegistrado), dialogo ?? throw new ArgumentNullException(nameof(dialogo)), nombre);
        }
    }
}
