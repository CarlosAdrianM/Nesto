using Newtonsoft.Json;
using System;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Efecto incluido en una remesa de cobro (Nesto#340, Fase 1C.14 slice 3).
    /// Los JsonProperty mapean los nombres ASCII del DTO del API a los que bindea Remesas.xaml,
    /// que venían de la entidad EF ExtractoCliente (con acentos y "º").
    /// </summary>
    public class MovimientoRemesaModel
    {
        public int Id { get; set; }

        [JsonProperty("Cliente")]
        public string Número { get; set; }

        public string Contacto { get; set; }

        [JsonProperty("Documento")]
        public string Nº_Documento { get; set; }

        public string Efecto { get; set; }
        public string Concepto { get; set; }
        public decimal Importe { get; set; }

        [JsonProperty("Ccc")]
        public string CCC { get; set; }

        // Slice 5: el grid de detalle de impagados pinta también la fecha del efecto.
        public DateTime Fecha { get; set; }
    }
}
