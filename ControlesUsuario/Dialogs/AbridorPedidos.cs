namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// NestoAPI#555: abre un pedido de venta en una pestaña. La campana de notificaciones lo usa para que
    /// pulsar un aviso que habla de un pedido (p. ej. «el pedido ha cogido picking») lleve a ese pedido.
    /// La implementación vive en la aplicación (módulo de pedidos), así ControlesUsuario no depende de él.
    /// </summary>
    public interface IAbridorPedidos
    {
        void Abrir(string empresa, int pedido);
    }
}
