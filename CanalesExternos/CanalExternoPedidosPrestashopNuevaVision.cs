using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.ApisExternas;

namespace Nesto.Modulos.CanalesExternos
{
    /// <summary>Pedidos de la tienda de Nueva Visión (productosdeesteticaypeluqueriaprofesional.com), serie NV.</summary>
    public sealed class CanalExternoPedidosPrestashopNuevaVision : CanalExternoPedidosPrestashop
    {
        public CanalExternoPedidosPrestashopNuevaVision(IConfiguracion configuracion, Interfaces.IClientesPorTelefonoService clientesLookup)
            : base(configuracion, clientesLookup, TiendaPrestashop.NuevaVision)
        {
        }
    }
}
