namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.4, 7.º tramo): una vista (o su DataContext) que navega dentro de un ámbito de regiones propio: los
    /// maestro-detalle de PedidoVenta y PedidoCompra, donde cada pestaña tiene SU región de lista y SU región de detalle,
    /// con los mismos nombres que las de las otras pestañas iguales. Sustituye a pasar a mano el <c>IRegionManager</c> con
    /// ámbito de Prism (<c>scopedRegionManager</c>, <c>CambiarRegionManager</c>…).
    ///
    /// No hay que asignarlo: lo hace <see cref="IServicioNavegacion"/> al meter la vista en una región.
    /// <list type="bullet">
    /// <item><see cref="IServicioNavegacion.AbrirVistaConAmbito"/>: la vista recibe la navegación del ámbito NUEVO que se
    /// crea para ella (la de sus propias regiones), antes de activarse.</item>
    /// <item><see cref="IServicioNavegacion.AbrirVistaNueva"/>: recibe la navegación del ámbito de la región donde entra.
    /// Así la lista que la vista maestra pone en su región de lista navega al detalle de ESA pestaña.</item>
    /// </list>
    /// Se entrega a la vista y a su DataContext, a cada uno si lo implementa.
    /// </summary>
    public interface IConAmbitoNavegacion
    {
        IServicioNavegacion NavegacionAmbito { get; set; }
    }
}
