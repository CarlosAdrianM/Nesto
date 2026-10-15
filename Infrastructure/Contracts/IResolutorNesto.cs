using System;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.5): de donde se saca lo registrado con <see cref="IRegistroNesto"/>, sin tipos de Prism ni de
    /// Microsoft.Extensions.DependencyInjection. Hoy lo implementa <c>ResolutorPrism</c> (delega en el
    /// <c>IContainerProvider</c> de Prism/Unity); en el tramo 4, el de <c>ContenedorMicrosoft</c> (MS.DI).
    /// </summary>
    public interface IResolutorNesto
    {
        /// <summary>
        /// Lo registrado para <paramref name="tipo"/> o, si es una clase concreta sin registrar, una instancia nueva con sus
        /// dependencias sacadas del contenedor. Lanza si no se puede construir.
        /// </summary>
        object Resolver(Type tipo);

        /// <summary>Lo mismo que <see cref="Resolver(Type)"/>, con el tipo ya convertido.</summary>
        T Resolver<T>();

        /// <summary>Una vista nueva de las registradas con <see cref="IRegistroNesto.Vista"/> con ese nombre. Lanza si no hay ninguna.</summary>
        object ResolverVista(string nombre);

        /// <summary>Si hay una vista registrada con ese nombre.</summary>
        bool TieneVista(string nombre);

        /// <summary>El diálogo registrado con ese nombre (<see cref="IRegistroNesto.Dialogo"/>); null si no hay ninguno.</summary>
        DialogoRegistrado Dialogo(string nombre);
    }
}
