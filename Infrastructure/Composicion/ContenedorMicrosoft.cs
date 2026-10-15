using Microsoft.Extensions.DependencyInjection;
using Nesto.Infrastructure.Contracts;
using System;

namespace Nesto.Infrastructure.Composicion
{
    /// <summary>
    /// Nesto#490 (4C.5): <see cref="IRegistroNesto"/> sobre <see cref="IServiceCollection"/> de
    /// Microsoft.Extensions.DependencyInjection. En el tramo 1 solo lo usa la prueba sombra (la composición de Nesto
    /// montada sobre MS.DI con <c>ValidateOnBuild</c>); en el tramo 4 sustituye a Prism/Unity.
    /// <list type="bullet">
    /// <item>Las vistas por nombre son servicios con clave (<c>AddKeyedTransient(typeof(object), nombre, tipo)</c>).</item>
    /// <item>Los diálogos, instancias con clave de <see cref="DialogoRegistrado"/>.</item>
    /// </list>
    /// </summary>
    public sealed class ContenedorMicrosoft : RegistroNestoBase
    {
        public ContenedorMicrosoft() : this(new ServiceCollection())
        {
        }

        public ContenedorMicrosoft(IServiceCollection servicios)
        {
            Servicios = servicios ?? throw new ArgumentNullException(nameof(servicios));
        }

        /// <summary>Los registros hechos hasta ahora.</summary>
        public IServiceCollection Servicios { get; }

        public override void Transitorio(Type servicio, Type implementacion)
            => _ = Servicios.AddTransient(servicio, implementacion);

        public override void Singleton(Type servicio, Type implementacion)
            => _ = Servicios.AddSingleton(servicio, implementacion);

        public override void Singleton(Type servicio, Func<IResolutorNesto, object> fabrica)
        {
            if (fabrica == null)
            {
                throw new ArgumentNullException(nameof(fabrica));
            }
            _ = Servicios.AddSingleton(servicio, proveedor => fabrica(new ResolutorMicrosoft(proveedor)));
        }

        public override void Instancia(Type servicio, object instancia)
            => _ = Servicios.AddSingleton(servicio, instancia ?? throw new ArgumentNullException(nameof(instancia)));

        public override void Vista(string nombre, Type tipoVista)
        {
            ComprobarNombre(nombre, "La vista");
            _ = Servicios.AddKeyedTransient(typeof(object), nombre, tipoVista);
        }

        public override void Dialogo(string nombre, DialogoRegistrado dialogo)
        {
            ComprobarNombre(nombre, "El diálogo");
            _ = Servicios.AddKeyedSingleton(typeof(DialogoRegistrado), nombre, dialogo ?? throw new ArgumentNullException(nameof(dialogo)));
        }

        /// <summary>
        /// Construye el contenedor. Con <paramref name="validar"/> (lo normal), <c>ValidateOnBuild</c> y <c>ValidateScopes</c>:
        /// lanza al construirlo si a algún registro le falta una dependencia.
        /// </summary>
        public ResolutorMicrosoft Construir(bool validar = true)
        {
            ServiceProvider proveedor = Servicios.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = validar,
                ValidateScopes = validar
            });
            return new ResolutorMicrosoft(proveedor);
        }
    }

    /// <summary>Nesto#490 (4C.5): <see cref="IResolutorNesto"/> sobre el <see cref="IServiceProvider"/> de MS.DI.</summary>
    public sealed class ResolutorMicrosoft : IResolutorNesto
    {
        public ResolutorMicrosoft(IServiceProvider proveedor)
        {
            Proveedor = proveedor ?? throw new ArgumentNullException(nameof(proveedor));
        }

        public IServiceProvider Proveedor { get; }

        /// <summary>
        /// Lo registrado o, como hacía Unity, una clase concreta sin registrar construida con sus dependencias
        /// (<see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/>).
        /// </summary>
        public object Resolver(Type tipo)
            => Proveedor.GetService(tipo) ?? ActivatorUtilities.CreateInstance(Proveedor, tipo);

        public T Resolver<T>() => (T)Resolver(typeof(T));

        public object ResolverVista(string nombre) => Proveedor.GetRequiredKeyedService(typeof(object), nombre);

        public bool TieneVista(string nombre) => EsServicioConClave(typeof(object), nombre);

        public DialogoRegistrado Dialogo(string nombre)
            => EsServicioConClave(typeof(DialogoRegistrado), nombre)
                ? (DialogoRegistrado)Proveedor.GetRequiredKeyedService(typeof(DialogoRegistrado), nombre)
                : null;

        private bool EsServicioConClave(Type tipo, string nombre)
            => !string.IsNullOrWhiteSpace(nombre)
               && Proveedor.GetService(typeof(IServiceProviderIsKeyedService)) is IServiceProviderIsKeyedService claves
               && claves.IsKeyedService(tipo, nombre);
    }
}
