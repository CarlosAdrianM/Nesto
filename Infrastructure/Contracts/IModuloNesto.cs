namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.5): un módulo de Nesto (pedidos de venta, clientes, cajas…) sin el <c>IModule</c> de Prism.
    /// El arranque llama a <see cref="Registrar"/> en todos los módulos, construye el contenedor y después llama a
    /// <see cref="Inicializar"/> en todos.
    /// </summary>
    public interface IModuloNesto
    {
        /// <summary>
        /// Registra las vistas, los diálogos, los servicios y los tipos concretos del módulo (su menú de la cinta y los
        /// ViewModels incluidos). Es el único sitio donde el módulo registra.
        /// </summary>
        void Registrar(IRegistroNesto registro);

        /// <summary>
        /// Se llama cuando ya está todo registrado (normalmente, para poner su menú en la cinta con <see cref="IServicioCinta"/>).
        /// Aquí no se registra nada: con MS.DI el contenedor ya está construido y no admite registros nuevos.
        /// </summary>
        void Inicializar(IResolutorNesto resolutor);
    }
}
