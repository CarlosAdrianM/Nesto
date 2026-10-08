using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// NestoAPI#606: qué día entregamos el pedido a la agencia, tal y como lo contestan
    /// <c>POST api/PedidosVenta/FechaEntregaAgencia</c> (plantilla) y <c>GET api/PedidosVenta/{empresa}/{numero}/FechaEntregaAgencia</c>
    /// (detalle). Lo calcula la API; aquí solo se enseña. Las fechas son días, sin hora.
    /// </summary>
    public class FechaEntregaAgenciaDTO
    {
        public const string APLICA_PRIMERA = "Primera";
        public const string APLICA_COMPLETA = "Completa";

        /// <summary>Día de la primera entrega a la agencia. Null = sin fecha.</summary>
        public DateTime? PrimeraEntrega { get; set; }
        /// <summary>Día de la entrega con la que queda todo servido. Null = algo no tiene fecha.</summary>
        public DateTime? EntregaCompleta { get; set; }
        /// <summary>La que se enseña: la completa en «Todo junto», la primera en el resto. Null = sin fecha.</summary>
        public DateTime? FechaEntregaAgencia { get; set; }
        /// <summary>"Primera" o "Completa": cuál de las dos es <see cref="FechaEntregaAgencia"/>.</summary>
        public string Aplica { get; set; }
        /// <summary>Todos los días en que sale algo, en orden.</summary>
        public List<DateTime> Entregas { get; set; } = new List<DateTime>();
        /// <summary>Por qué, redactado por la API (va en el tooltip).</summary>
        public string Motivo { get; set; }
        /// <summary>La que se dio al crear el pedido. Null en la plantilla y en los pedidos que no la tienen.</summary>
        public DateTime? FechaPrometida { get; set; }
        /// <summary>
        /// Algo que conviene saber, redactado por la API (p. ej. «Con «Todo junto» el pedido no sale hasta que esté
        /// todo; …»). Null si no hay nada que avisar o la API es anterior a la propiedad.
        /// </summary>
        public string Aviso { get; set; }
    }

    /// <summary>
    /// NestoAPI#606: los textos que ve el usuario. Mismo criterio en la plantilla y en el detalle del pedido:
    /// «Se entrega a la agencia el jueves 15/10» (o «hoy», «mañana»), «Sin fecha todavía» y «Prometida: …».
    /// </summary>
    public static class TextosFechaEntregaAgencia
    {
        public const string SIN_FECHA = "Sin fecha todavía";
        private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

        /// <summary>Null si no hay respuesta (API caída o anterior al endpoint): entonces no se enseña nada.</summary>
        public static string Texto(FechaEntregaAgenciaDTO fecha, DateTime hoy)
        {
            if (fecha == null)
            {
                return null;
            }
            if (!fecha.FechaEntregaAgencia.HasValue)
            {
                return SIN_FECHA;
            }
            string texto = "Se entrega a la agencia " + Dia(fecha.FechaEntregaAgencia.Value, hoy, conArticulo: true);
            if (string.Equals(fecha.Aplica, FechaEntregaAgenciaDTO.APLICA_PRIMERA, StringComparison.OrdinalIgnoreCase)
                && fecha.EntregaCompleta.HasValue
                && fecha.EntregaCompleta.Value.Date != fecha.FechaEntregaAgencia.Value.Date)
            {
                texto += " · completo " + Dia(fecha.EntregaCompleta.Value, hoy, conArticulo: true);
            }
            return texto;
        }

        /// <summary>«Prometida: …» solo si hay prometida y no coincide con la que sale ahora.</summary>
        public static string TextoPrometida(FechaEntregaAgenciaDTO fecha, DateTime hoy)
        {
            if (fecha?.FechaPrometida == null)
            {
                return null;
            }
            DateTime prometida = fecha.FechaPrometida.Value.Date;
            if (fecha.FechaEntregaAgencia.HasValue && fecha.FechaEntregaAgencia.Value.Date == prometida)
            {
                return null;
            }
            return "Prometida: " + Dia(prometida, hoy, conArticulo: false);
        }

        /// <summary>«hoy», «mañana» o «(el) jueves 15/10» (con el año si no es el de hoy).</summary>
        internal static string Dia(DateTime fecha, DateTime hoy, bool conArticulo)
        {
            DateTime dia = fecha.Date;
            DateTime hoyDia = hoy.Date;
            if (dia == hoyDia)
            {
                return "hoy";
            }
            if (dia == hoyDia.AddDays(1))
            {
                return "mañana";
            }
            string nombreDia = Espanol.DateTimeFormat.GetDayName(dia.DayOfWeek);
            string formato = dia.Year == hoyDia.Year ? "dd/MM" : "dd/MM/yyyy";
            return (conArticulo ? "el " : string.Empty) + nombreDia + " " + dia.ToString(formato, Espanol);
        }
    }

    /// <summary>
    /// NestoAPI#606: lo que enseñan la plantilla y el detalle del pedido, ya redactado y listo para enlazar.
    /// </summary>
    public class FechaEntregaAgenciaVista : ObservableObject
    {
        private string _texto;
        private string _motivo;
        private string _textoPrometida;
        private string _aviso;

        public string Texto
        {
            get => _texto;
            private set
            {
                if (SetProperty(ref _texto, value))
                {
                    OnPropertyChanged(nameof(HayTexto));
                }
            }
        }

        public bool HayTexto => !string.IsNullOrWhiteSpace(Texto);

        /// <summary>El porqué de la API, para el tooltip.</summary>
        public string Motivo
        {
            get => _motivo;
            private set => SetProperty(ref _motivo, value);
        }

        public string TextoPrometida
        {
            get => _textoPrometida;
            private set
            {
                if (SetProperty(ref _textoPrometida, value))
                {
                    OnPropertyChanged(nameof(HayPrometida));
                }
            }
        }

        public bool HayPrometida => !string.IsNullOrWhiteSpace(TextoPrometida);

        /// <summary>El aviso de la API (no es un error: algo a tener en cuenta). Null si no viene.</summary>
        public string Aviso
        {
            get => _aviso;
            private set
            {
                if (SetProperty(ref _aviso, value))
                {
                    OnPropertyChanged(nameof(HayAviso));
                }
            }
        }

        public bool HayAviso => !string.IsNullOrWhiteSpace(Aviso);

        /// <summary>Null (sin respuesta) lo deja todo vacío: no se enseña nada.</summary>
        public void Aplicar(FechaEntregaAgenciaDTO fecha, DateTime hoy)
        {
            Texto = TextosFechaEntregaAgencia.Texto(fecha, hoy);
            Motivo = string.IsNullOrWhiteSpace(fecha?.Motivo) ? null : fecha.Motivo;
            TextoPrometida = TextosFechaEntregaAgencia.TextoPrometida(fecha, hoy);
            Aviso = string.IsNullOrWhiteSpace(fecha?.Aviso) ? null : fecha.Aviso.Trim();
        }

        public void Limpiar() => Aplicar(null, DateTime.Today);
    }
}
