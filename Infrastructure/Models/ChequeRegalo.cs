using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// NestoAPI#593 (c5): el cheque regalo de un cliente, tal y como lo contesta <c>GET api/ChequesRegalo/Cliente</c>.
    /// Si el cliente no tiene cheque en una campaña activa (o la campaña está apagada), la API da 404 y aquí no hay nada.
    /// Las reglas las pone la API; aquí solo se enseña y se calcula, de forma aproximada, cuánto falta para el mínimo.
    /// </summary>
    public class ChequeRegaloClienteDTO
    {
        public const string ESTADO_DISPONIBLE = "Disponible";
        public const string ESTADO_EN_PEDIDO = "EnPedido";
        public const string ESTADO_CANJEADO = "Canjeado";
        public const string ESTADO_PENDIENTE_DE_ACTIVAR = "PendienteDeActivar";
        public const string ESTADO_CADUCADO = "Caducado";
        public const string ESTADO_ANULADO = "Anulado";

        public string Campana { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        /// <summary>El producto de la línea del cheque (CHEQUE50_OCT26).</summary>
        public string Producto { get; set; }
        /// <summary>Base imponible que descuenta el cheque.</summary>
        public decimal Importe { get; set; }
        /// <summary>La base computable del pedido tiene que SUPERAR este importe.</summary>
        public decimal MinimoCanje { get; set; }
        public DateTime CanjeHasta { get; set; }
        public List<string> PrefijosNombreExcluidosMinimo { get; set; } = new List<string>();
        public List<string> GruposExcluidosMinimo { get; set; } = new List<string>();
        /// <summary>Disponible, EnPedido, Canjeado, PendienteDeActivar, Caducado o Anulado.</summary>
        public string Estado { get; set; }
        /// <summary>Solo true con Disponible.</summary>
        public bool SePuedeUsar { get; set; }
        /// <summary>La frase para el usuario, redactada por la API.</summary>
        public string Mensaje { get; set; }
        public string EmpresaFactura { get; set; }
        public string FacturaOrigen { get; set; }
        public DateTime? FechaFactura { get; set; }
        public DateTime? FechaActivacion { get; set; }
        public string EmpresaPedidoCanje { get; set; }
        public int? PedidoCanje { get; set; }
        public DateTime? FechaCanje { get; set; }
        /// <summary>«Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)»: el texto que pondrá la API en la línea.</summary>
        public string TextoLinea { get; set; }
    }

    /// <summary>
    /// NestoAPI#593 (c5): lo que comparten la plantilla y el detalle del pedido sobre el cheque regalo.
    /// </summary>
    public static class ReglasChequeRegalo
    {
        private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

        /// <summary>«50 €» o «12,50 €».</summary>
        public static string Euros(decimal importe)
        {
            return importe.ToString(importe == decimal.Truncate(importe) ? "N0" : "N2", Espanol) + " €";
        }

        /// <summary>¿Es esta línea la del cheque? (el producto del GET, sin mirar mayúsculas ni el relleno de espacios)</summary>
        public static bool EsLineaDelCheque(ChequeRegaloClienteDTO cheque, string producto)
        {
            return cheque != null && !string.IsNullOrWhiteSpace(cheque.Producto) && !string.IsNullOrWhiteSpace(producto)
                && string.Equals(cheque.Producto.Trim(), producto.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Lo mismo que mira la API para el mínimo: no cuentan el propio cheque, las cuentas contables ni los ficticios,
        /// ni los productos cuyo nombre empieza por un prefijo de la campaña («PACK 26») o de un grupo excluido («PEL»).
        /// </summary>
        public static bool CuentaParaElMinimo(ChequeRegaloClienteDTO cheque, string producto, string nombre, string grupo, bool esFicticio = false)
        {
            if (cheque == null || esFicticio || string.IsNullOrWhiteSpace(producto) || EsLineaDelCheque(cheque, producto))
            {
                return false;
            }
            string nombreLimpio = nombre?.Trim() ?? string.Empty;
            if ((cheque.PrefijosNombreExcluidosMinimo ?? new List<string>())
                .Any(p => !string.IsNullOrWhiteSpace(p) && nombreLimpio.StartsWith(p.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            string grupoLimpio = grupo?.Trim();
            return string.IsNullOrEmpty(grupoLimpio) || !(cheque.GruposExcluidosMinimo ?? new List<string>())
                .Any(g => string.Equals(g?.Trim(), grupoLimpio, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Lo que falta para superar el mínimo (como la API: mínimo − base + 0,01), o 0 si ya lo supera.</summary>
        public static decimal Falta(ChequeRegaloClienteDTO cheque, decimal baseComputable)
        {
            if (cheque == null || baseComputable > cheque.MinimoCanje)
            {
                return 0;
            }
            return cheque.MinimoCanje - baseComputable + 0.01M;
        }

        /// <summary>«Faltan 120,01 € de producto computable para superar los 250 € (lleva 129,99 €).» o null si ya lo supera.</summary>
        public static string TextoFalta(ChequeRegaloClienteDTO cheque, decimal baseComputable)
        {
            decimal falta = Falta(cheque, baseComputable);
            if (falta <= 0)
            {
                return null;
            }
            return $"Faltan {falta.ToString("N2", Espanol)} € de producto computable para superar los {Euros(cheque.MinimoCanje)} " +
                $"(lleva {baseComputable.ToString("N2", Espanol)} €). Es aproximado: al guardar manda lo que diga el servidor.";
        }

        /// <summary>«Usar el cheque regalo de 50 €»</summary>
        public static string TextoUsar(ChequeRegaloClienteDTO cheque)
        {
            return cheque == null ? null : $"Usar el cheque regalo de {Euros(cheque.Importe)}";
        }

        /// <summary>«Añadir el cheque regalo de 50 €»</summary>
        public static string TextoAnadir(ChequeRegaloClienteDTO cheque)
        {
            return cheque == null ? null : $"Añadir el cheque regalo de {Euros(cheque.Importe)}";
        }

        /// <summary>El texto de la línea (si la API no lo manda, uno parecido; la API pone el suyo al guardar).</summary>
        public static string TextoLinea(ChequeRegaloClienteDTO cheque)
        {
            if (cheque == null)
            {
                return null;
            }
            return string.IsNullOrWhiteSpace(cheque.TextoLinea) ? $"Cheque regalo {Euros(cheque.Importe)}" : cheque.TextoLinea.Trim();
        }

        /// <summary>El mensaje de la API o, si no viene, uno genérico.</summary>
        public static string Mensaje(ChequeRegaloClienteDTO cheque)
        {
            if (cheque == null)
            {
                return null;
            }
            if (!string.IsNullOrWhiteSpace(cheque.Mensaje))
            {
                return cheque.Mensaje.Trim();
            }
            return cheque.SePuedeUsar
                ? $"El cliente tiene un cheque regalo de {Euros(cheque.Importe)} + IVA para un pedido de más de {Euros(cheque.MinimoCanje)}."
                : "El cliente tiene un cheque regalo que ahora no se puede usar.";
        }
    }

    /// <summary>
    /// NestoAPI#593 (c5): lo que enseñan la plantilla y el detalle del pedido sobre el cheque regalo del cliente, listo
    /// para enlazar. Sin cheque (404, error o campaña apagada) no se ve nada.
    /// </summary>
    public class ChequeRegaloVista : ObservableObject
    {
        private ChequeRegaloClienteDTO _cheque;

        public ChequeRegaloClienteDTO Cheque
        {
            get => _cheque;
            private set
            {
                if (SetProperty(ref _cheque, value))
                {
                    OnPropertyChanged(nameof(HayCheque));
                    OnPropertyChanged(nameof(SePuedeUsar));
                    OnPropertyChanged(nameof(NoSePuedeUsar));
                    OnPropertyChanged(nameof(Mensaje));
                    OnPropertyChanged(nameof(TextoUsar));
                    OnPropertyChanged(nameof(TextoAnadir));
                    OnPropertyChanged(nameof(Importe));
                }
            }
        }

        public bool HayCheque => Cheque != null;
        public bool SePuedeUsar => Cheque?.SePuedeUsar == true;
        /// <summary>Hay cheque pero ya está en un pedido, canjeado, caducado...: solo se informa, sin casilla.</summary>
        public bool NoSePuedeUsar => Cheque != null && !Cheque.SePuedeUsar;
        public string Mensaje => ReglasChequeRegalo.Mensaje(Cheque);
        public string TextoUsar => ReglasChequeRegalo.TextoUsar(Cheque);
        public string TextoAnadir => ReglasChequeRegalo.TextoAnadir(Cheque);
        public decimal Importe => Cheque?.Importe ?? 0;

        public void Aplicar(ChequeRegaloClienteDTO cheque)
        {
            Cheque = cheque;
        }

        public void Limpiar()
        {
            Aplicar(null);
        }
    }
}
