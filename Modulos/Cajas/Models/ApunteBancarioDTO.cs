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
        public string ConceptoPropio { get; set; }
        public string ClaveDebeOHaberMovimiento { get; set; }
        public decimal ImporteMovimiento { get; set; }
        public string NumeroDocumento { get; set; }
        public string Referencia1 { get; set; }
        public string Referencia2 { get; set; }

        // Registros Complementarios de Concepto (Hasta un máximo de 5)
        public List<RegistroComplementarioConcepto> RegistrosConcepto { get; set; }

        // Registro Complementario de Información de Equivalencia de Importe (Opcional)
        public RegistroComplementarioEquivalencia ImporteEquivalencia { get; set; }

    }


    public class RegistroComplementarioConcepto
    {
        public string CodigoRegistroConcepto { get; set; }
        public string CodigoDatoConcepto { get; set; }
        public string Concepto { get; set; }
    }

    public class RegistroComplementarioEquivalencia
    {
        public string CodigoRegistroEquivalencia { get; set; }
        public string CodigoDatoEquivalencia { get; set; }
        public string ClaveDivisaOrigen { get; set; }
        public decimal ImporteEquivalencia { get; set; }
        public string CampoLibreEquivalencia { get; set; }
    }
    /*
    public class RegistroCabeceraCuenta
    {
        // Registro de Cabecera de Cuenta
        public string CodigoRegistroCabecera { get; set; }
        public string ClaveEntidad { get; set; }
        public string ClaveOficina { get; set; }
        public string NumeroCuenta { get; set; }
        public DateTime FechaInicial { get; set; }
        public DateTime FechaFinal { get; set; }
        public string ClaveDebeOHaber { get; set; }
        public decimal ImporteSaldoInicial { get; set; }
        public string ClaveDivisa { get; set; }
        public string ModalidadInformacion { get; set; }
        public string NombreAbreviado { get; set; }
        public string CampoLibreCabecera { get; set; }
    }

    public class RegistroFinalCuenta
    {
        // Registro Final de Cuenta
        public string CodigoRegistroFinal { get; set; }
        public string ClaveEntidadFinal { get; set; }
        public string ClaveOficinaFinal { get; set; }
        public string NumeroCuentaFinal { get; set; }
        public int NumeroApuntesDebe { get; set; }
        public decimal TotalImportesDebe { get; set; }
        public int NumeroApuntesHaber { get; set; }
        public decimal TotalImportesHaber { get; set; }
        public string CodigoSaldoFinal { get; set; }
        public decimal SaldoFinal { get; set; }
        public string ClaveDivisaFinal { get; set; }
        public string CampoLibreFinal { get; set; }
    }

    public class RegistroFinalFichero
    {
        // Registro de Fin de Fichero
        public string CodigoRegistroFinFichero { get; set; }
        public string Nueves { get; set; }
        public int NumeroRegistros { get; set; }
        public string CampoLibreFinFichero { get; set; }
    }
    */
}