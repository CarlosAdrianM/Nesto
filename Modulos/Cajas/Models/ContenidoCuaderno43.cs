using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cajas.Models
{
    public class ContenidoCuaderno43
    {
        public ContenidoCuaderno43()
        {
            Apuntes = new List<ApunteBancarioDTO>();
        }
        public RegistroCabeceraCuenta Cabecera { get; set; }
        public List<ApunteBancarioDTO> Apuntes { get; set; }
        public RegistroFinalCuenta FinalCuenta { get; set; }
        public RegistroFinalFichero FinalFichero { get; set; }
        public string Usuario { get; set; }
    }
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


}