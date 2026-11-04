using Nesto.Infrastructure.Contracts;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#579 / Nesto#618: quién puede crear notas de entrega, de recogida y de cobro: Almacén, Administración, Dirección e
    /// Informática (los mismos grupos que deja la API, <c>PermisosNotasEntrega</c> de NestoAPI). Lo usan el detalle del pedido y la
    /// ficha del cliente.
    /// </summary>
    public static class PermisosNotasEntrega
    {
        /// <summary>Nombre con el que se registra el diálogo «Nueva nota de cobro» (módulo Cliente; lo abre también PedidoVenta).</summary>
        public const string DIALOGO_NOTA_COBRO = "NotaCobroDialog";

        public static bool Puede(IConfiguracion configuracion)
        {
            if (configuracion == null)
            {
                return false;
            }
            return configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA);
        }
    }
}
