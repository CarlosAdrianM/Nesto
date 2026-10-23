using System;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.5): donde Nesto y sus módulos registran lo que se puede pedir al contenedor, sin tipos de Prism ni de
    /// Microsoft.Extensions.DependencyInjection. Lo implementa <c>ContenedorMicrosoft</c> (MS.DI); hasta el tramo 4 de
    /// 4C.5, también <c>RegistroPrism</c> (sobre el <c>IContainerRegistry</c> de Prism/Unity).
    /// <para>
    /// Diferencia con Unity: Unity construye cualquier clase concreta aunque no esté registrada y MS.DI no. Por eso todo lo
    /// que se pide por su tipo concreto (menús de la cinta, ViewModels de las vistas, ventanas…) se registra aquí.
    /// </para>
    /// </summary>
    public interface IRegistroNesto
    {
        /// <summary>Una instancia nueva de <paramref name="implementacion"/> cada vez que se pide <paramref name="servicio"/>.</summary>
        void Transitorio(Type servicio, Type implementacion);

        /// <summary>Una instancia nueva de <typeparamref name="TImplementacion"/> cada vez que se pide <typeparamref name="TServicio"/>.</summary>
        void Transitorio<TServicio, TImplementacion>() where TImplementacion : TServicio;

        /// <summary>La clase concreta <typeparamref name="T"/>, nueva cada vez (lo que Unity hacía solo sin registrarla).</summary>
        void Transitorio<T>() where T : class;

        /// <summary>Una sola instancia de <paramref name="implementacion"/>, creada la primera vez que se pide <paramref name="servicio"/>.</summary>
        void Singleton(Type servicio, Type implementacion);

        /// <summary>Una sola instancia de <typeparamref name="TImplementacion"/>, creada la primera vez que se pide <typeparamref name="TServicio"/>.</summary>
        void Singleton<TServicio, TImplementacion>() where TImplementacion : TServicio;

        /// <summary>Una sola instancia, la que devuelve <paramref name="fabrica"/> la primera vez que se pide <paramref name="servicio"/>.</summary>
        void Singleton(Type servicio, Func<IResolutorNesto, object> fabrica);

        /// <summary>Una sola instancia, la que devuelve <paramref name="fabrica"/> la primera vez que se pide <typeparamref name="TServicio"/>.</summary>
        void Singleton<TServicio>(Func<IResolutorNesto, TServicio> fabrica);

        /// <summary>Siempre <paramref name="instancia"/>, ya creada.</summary>
        void Instancia(Type servicio, object instancia);

        /// <summary>Siempre <paramref name="instancia"/>, ya creada.</summary>
        void Instancia<TServicio>(TServicio instancia);

        /// <summary>
        /// La vista a la que se navega por su nombre (<see cref="IResolutorNesto.ResolverVista"/>), nueva cada vez. Es lo que
        /// con Prism era <c>Register&lt;object, TVista&gt;(nombre)</c>.
        /// </summary>
        void Vista(string nombre, Type tipoVista);

        /// <summary>La vista <typeparamref name="TVista"/>, a la que se navega por su nombre, nueva cada vez.</summary>
        void Vista<TVista>(string nombre) where TVista : class;

        /// <summary>
        /// El diálogo que se abre por su nombre con <see cref="IServicioDialogos"/> (lo busca <see cref="IResolutorNesto.Dialogo"/>).
        /// Normalmente se registra con <c>ControlesUsuario.Dialogs.RegistroDialogos.RegistrarDialogo</c>.
        /// </summary>
        void Dialogo(string nombre, DialogoRegistrado dialogo);
    }
}
