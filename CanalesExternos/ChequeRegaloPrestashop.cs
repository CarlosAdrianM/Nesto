using Nesto.Infrastructure.Models;
using Nesto.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nesto.Modulos.CanalesExternos
{
    /// <summary>
    /// Nesto#533: una regla de carrito aplicada a un pedido de la tienda (recurso <c>order_cart_rules</c> del
    /// webservice, con el código sacado de <c>cart_rules</c>).
    /// </summary>
    public class ReglaCarritoPrestashop
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        /// <summary>Lo que descontó la tienda, con IVA (<c>value</c>).</summary>
        public decimal ValorConIva { get; set; }
        /// <summary>Lo que descontó la tienda, sin IVA (<c>value_tax_excl</c>).</summary>
        public decimal ValorSinIva { get; set; }
    }

    /// <summary>Qué hacer con el cheque regalo de un pedido de la tienda.</summary>
    public class DecisionChequeRegaloPrestashop
    {
        public static readonly DecisionChequeRegaloPrestashop Ninguna = new();

        /// <summary>La línea que se añade al pedido: la del cheque (−1) o, si no se puede canjear, el cupón (TiCKET).</summary>
        public LineaPedidoVentaDTO Linea { get; set; }
        /// <summary>Lo que hay que quitar de total_discounts antes de repartir el resto como cupón.</summary>
        public decimal DescuentoSinIva { get; set; }
        public decimal DescuentoConIva { get; set; }
        /// <summary>Texto para los comentarios del pedido (null si no hay nada que revisar).</summary>
        public string Aviso { get; set; }
        public bool Canjeado { get; set; }
    }

    /// <summary>
    /// Nesto#533: el cheque regalo canjeado en la tienda PrestaShop (módulo nestocheques, prestashop-nestologin#8).
    /// El módulo aplica una regla de carrito con código <c>NVCHEQUE-{Campana}-{cliente}</c>; al importar el pedido,
    /// ese descuento no se reparte como cupón sino que va como la línea del cheque (producto de la campaña, cantidad
    /// −1), que NestoAPI normaliza, valida y reserva al grabar el pedido, como en Nesto y TiendasNuevaVision.
    /// Si no se puede canjear (cliente genérico, otro cliente, cheque ya usado…), el descuento queda como cupón y
    /// se avisa en los comentarios para revisarlo a mano: el importe cobrado por la tienda tiene que cuadrar.
    /// </summary>
    public static class ChequeRegaloPrestashop
    {
        public const string PREFIJO_CODIGO = "NVCHEQUE-";
        internal const string CLIENTE_TIENDA_ONLINE = "31517";
        internal const string AVISO = "ATENCIÓN CHEQUE REGALO: ";

        public static bool EsCodigoCheque(string codigo)
            => codigo != null && codigo.Trim().StartsWith(PREFIJO_CODIGO, StringComparison.OrdinalIgnoreCase);

        /// <summary>«NVCHEQUE-CHEQUE50_OCT_2026-15191» → campaña y cliente. False si no tiene esa forma.</summary>
        public static bool LeerCodigo(string codigo, out string campana, out string cliente)
        {
            campana = null;
            cliente = null;
            if (!EsCodigoCheque(codigo))
            {
                return false;
            }
            string resto = codigo.Trim()[PREFIJO_CODIGO.Length..];
            int guion = resto.LastIndexOf('-');
            if (guion <= 0 || guion == resto.Length - 1)
            {
                return false;
            }
            campana = resto[..guion].Trim();
            cliente = resto[(guion + 1)..].Trim();
            return true;
        }

        /// <summary>Los importes del webservice («50.000000»), sin depender de la cultura del puesto.</summary>
        internal static decimal LeerImporte(string valor)
            => decimal.TryParse(valor?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal importe) ? importe : 0;

        /// <summary>
        /// Decide qué hacer con las reglas NVCHEQUE-* del pedido. <paramref name="cheque"/> es lo que contesta
        /// GET api/ChequesRegalo/Cliente para el cliente del pedido (null si no tiene, si la API no contesta o si no se
        /// ha podido preguntar).
        /// </summary>
        public static DecisionChequeRegaloPrestashop Decidir(IEnumerable<ReglaCarritoPrestashop> reglas, string clientePedido,
            ChequeRegaloClienteDTO cheque, Func<decimal, string, LineaPedidoVentaDTO> crearLineaCupon, Func<ChequeRegaloClienteDTO, LineaPedidoVentaDTO> crearLineaCheque)
        {
            List<ReglaCarritoPrestashop> reglasCheque = (reglas ?? Enumerable.Empty<ReglaCarritoPrestashop>())
                .Where(r => EsCodigoCheque(r.Codigo)).ToList();
            if (!reglasCheque.Any())
            {
                return DecisionChequeRegaloPrestashop.Ninguna;
            }

            decimal sinIva = reglasCheque.Sum(r => r.ValorSinIva);
            decimal conIva = reglasCheque.Sum(r => r.ValorConIva);
            string codigos = string.Join(", ", reglasCheque.Select(r => r.Codigo.Trim()));
            string motivo = MotivoParaNoCanjear(reglasCheque, clientePedido?.Trim(), cheque);
            if (motivo != null)
            {
                return new DecisionChequeRegaloPrestashop
                {
                    Linea = crearLineaCupon(conIva, "CHEQUE REGALO NO APLICADO: REVISAR"),
                    DescuentoSinIva = sinIva,
                    DescuentoConIva = conIva,
                    Aviso = $"{AVISO}el pedido trae la regla {codigos} ({sinIva:0.00} € sin IVA) pero no se ha podido " +
                        $"aplicar el cheque: {motivo} Se ha dejado como cupón de descuento; revisar a mano."
                };
            }
            return new DecisionChequeRegaloPrestashop
            {
                Linea = crearLineaCheque(cheque),
                DescuentoSinIva = sinIva,
                DescuentoConIva = conIva,
                Canjeado = true
            };
        }

        internal static string MotivoParaNoCanjear(List<ReglaCarritoPrestashop> reglasCheque, string clientePedido, ChequeRegaloClienteDTO cheque)
        {
            if (reglasCheque.Count > 1)
            {
                return "hay más de una regla de cheque regalo en el pedido.";
            }
            if (!LeerCodigo(reglasCheque[0].Codigo, out string campana, out string clienteCodigo))
            {
                return "el código no tiene la forma NVCHEQUE-{campaña}-{cliente}.";
            }
            if (string.IsNullOrWhiteSpace(clientePedido) || clientePedido == CLIENTE_TIENDA_ONLINE)
            {
                return "el pedido ha entrado con el cliente genérico de la tienda online (el NIF de la dirección no es de ninguna ficha).";
            }
            if (!string.Equals(clienteCodigo, clientePedido, StringComparison.OrdinalIgnoreCase))
            {
                return $"el cheque es del cliente {clienteCodigo} y el pedido ha entrado con el cliente {clientePedido} (por el NIF de la dirección).";
            }
            if (cheque == null)
            {
                return $"NestoAPI no devuelve ningún cheque regalo del cliente {clientePedido} (o no ha contestado).";
            }
            if (!string.Equals(cheque.Campana?.Trim(), campana, StringComparison.OrdinalIgnoreCase))
            {
                return $"el cheque del cliente es de la campaña {cheque.Campana?.Trim()}, no de la {campana}.";
            }
            if (!cheque.SePuedeUsar)
            {
                return string.IsNullOrWhiteSpace(cheque.Mensaje) ? $"el cheque está {cheque.Estado}." : cheque.Mensaje.Trim();
            }
            if (Math.Round(reglasCheque[0].ValorSinIva, 2, MidpointRounding.AwayFromZero) != Math.Round(cheque.Importe, 2, MidpointRounding.AwayFromZero))
            {
                return $"la tienda ha descontado {reglasCheque[0].ValorSinIva:0.00} € sin IVA y el cheque es de {cheque.Importe:0.00} €.";
            }
            return null;
        }
    }
}
