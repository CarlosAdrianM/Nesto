using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Nesto#518 (NestoAPI#613): un cupón de descuento de un evento (o manual), tal y como lo contestan
    /// <c>GET api/Clientes/{empresa}/{cliente}/Cupones</c> y <c>GET api/Cupones/{codigo}</c>. Las reglas las pone la API:
    /// aquí solo se enseña y se manda la línea con el código.
    /// </summary>
    public class CuponDTO
    {
        public int Id { get; set; }
        /// <summary>«EV-2610-7K3Q» (evento) o «MN-2610-…» (manual).</summary>
        public string Codigo { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        /// <summary>IVA incluido: lo que se descuenta del pedido.</summary>
        public decimal Importe { get; set; }
        /// <summary>El pedido tiene que llevar al menos esto de productos, IVA incluido (el doble del cupón).</summary>
        public decimal PedidoMinimo { get; set; }
        /// <summary>Evento o Manual.</summary>
        public string Origen { get; set; }
        public int? EventoId { get; set; }
        public string Evento { get; set; }
        /// <summary>El motivo de un cupón manual.</summary>
        public string Motivo { get; set; }
        public DateTime FechaEmision { get; set; }
        public DateTime ValidoDesde { get; set; }
        public DateTime Caducidad { get; set; }
        /// <summary>Emitido, Activo, Usado, Anulado o Caducado.</summary>
        public string Estado { get; set; }
        /// <summary>Solo true con Activo.</summary>
        public bool SePuedeUsar { get; set; }
        /// <summary>La frase para el usuario, redactada por la API («Tiene un cupón de 50,00 €…»).</summary>
        public string Mensaje { get; set; }
        /// <summary>El producto de la línea del cupón (CUPONEVENTO).</summary>
        public string Producto { get; set; }
        /// <summary>«Cupón EV-2610-7K3Q (Masterclass Cloasma)»: el texto que pondrá la API en la línea.</summary>
        public string TextoLinea { get; set; }
        public int? UsadoEnPedido { get; set; }
        public string FacturaUso { get; set; }
        public DateTime? FechaUso { get; set; }
        public string FacturaOrigen { get; set; }
        /// <summary>
        /// Nesto#547 (NestoAPI#694): dónde se usó: «PrestaShop» si lo canjeó la tienda online (entonces UsadoEnPedido es null
        /// hasta que entra el pedido en Nesto). Null en los usados en Nesto.
        /// </summary>
        public string CanalUso { get; set; }
        /// <summary>Nesto#547: la referencia del pedido de la tienda donde se usó (la que Nesto guarda como PedidoCanalId).</summary>
        public string PedidoCanal { get; set; }
        /// <summary>Nesto#547: lo que descontó la tienda, IVA incluido.</summary>
        public decimal? ImporteDescontado { get; set; }
    }

    /// <summary>Nesto#518: <c>POST api/Cupones</c>, cupón sin evento (solo Dirección, con motivo obligatorio).</summary>
    public class CrearCuponManualDTO
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        /// <summary>IVA incluido.</summary>
        public decimal Importe { get; set; }
        public string Motivo { get; set; }
        /// <summary>Opcional: si no va, hoy.</summary>
        public DateTime? ValidoDesde { get; set; }
        /// <summary>
        /// Carlos 29/10/26: el pedido mínimo (€, productos con IVA) para usar el cupón. Opcional: si no va, la API pone 200.
        /// </summary>
        public decimal? PedidoMinimo { get; set; }
    }

    /// <summary>Nesto#518: <c>POST api/Cupones/{codigo}/Anular</c> (Administración y Dirección).</summary>
    public class AnularCuponDTO
    {
        /// <summary>Opcional: si no va, la API pone «Devolución del curso.».</summary>
        public string Motivo { get; set; }
    }

    /// <summary>
    /// Nesto#518 (NestoAPI#614, pendiente en la API): un cupón que el cliente ha gastado en la tienda PrestaShop.
    /// Contrato supuesto de <c>POST api/Cupones/{codigo}/Usar</c>: con una API que no lo tenga, 404 y no pasa nada.
    /// </summary>
    public class UsoCuponTiendaDTO
    {
        public string Empresa { get; set; }
        public int Pedido { get; set; }
        /// <summary>«PrestaShop».</summary>
        public string Canal { get; set; }
        /// <summary>La referencia del pedido en la tienda.</summary>
        public string PedidoCanal { get; set; }
        /// <summary>Lo que descontó la tienda, IVA incluido.</summary>
        public decimal ImporteDescontado { get; set; }
    }

    /// <summary>Nesto#518: lo que comparten la plantilla, el detalle del pedido, la ficha del cliente y CanalesExternos.</summary>
    public static class ReglasCupon
    {
        public const string ESTADO_EMITIDO = "Emitido";
        public const string ESTADO_ACTIVO = "Activo";
        public const string ESTADO_USADO = "Usado";
        public const string ESTADO_ANULADO = "Anulado";
        public const string ESTADO_CADUCADO = "Caducado";

        public const string ORIGEN_EVENTO = "Evento";
        public const string ORIGEN_MANUAL = "Manual";

        /// <summary>El producto ficticio de la línea (NestoAPI Cupones:Producto). Manda el que diga la API en el DTO.</summary>
        public const string PRODUCTO_POR_DEFECTO = "CUPONEVENTO";

        /// <summary>El IVA con el que la plantilla saca la base aproximada (la exacta, con el recargo, la pone la API).</summary>
        public const decimal PORCENTAJE_IVA = 21M;

        private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");
        private static readonly Regex FormatoCodigo = new(@"^(EV|MN)-\d{4}-[0-9A-Z]{4}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>«50 €» o «12,50 €».</summary>
        public static string Euros(decimal importe)
        {
            return importe.ToString(importe == decimal.Truncate(importe) ? "N0" : "N2", Espanol) + " €";
        }

        public static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy", Espanol);

        /// <summary>¿Tiene la forma de un código de cupón de Nesto (EV-2610-7K3Q, MN-2610-…)?</summary>
        public static bool EsCodigoCupon(string codigo) => !string.IsNullOrWhiteSpace(codigo) && FormatoCodigo.IsMatch(codigo.Trim());

        /// <summary>El producto de la línea del cupón: el del DTO o, si no viene, CUPONEVENTO.</summary>
        public static string ProductoDe(CuponDTO cupon)
        {
            return string.IsNullOrWhiteSpace(cupon?.Producto) ? PRODUCTO_POR_DEFECTO : cupon.Producto.Trim();
        }

        /// <summary>¿Es esta línea la de un cupón? (el producto del cupón, sin mirar mayúsculas ni el relleno de espacios)</summary>
        public static bool EsLineaDeCupon(string producto, CuponDTO cupon = null)
        {
            return !string.IsNullOrWhiteSpace(producto) && string.Equals(producto.Trim(), ProductoDe(cupon), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// La base de la línea −1 para que el cupón descuente su importe exacto: importe / (1 + IVA + RE), a 4 decimales como
        /// LinPedidoVta.Precio (50 € al 21 % → 41,3223; con RE del 5,2 % → 39,6197). Sin IVA en el pedido, la base es el importe.
        /// Es lo que hace la API al grabar (NestoAPI#613, decisión de Carlos del 15/10/26); aquí es para que el total cuadre antes.
        /// </summary>
        /// <param name="porcentajeIva">En tanto por cien (21).</param>
        /// <param name="porcentajeRecargo">En tanto por cien (5,2).</param>
        public static decimal BaseLinea(decimal importe, bool conIva, decimal porcentajeIva = PORCENTAJE_IVA, decimal porcentajeRecargo = 0)
        {
            if (!conIva)
            {
                return importe;
            }
            return Math.Round(importe / (1 + (porcentajeIva + porcentajeRecargo) / 100M), 4, MidpointRounding.AwayFromZero);
        }

        /// <summary>«Usar el cupón de 50 €»</summary>
        public static string TextoUsar(CuponDTO cupon) => cupon == null ? null : $"Usar el cupón de {Euros(cupon.Importe)}";

        /// <summary>«Añadir el cupón de 50 €»</summary>
        public static string TextoAnadir(CuponDTO cupon) => cupon == null ? null : $"Añadir el cupón de {Euros(cupon.Importe)}";

        /// <summary>El texto de la línea (si la API no lo manda, uno parecido; la API pone el suyo al guardar).</summary>
        public static string TextoLinea(CuponDTO cupon)
        {
            if (cupon == null)
            {
                return null;
            }
            return string.IsNullOrWhiteSpace(cupon.TextoLinea) ? $"Cupón {cupon.Codigo?.Trim()}" : cupon.TextoLinea.Trim();
        }

        /// <summary>El mensaje de la API o, si no viene, uno genérico.</summary>
        public static string Mensaje(CuponDTO cupon)
        {
            if (cupon == null)
            {
                return null;
            }
            if (!string.IsNullOrWhiteSpace(cupon.Mensaje))
            {
                return cupon.Mensaje.Trim();
            }
            return cupon.SePuedeUsar
                ? $"Tiene un cupón de {Euros(cupon.Importe)} ({cupon.Codigo?.Trim()}) para un pedido de al menos {Euros(cupon.PedidoMinimo)} de productos, hasta el {Fecha(cupon.Caducidad)}."
                : $"El cliente tiene el cupón {cupon.Codigo?.Trim()}, que ahora no se puede usar.";
        }

        /// <summary>El cupón ya está aplicado en este pedido.</summary>
        public static bool EstaEnElPedido(CuponDTO cupon, int? pedido)
        {
            return cupon != null && pedido.HasValue && pedido.Value != 0 && cupon.UsadoEnPedido == pedido.Value &&
                string.Equals(cupon.Estado?.Trim(), ESTADO_USADO, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// El cupón que se ofrece al meter un pedido (uno por pedido): el que ya lleva este pedido; si no, el primero que se puede
        /// usar (la API los da en ese orden); si no, uno que valdrá más adelante (Emitido), solo para avisar. Los usados en otros
        /// pedidos, anulados y caducados no se enseñan aquí (sí en la ficha del cliente).
        /// </summary>
        public static CuponDTO ElegirParaPedido(IEnumerable<CuponDTO> cupones, int? pedidoEnEdicion)
        {
            List<CuponDTO> lista = (cupones ?? Enumerable.Empty<CuponDTO>()).Where(c => c != null && !string.IsNullOrWhiteSpace(c.Codigo)).ToList();
            return lista.FirstOrDefault(c => EstaEnElPedido(c, pedidoEnEdicion))
                ?? lista.FirstOrDefault(c => c.SePuedeUsar)
                ?? lista.FirstOrDefault(c => string.Equals(c.Estado?.Trim(), ESTADO_EMITIDO, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Lo que falta, IVA incluido, para el pedido mínimo del cupón (0 si llega). Como la API: hay que llegar (100,00 € justos
        /// valen). Lo computable lo calcula quien llama y es aproximado; al guardar manda el servidor.
        /// </summary>
        public static decimal Falta(CuponDTO cupon, decimal totalComputableConIva)
        {
            if (cupon == null || totalComputableConIva >= cupon.PedidoMinimo)
            {
                return 0;
            }
            return cupon.PedidoMinimo - totalComputableConIva;
        }

        /// <summary>«Faltan 20,00 € de productos (IVA incluido) para el pedido mínimo del cupón, 100 € (lleva 80,00 €)…» o null.</summary>
        public static string TextoFalta(CuponDTO cupon, decimal totalComputableConIva)
        {
            decimal falta = Falta(cupon, totalComputableConIva);
            if (falta <= 0)
            {
                return null;
            }
            return $"Faltan {falta.ToString("N2", Espanol)} € de productos (IVA incluido, sin cursos ni portes) para el pedido mínimo del cupón, " +
                $"{Euros(cupon.PedidoMinimo)} (lleva {totalComputableConIva.ToString("N2", Espanol)} €). Es aproximado: al guardar manda lo que diga el servidor.";
        }

        /// <summary>Para la ficha: «Usado en el pedido 930001 (factura NV2612345)», «Caduca el 30/04/2027»…</summary>
        public static string Situacion(CuponDTO cupon)
        {
            if (cupon == null)
            {
                return null;
            }
            string estado = cupon.Estado?.Trim();
            if (string.Equals(estado, ESTADO_USADO, StringComparison.OrdinalIgnoreCase))
            {
                string pedido = cupon.UsadoEnPedido.HasValue ? $"en el pedido {cupon.UsadoEnPedido}" : "en un pedido";
                string factura = string.IsNullOrWhiteSpace(cupon.FacturaUso) ? string.Empty : $" (factura {cupon.FacturaUso.Trim()})";
                string fecha = cupon.FechaUso.HasValue ? $" el {Fecha(cupon.FechaUso.Value)}" : string.Empty;
                return $"Usado {pedido}{factura}{fecha}";
            }
            if (string.Equals(estado, ESTADO_EMITIDO, StringComparison.OrdinalIgnoreCase))
            {
                return $"Vale desde el {Fecha(cupon.ValidoDesde)}";
            }
            if (string.Equals(estado, ESTADO_ACTIVO, StringComparison.OrdinalIgnoreCase))
            {
                return $"Se puede usar hasta el {Fecha(cupon.Caducidad)}";
            }
            if (string.Equals(estado, ESTADO_CADUCADO, StringComparison.OrdinalIgnoreCase))
            {
                return $"Caducó el {Fecha(cupon.Caducidad)}";
            }
            return string.IsNullOrWhiteSpace(cupon.Mensaje) ? estado : cupon.Mensaje.Trim();
        }

        /// <summary>Solo se anula lo que no se ha usado: Emitido o Activo.</summary>
        public static bool SePuedeAnular(CuponDTO cupon)
        {
            string estado = cupon?.Estado?.Trim();
            return string.Equals(estado, ESTADO_ACTIVO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, ESTADO_EMITIDO, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Nesto#518: lo que enseñan la plantilla y el detalle del pedido sobre el cupón del cliente, listo para enlazar. Sin cupón
    /// (no tiene, la API es anterior a NestoAPI#613 o falla) no se ve nada.
    /// </summary>
    public class CuponVista : ObservableObject
    {
        private CuponDTO _cupon;

        public CuponDTO Cupon
        {
            get => _cupon;
            private set
            {
                if (SetProperty(ref _cupon, value))
                {
                    OnPropertyChanged(nameof(HayCupon));
                    OnPropertyChanged(nameof(SePuedeUsar));
                    OnPropertyChanged(nameof(NoSePuedeUsar));
                    OnPropertyChanged(nameof(Mensaje));
                    OnPropertyChanged(nameof(TextoUsar));
                    OnPropertyChanged(nameof(TextoAnadir));
                    OnPropertyChanged(nameof(Importe));
                    OnPropertyChanged(nameof(Codigo));
                }
            }
        }

        public bool HayCupon => Cupon != null;
        public bool SePuedeUsar => Cupon?.SePuedeUsar == true;
        /// <summary>Hay cupón pero todavía no vale (Emitido) o ya está en un pedido: solo se informa.</summary>
        public bool NoSePuedeUsar => Cupon != null && !Cupon.SePuedeUsar;
        public string Mensaje => ReglasCupon.Mensaje(Cupon);
        public string TextoUsar => ReglasCupon.TextoUsar(Cupon);
        public string TextoAnadir => ReglasCupon.TextoAnadir(Cupon);
        /// <summary>IVA incluido.</summary>
        public decimal Importe => Cupon?.Importe ?? 0;
        public string Codigo => Cupon?.Codigo?.Trim();

        public void Aplicar(CuponDTO cupon)
        {
            Cupon = cupon;
        }

        public void Limpiar()
        {
            Aplicar(null);
        }
    }
}
