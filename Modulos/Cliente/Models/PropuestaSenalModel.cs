using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>
    /// Nesto#517 (NestoAPI#615): un cobro a cuenta que la API propone como señal de uno o varios eventos
    /// (GET api/Eventos/Senales/Propuestas). La propuesta no se confirma sola: la confirma o descarta Administración.
    /// </summary>
    public class PropuestaSenalModel
    {
        public string Empresa { get; set; }
        public int NumOrdenExtracto { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public DateTime? FechaApunte { get; set; }
        public string Documento { get; set; }
        public string Concepto { get; set; }
        /// <summary>El concepto de la transferencia en el banco (al puntear).</summary>
        public string ConceptoBanco { get; set; }
        public decimal Importe { get; set; }
        /// <summary>Primero los que tienen inscripción, luego los de concepto y, a igualdad, por fecha del evento.</summary>
        public List<CandidatoSenalModel> Candidatos { get; set; } = new();
    }

    /// <summary>NestoAPI#615: un evento candidato de una propuesta, con su motivo.</summary>
    public class CandidatoSenalModel
    {
        /// <summary>El id de la propuesta (lo que se confirma o se descarta).</summary>
        public int Id { get; set; }
        public int EventoId { get; set; }
        public string Evento { get; set; }
        public DateTime FechaEvento { get; set; }
        public decimal ImporteSenal { get; set; }
        public bool PorInscripcion { get; set; }
        public bool PorConcepto { get; set; }
        public int? InscripcionId { get; set; }
        public string Motivo { get; set; }
        /// <summary>Cajas, Conciliar o el barrido.</summary>
        public string Origen { get; set; }
        public DateTime? FechaPropuesta { get; set; }
    }

    /// <summary>Nesto#517: una fila de la lista de propuestas (un cobro con uno de sus candidatos).</summary>
    public class FilaPropuestaSenal
    {
        public FilaPropuestaSenal(PropuestaSenalModel cobro, CandidatoSenalModel candidato, bool primeraDelCobro)
        {
            Cobro = cobro;
            Candidato = candidato;
            PrimeraDelCobro = primeraDelCobro;
        }

        public PropuestaSenalModel Cobro { get; }
        public CandidatoSenalModel Candidato { get; }
        /// <summary>Para pintar el cobro solo en su primera fila cuando tiene varios candidatos.</summary>
        public bool PrimeraDelCobro { get; }

        public int IdPropuesta => Candidato.Id;
        public string Cliente => $"{Cobro.Cliente?.Trim()}/{Cobro.Contacto?.Trim()}";
        public string Nombre => Cobro.Nombre?.Trim();
        public DateTime? FechaApunte => Cobro.FechaApunte;
        public decimal Importe => Cobro.Importe;
        public string ConceptoBanco => string.IsNullOrWhiteSpace(Cobro.ConceptoBanco) ? Cobro.Concepto?.Trim() : Cobro.ConceptoBanco.Trim();
        public string Evento => $"{Candidato.Evento?.Trim()} {Candidato.FechaEvento:dd/MM/yyyy}";
        public decimal ImporteSenal => Candidato.ImporteSenal;
        public string Motivo => Candidato.Motivo;

        public static List<FilaPropuestaSenal> Aplanar(IEnumerable<PropuestaSenalModel> propuestas)
        {
            var filas = new List<FilaPropuestaSenal>();
            foreach (PropuestaSenalModel cobro in propuestas ?? Array.Empty<PropuestaSenalModel>())
            {
                bool primera = true;
                foreach (CandidatoSenalModel candidato in cobro.Candidatos ?? new List<CandidatoSenalModel>())
                {
                    filas.Add(new FilaPropuestaSenal(cobro, candidato, primera));
                    primera = false;
                }
            }
            return filas;
        }
    }
}
