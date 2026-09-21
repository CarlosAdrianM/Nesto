using Nesto.Infrastructure.Contracts;
using System;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// ¿Puede este usuario crear o modificar un pedido que no pasa la validación del servidor?
    /// Quien puede, ve el "¿Desea crearlo de todos modos?" y reintenta con
    /// <c>CreadoSinPasarValidacion</c>; quien no, ve el error y ya está.
    ///
    /// <para>NestoAPI#501: hasta hoy esto se miraba solo por grupo de Windows (Dirección, Almacén y
    /// Tiendas con todas las líneas en su almacén), pero el servidor lleva tiempo aceptando además
    /// el parámetro de usuario <c>PermitirCrearPedidoConErroresValidacion</c>
    /// (<c>PedidosVentaController.TieneParametroPermitirOmitirValidacion</c>). Al no mirarlo aquí,
    /// a quien solo tenía el parámetro NUNCA se le llegaba a ofrecer la pregunta: el cliente se
    /// rendía antes de preguntar. Hacía falta para que Manuel pueda vender las familias
    /// restringidas (Kinetics) a clientes que compraron Faby o Greenik.</para>
    ///
    /// <para>NestoApp ya hacía lo correcto (<c>usuario.permitirCrearPedidoConErroresValidacion</c>).</para>
    /// </summary>
    public static class PermisosValidacionPedido
    {
        /// <summary>
        /// <paramref name="todasLasLineasEnElAlmacenDelUsuario"/> solo se evalúa si el usuario está
        /// en el grupo Tiendas, que es el único al que se le exige.
        /// </summary>
        public static async Task<bool> PuedeOmitirValidacion(
            IConfiguracion configuracion,
            string empresa,
            Func<bool> todasLasLineasEnElAlmacenDelUsuario)
        {
            if (configuracion == null)
            {
                return false;
            }

            if (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN))
            {
                return true;
            }

            if (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)
                && todasLasLineasEnElAlmacenDelUsuario != null
                && todasLasLineasEnElAlmacenDelUsuario())
            {
                return true;
            }

            return await TieneParametro(configuracion, empresa).ConfigureAwait(false);
        }

        /// <summary>
        /// El mismo criterio que el servidor: "1", "TRUE", "SI" o "SÍ", sin distinguir mayúsculas.
        /// Si la lectura falla, NO se ofrece forzar: es el lado seguro.
        /// </summary>
        private static async Task<bool> TieneParametro(IConfiguracion configuracion, string empresa)
        {
            try
            {
                string valor = await configuracion.leerParametro(
                    empresa, Parametros.Claves.PermitirCrearPedidoConErroresValidacion).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(valor))
                {
                    return false;
                }

                valor = valor.Trim().ToUpperInvariant();
                return valor == "1" || valor == "TRUE" || valor == "SI" || valor == "SÍ";
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
