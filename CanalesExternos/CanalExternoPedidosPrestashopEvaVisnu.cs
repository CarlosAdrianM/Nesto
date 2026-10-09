using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.ApisExternas;

namespace Nesto.Modulos.CanalesExternos
{
    /// <summary>
    /// Nesto#520: pedidos de la tienda de Eva Visnú (evavisnu.com). Igual que la de Nueva Visión salvo la serie
    /// (EV); la clave (PrestashopWebserviceKeyEV) va en la URL porque esa tienda no acepta la autenticación básica.
    /// </summary>
    public sealed class CanalExternoPedidosPrestashopEvaVisnu : CanalExternoPedidosPrestashop
    {
        public CanalExternoPedidosPrestashopEvaVisnu(IConfiguracion configuracion, Interfaces.IClientesPorTelefonoService clientesLookup)
            : base(configuracion, clientesLookup, TiendaPrestashop.EvaVisnu)
        {
        }
    }
}
