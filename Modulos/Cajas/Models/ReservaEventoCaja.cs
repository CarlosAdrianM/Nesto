using System;

namespace Nesto.Modulos.Cajas.Models
{
    /// <summary>
    /// Carlos (19/11/26, caso del cliente 33895): una reserva de evento sin pagar del cliente, que se cobra en Cajas como cualquier
    /// otra deuda (GET api/Eventos/Inscripciones/SinPagar). Al cobrarla, la API apunta el cobro en la caja, pasa la inscripción a
    /// pagada, factura el curso y emite el cupón, como cuando llega el enlace pagado.
    /// </summary>
    public class ReservaEventoCaja
    {
        public int InscripcionId { get; set; }
        public int EventoId { get; set; }
        public string Evento { get; set; }
        public DateTime FechaEvento { get; set; }
        public string Almacen { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Estado { get; set; }
        public string EstadoTexto { get; set; }
        public string Modalidad { get; set; }
        /// <summary>La señal: se cobra entera o no se cobra.</summary>
        public decimal Importe { get; set; }
        public DateTime? FechaLimitePago { get; set; }
        /// <summary>«Señal Masterclass Rosácea (24/11)».</summary>
        public string Descripcion { get; set; }
        public string Documento { get; set; }
        public bool Cobrable { get; set; }
        /// <summary>Por qué no se puede cobrar aquí (de usted).</summary>
        public string Motivo { get; set; }
        public bool CobroYaApuntado { get; set; }

        /// <summary>Lo que sale en la columna «Situación» de la lista.</summary>
        public string Situacion => Cobrable
            ? (CobroYaApuntado ? "Cobrada a medias: al cobrarla se termina sin cobrar otra vez" : "Se puede cobrar")
            : Motivo;
    }

    /// <summary>POST api/Eventos/Inscripciones/{id}/CobroCaja.</summary>
    public class CobroReservaEventoCaja
    {
        public string FormaPago { get; set; }
        public decimal Importe { get; set; }
        public string CuentaCobro { get; set; }
        public DateTime? Fecha { get; set; }
        public string Diario { get; set; }
        public string Delegacion { get; set; }
        public string FormaVenta { get; set; }
        public string ConceptoAdicional { get; set; }
    }

    /// <summary>Lo que contesta la API al cobrar la señal.</summary>
    public class ResultadoCobroReservaEventoCaja
    {
        public int Asiento { get; set; }
        public bool CobroYaEstaba { get; set; }
        public string Documento { get; set; }
        public decimal Importe { get; set; }
        public int? Pedido { get; set; }
        public string Factura { get; set; }
        public string EstadoFacturacion { get; set; }
        public string Cupon { get; set; }
        public decimal? ImporteCupon { get; set; }
        /// <summary>Lo que se ha hecho (factura, cupón…), de usted, para enseñarlo tal cual.</summary>
        public string Mensaje { get; set; }
    }
}
