using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>NestoAPI#591: un evento (curso, masterclass…) con señal reembolsable (EventoDTO de api/Eventos).</summary>
    public class EventoModel
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Titulo { get; set; }
        public DateTime Fecha { get; set; }
        public decimal ImporteSenal { get; set; }
        public bool Activo { get; set; } = true;
        public string Usuario { get; set; }
        public DateTime? FechaModificacion { get; set; }

        // Nesto#517 (NestoAPI#611): los campos nuevos del evento.

        /// <summary>
        /// La tienda del evento: REI (Reina) o ALC (Alcobendas); la mantiene esa tienda. Sin tienda (los de #591) no se manda,
        /// porque la API exige REI o ALC si el campo viene en el cuerpo, aunque sea vacío.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Almacen { get; set; }
        /// <summary>La referencia de producto del curso (una por curso), con la que se factura.</summary>
        public string ProductoCurso { get; set; }
        /// <summary>Plazas del evento; null = sin límite.</summary>
        public int? Plazas { get; set; }
        /// <summary>Solo lectura: inscripciones que ocupan plaza (todas menos caducadas y anuladas).</summary>
        public int PlazasOcupadas { get; set; }
        public string Lugar { get; set; }
        /// <summary>
        /// El texto largo del evento. En la API se llama así (y no «Descripcion») porque <see cref="Descripcion"/> es la
        /// calculada de este modelo, que también se manda.
        /// </summary>
        public string DescripcionLarga { get; set; }
        /// <summary>Solo lectura: cuándo se pasó lista (null = todavía no).</summary>
        public DateTime? FechaListaAsistencia { get; set; }

        // NestoAPI#690 (corte 1b, Carlos 22/10/26): contrato del comentario del 22/10/26 en la issue. La API de antes no los
        // manda y los ignora al recibirlos.

        /// <summary>NestoAPI#690: «HH:mm» («10:00») o null.</summary>
        public string HoraInicio { get; set; }
        /// <summary>NestoAPI#690: «HH:mm», posterior a <see cref="HoraInicio"/>, o null.</summary>
        public string HoraFin { get; set; }
        /// <summary>NestoAPI#690: ponente (texto libre, hasta 100).</summary>
        public string Ponente { get; set; }
        /// <summary>NestoAPI#690: solo lectura. URL pública de la imagen (se sube con POST api/Eventos/{id}/Imagen). No se manda.</summary>
        public string ImagenUrl { get; set; }
        public bool ShouldSerializeImagenUrl() => false;
        /// <summary>
        /// NestoAPI#690: la señal NO reembolsable (factura el curso exento, sin cupón). <see cref="ImporteSenal"/> es la
        /// reembolsable (con cupón). Las dos a 0: evento gratuito. Null con la API de antes de #690: entonces no se manda (al
        /// crear, la API pondría 60; en un PUT, lo que no viene se conserva).
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public decimal? ImporteSenalNoReembolsable { get; set; }
        public const string FORMATO_PRESENCIAL = "Presencial";
        public const string FORMATO_ONLINE = "Online";

        /// <summary>
        /// Presencial u Online (webinar). Null cuenta como Presencial. Aunque sea online, el evento es de una tienda (el
        /// ponente emite desde allí y la factura lleva su almacén y su delegación).
        /// </summary>
        public string Formato { get; set; }

        [JsonIgnore]
        public bool EsOnline => string.Equals(Formato?.Trim(), FORMATO_ONLINE, StringComparison.OrdinalIgnoreCase);

        /// <summary>NestoAPI#690: solo lectura. Las dos señales a 0. No se manda.</summary>
        public bool Gratuito { get; set; }
        public bool ShouldSerializeGratuito() => false;
        /// <summary>NestoAPI#690: solo lectura. Lo que se puede elegir al inscribir (Reembolsable, NoReembolsable, Gratuita). No se manda.</summary>
        public List<string> Modalidades { get; set; }
        public bool ShouldSerializeModalidades() => false;

        /// <summary>El de la API o, con la de antes, el de las dos señales a 0.</summary>
        [JsonIgnore]
        public bool EsGratuito => Gratuito || (ImporteSenal == 0 && ImporteSenalNoReembolsable == 0);

        /// <summary>«10:00 – 13:00», «10:00» o vacío.</summary>
        [JsonIgnore]
        public string TextoHorario => string.IsNullOrWhiteSpace(HoraInicio)
            ? string.Empty
            : string.IsNullOrWhiteSpace(HoraFin) ? HoraInicio.Trim() : $"{HoraInicio.Trim()} – {HoraFin.Trim()}";

        /// <summary>«100,00 € / 60,00 €», «Gratuito» o solo la reembolsable con la API de antes de #690 o sin no reembolsable.</summary>
        [JsonIgnore]
        public string TextoSenales => EsGratuito
            ? "Gratuito"
            : ImporteSenalNoReembolsable > 0 ? $"{ImporteSenal:C} / {ImporteSenalNoReembolsable.Value:C}" : $"{ImporteSenal:C}";

        /// <summary>«Masterclass Cloasma 13/10/2026» (combos y diálogo de elegir evento).</summary>
        public string Descripcion => $"{Titulo} {Fecha:dd/MM/yyyy}";

        /// <summary>«Reina», «Alcobendas» o vacío (columna de la lista).</summary>
        [JsonIgnore]
        public string NombreTienda => TiendaEvento.Nombre(Almacen);

        /// <summary>«3 / 12», «3» si no hay límite (columna de la lista).</summary>
        [JsonIgnore]
        public string TextoPlazas => Plazas.HasValue ? $"{PlazasOcupadas} / {Plazas.Value}" : PlazasOcupadas.ToString();
    }

    /// <summary>Nesto#517: una tienda donde se hacen eventos (combo del mantenimiento). Código null = sin tienda.</summary>
    public class TiendaEvento
    {
        public const string REINA = "REI";
        public const string ALCOBENDAS = "ALC";

        public TiendaEvento(string codigo, string texto)
        {
            Codigo = codigo;
            Texto = texto;
        }

        public string Codigo { get; }
        public string Texto { get; }

        public override string ToString() => Texto;

        public static bool EsTiendaDeEventos(string almacen) =>
            string.Equals(almacen?.Trim(), REINA, StringComparison.OrdinalIgnoreCase)
            || string.Equals(almacen?.Trim(), ALCOBENDAS, StringComparison.OrdinalIgnoreCase);

        public static string Nombre(string almacen)
        {
            switch (almacen?.Trim()?.ToUpperInvariant())
            {
                case REINA: return "Reina";
                case ALCOBENDAS: return "Alcobendas";
                default: return almacen?.Trim() ?? string.Empty;
            }
        }
    }

    /// <summary>NestoAPI#591: una señal con su evento, cliente, pendiente y estado (SenalEventoDTO de api/Eventos/Senales).</summary>
    public class SenalEventoModel
    {
        public const string PENDIENTE = "Pendiente";
        public const string LIBERADA = "Liberada";
        public const string SIN_COMPRA = "SinCompra";
        public const string CONSUMIDA = "Consumida";

        public int Id { get; set; }
        public int EventoId { get; set; }
        public string Evento { get; set; }
        public DateTime FechaEvento { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public int NumOrdenExtracto { get; set; }
        public DateTime? FechaApunte { get; set; }
        public string Documento { get; set; }
        public string Concepto { get; set; }
        /// <summary>Importe original del apunte a favor (positivo).</summary>
        public decimal Importe { get; set; }
        /// <summary>Lo que le queda a favor hoy (positivo).</summary>
        public decimal ImportePendiente { get; set; }
        /// <summary>Pendiente, Liberada, SinCompra o Consumida.</summary>
        public string Estado { get; set; }
        /// <summary>Pendiente, Liberada, Sin compra o Consumida (lo que se enseña).</summary>
        public string EstadoTexto { get; set; }
        public DateTime FechaSinCompra { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaMarcado { get; set; }

        /// <summary>«Señal: Masterclass Cloasma 13/10» (columna del extracto).</summary>
        public string TextoExtracto => $"Señal: {Evento} {FechaEvento:dd/MM}";
    }

    /// <summary>NestoAPI#591: cuerpo de POST api/Eventos/{id}/Senales.</summary>
    public class MarcarSenalEventoModel
    {
        public string Empresa { get; set; }
        public int NumOrdenExtracto { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
    }
}
