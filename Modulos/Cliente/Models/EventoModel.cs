using System;

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

        /// <summary>«Masterclass Cloasma 13/10/2026» (combos y diálogo de elegir evento).</summary>
        public string Descripcion => $"{Titulo} {Fecha:dd/MM/yyyy}";
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
        public decimal Importe { get; set; }
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
