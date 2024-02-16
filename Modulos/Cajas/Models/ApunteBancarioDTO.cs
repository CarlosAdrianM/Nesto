using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cajas.Models
{
    public class ApunteBancarioDTO
    {
        public int Id { get; set; }

        // Registro Principal de Movimientos
        public string CodigoRegistroPrincipal { get; set; }
        public string ClaveOficinaOrigen { get; set; }
        public DateTime FechaOperacion { get; set; }
        public DateTime FechaValor { get; set; }
        public string ConceptoComun { get; set; }
        public string TextoConceptoComun { get; set; }
        public string ConceptoPropio { get; set; }
        public string ClaveDebeOHaberMovimiento { get; set; }
        public decimal ImporteMovimiento { get; set; }
        public string NumeroDocumento { get; set; }
        public string Referencia1 { get; set; }
        public string Referencia2 { get; set; }

        public EstadoPunteo EstadoPunteo { get; set; }

        // Registros Complementarios de Concepto (Hasta un máximo de 5)
        public List<RegistroComplementarioConcepto> RegistrosConcepto { get; set; }

        // Registro Complementario de Información de Equivalencia de Importe (Opcional)
        public RegistroComplementarioEquivalencia ImporteEquivalencia { get; set; }

    }


    public class RegistroComplementarioConcepto
    {
        public string CodigoRegistroConcepto { get; set; }
        public string CodigoDato { get; set; }
        public string Concepto { get; set; }
        public string Concepto2 { get; set; }
        public string ConceptoCompleto { get => $"{Concepto}{Concepto2}"; }
    }

    public class RegistroComplementarioEquivalencia
    {
        public string CodigoRegistroEquivalencia { get; set; }
        public string CodigoDato { get; set; }
        public string ClaveDivisaOrigen { get; set; }
        public decimal ImporteEquivalencia { get; set; }
        public string CampoLibreEquivalencia { get; set; }
    }

    public enum EstadoPunteo
    {
        SinPuntear = 0,
        CompletamentePunteado,
        ParcialmentePunteado
    }

}