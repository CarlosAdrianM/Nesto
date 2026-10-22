using Nesto.Infrastructure.Models;
using Nesto.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.CanalesExternos
{
    /// <summary>Qué hacer con el cupón de Nesto de un pedido de la tienda.</summary>
    public class DecisionCuponPrestashop
    {
        public static readonly DecisionCuponPrestashop Ninguna = new();

        /// <summary>La línea que se añade al pedido: la del cupón (con su código) o, si no se puede aplicar, un TiCKET.</summary>
        public LineaPedidoVentaDTO Linea { get; set; }
        /// <summary>Lo que hay que quitar de total_discounts antes de repartir el resto como cupón de la tienda.</summary>
        public decimal DescuentoSinIva { get; set; }
        public decimal DescuentoConIva { get; set; }
        /// <summary>Texto para los comentarios del pedido (null si no hay nada que revisar).</summary>
        public string Aviso { get; set; }
        /// <summary>El pedido lleva la línea del cupón con su código: NestoAPI lo reserva al grabar.</summary>
        public bool Aplicado { get; set; }
        /// <summary>
        /// El cupón existe en Nesto pero ya estaba usado o anulado (se ha gastado en los dos sitios): tras crear el pedido se le
        /// dice a NestoAPI (POST api/Cupones/{codigo}/Usar, NestoAPI#614) para que avise a Administración. Null si no hace falta.
        /// </summary>
        public string CodigoParaAvisar { get; set; }
    }

    /// <summary>
    /// Nesto#518 (NestoAPI#614): el cupón de un evento (o manual) gastado en la tienda PrestaShop. NestoAPI es el dueño de los
    /// cupones y PrestaShop un espejo: la regla de carrito tiene el mismo código que el cupón (EV-2610-7K3Q, MN-…). Al importar
    /// el pedido, ese descuento no se reparte como cupón de la tienda sino que va como la línea del cupón (CodigoCupon), que
    /// NestoAPI normaliza (base con el IVA y el recargo del cliente), valida y reserva al grabar, como en Nesto y TNV.
    /// Si no se puede aplicar (cliente genérico, de otro cliente, ya usado o anulado en Nesto, importe distinto…), el pedido
    /// entra igual: el descuento va como TiCKET y se avisa en los comentarios para revisarlo, porque lo cobrado por la
    /// tienda tiene que cuadrar.
    /// </summary>
    public static class CuponPrestashop
    {
        internal const string AVISO = "ATENCIÓN CUPÓN: ";

        public static bool EsCodigoCupon(string codigo) => ReglasCupon.EsCodigoCupon(codigo);

        /// <summary>
        /// Decide qué hacer con las reglas de cupón del pedido. <paramref name="cupon"/> es lo que contesta GET api/Cupones/{codigo}
        /// para el código de la regla (null si no existe, si la API no contesta o si no se ha podido preguntar).
        /// <paramref name="pedidoCanalId"/> es la referencia del pedido en la tienda (Nesto#547: si el módulo de la tienda ya ha
        /// marcado el cupón como usado en este mismo pedido, se aplica).
        /// </summary>
        public static DecisionCuponPrestashop Decidir(IEnumerable<ReglaCarritoPrestashop> reglas, string clientePedido, CuponDTO cupon,
            Func<decimal, string, LineaPedidoVentaDTO> crearLineaTicket, Func<CuponDTO, LineaPedidoVentaDTO> crearLineaCupon,
            string pedidoCanalId = null)
        {
            List<ReglaCarritoPrestashop> reglasCupon = (reglas ?? Enumerable.Empty<ReglaCarritoPrestashop>())
                .Where(r => EsCodigoCupon(r.Codigo)).ToList();
            if (!reglasCupon.Any())
            {
                return DecisionCuponPrestashop.Ninguna;
            }

            // A 4 decimales, como total_discounts del pedido (AnadirLineasAsync): si no, el resto que se reparte como descuento
            // de la tienda arrastra decimales (71,3223 − 41,322314) y deja de reconocerse un 10 %
            decimal sinIva = Math.Round(reglasCupon.Sum(r => r.ValorSinIva), 4, MidpointRounding.AwayFromZero);
            decimal conIva = Math.Round(reglasCupon.Sum(r => r.ValorConIva), 4, MidpointRounding.AwayFromZero);
            string codigos = string.Join(", ", reglasCupon.Select(r => r.Codigo.Trim().ToUpperInvariant()));
            string motivo = MotivoParaNoAplicar(reglasCupon, clientePedido?.Trim(), cupon, pedidoCanalId);
            if (motivo != null)
            {
                return new DecisionCuponPrestashop
                {
                    Linea = crearLineaTicket(conIva, "CUPÓN NO APLICADO: REVISAR"),
                    DescuentoSinIva = sinIva,
                    DescuentoConIva = conIva,
                    Aviso = $"{AVISO}el pedido trae el cupón {codigos} ({conIva:0.00} € con IVA) pero no se ha podido aplicar en Nesto: {motivo} " +
                        "Se ha dejado como descuento (TiCKET); revisar a mano.",
                    CodigoParaAvisar = YaGastadoEnNesto(cupon) ? cupon.Codigo.Trim() : null
                };
            }
            return new DecisionCuponPrestashop
            {
                Linea = crearLineaCupon(cupon),
                DescuentoSinIva = sinIva,
                DescuentoConIva = conIva,
                Aplicado = true
            };
        }

        /// <summary>Usado o anulado en Nesto: el cliente lo ha gastado en los dos sitios (o se anuló y el espejo no llegó).</summary>
        internal static bool YaGastadoEnNesto(CuponDTO cupon)
        {
            string estado = cupon?.Estado?.Trim();
            return !string.IsNullOrWhiteSpace(cupon?.Codigo) &&
                (string.Equals(estado, ReglasCupon.ESTADO_USADO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, ReglasCupon.ESTADO_ANULADO, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Nesto#547 (NestoAPI#694): el módulo de la tienda ya ha marcado el cupón como usado en ESTE pedido de la tienda (Usado,
        /// sin pedido de Nesto todavía, canal PrestaShop y la misma referencia). No se mira la caducidad ni SePuedeUsar: se puede
        /// haber usado el último día de validez e importarse al día siguiente. La API acepta la línea y la liga al pedido.
        /// </summary>
        internal static bool UsadoEnEstePedidoDeLaTienda(CuponDTO cupon, string pedidoCanalId)
        {
            return cupon != null && !string.IsNullOrWhiteSpace(pedidoCanalId)
                && string.Equals(cupon.Estado?.Trim(), ReglasCupon.ESTADO_USADO, StringComparison.OrdinalIgnoreCase)
                && cupon.UsadoEnPedido == null
                && string.Equals(cupon.CanalUso?.Trim(), "PrestaShop", StringComparison.OrdinalIgnoreCase)
                && string.Equals(cupon.PedidoCanal?.Trim(), pedidoCanalId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        internal static string MotivoParaNoAplicar(List<ReglaCarritoPrestashop> reglasCupon, string clientePedido, CuponDTO cupon, string pedidoCanalId = null)
        {
            if (reglasCupon.Count > 1)
            {
                return "hay más de un cupón en el pedido y solo se puede usar uno por pedido.";
            }
            if (string.IsNullOrWhiteSpace(clientePedido) || clientePedido == ChequeRegaloPrestashop.CLIENTE_TIENDA_ONLINE)
            {
                return "el pedido ha entrado con el cliente genérico de la tienda online (el NIF de la dirección no es de ninguna ficha).";
            }
            if (cupon == null)
            {
                return $"NestoAPI no encuentra el cupón {reglasCupon[0].Codigo.Trim().ToUpperInvariant()} (o no ha contestado).";
            }
            if (!string.Equals(cupon.Cliente?.Trim(), clientePedido, StringComparison.OrdinalIgnoreCase))
            {
                return $"el cupón es del cliente {cupon.Cliente?.Trim()} y el pedido ha entrado con el cliente {clientePedido} (por el NIF de la dirección).";
            }
            if (!cupon.SePuedeUsar && !UsadoEnEstePedidoDeLaTienda(cupon, pedidoCanalId))
            {
                return string.IsNullOrWhiteSpace(cupon.Mensaje) ? $"el cupón está {cupon.Estado?.Trim()}." : cupon.Mensaje.Trim();
            }
            if (Math.Round(reglasCupon[0].ValorConIva, 2, MidpointRounding.AwayFromZero) != Math.Round(cupon.Importe, 2, MidpointRounding.AwayFromZero))
            {
                return $"la tienda ha descontado {reglasCupon[0].ValorConIva:0.00} € con IVA y el cupón es de {cupon.Importe:0.00} €.";
            }
            return null;
        }
    }
}
