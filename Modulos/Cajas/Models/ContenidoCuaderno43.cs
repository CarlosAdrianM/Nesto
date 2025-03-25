using System;
using System.Collections.Generic;
using System.Text;

namespace Nesto.Modulos.Cajas.Models
{
    public class ContenidoCuaderno43
    {
        public ContenidoCuaderno43()
        {
            Apuntes = [];
        }
        public RegistroCabeceraCuenta Cabecera { get; set; }
        public List<ApunteBancarioDTO> Apuntes { get; set; }
        public RegistroFinalCuenta FinalCuenta { get; set; }
        public RegistroFinalFichero FinalFichero { get; set; }
        public string Usuario { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new();

            // Cabecera de cuenta - Registro 11
            _ = sb.AppendLine(
                $"{ExactLength(Cabecera.CodigoRegistroCabecera, 2)}" +
                $"{ExactLength(Cabecera.ClaveEntidad, 4)}" +
                $"{ExactLength(Cabecera.ClaveOficina, 4)}" +
                $"{ExactLength(Cabecera.NumeroCuenta, 10)}" +
                $"{Cabecera.FechaInicial:yyMMdd}" +
                $"{Cabecera.FechaFinal:yyMMdd}" +
                $"{ExactLength(Cabecera.ClaveDebeOHaber, 1)}" +
                $"{FormatDecimal(Cabecera.ImporteSaldoInicial, 14)}" +
                $"{ExactLength(Cabecera.ClaveDivisa, 3)}" +
                $"{ExactLength(Cabecera.ModalidadInformacion, 1)}" +
                $"{ExactLength(Cabecera.NombreAbreviado, 26)}" +
                $"{ExactLength(Cabecera.CampoLibreCabecera, 3)}");

            // Líneas de apuntes - Registro 22
            foreach (ApunteBancarioDTO apunte in Apuntes)
            {
                _ = sb.AppendLine(
                    $"{ExactLength(apunte.CodigoRegistroPrincipal, 2)}" +
                    $"{ExactLength("", 4)}" + // Hay 4 caracteres vacíos según el parser (entre posiciones 2-6)
                    $"{ExactLength(apunte.ClaveOficinaOrigen, 4)}" +
                    $"{apunte.FechaOperacion:yyMMdd}" +
                    $"{apunte.FechaValor:yyMMdd}" +
                    $"{ExactLength(apunte.ConceptoComun, 2)}" + // En el parser parece ser 2 caracteres, no 4
                    $"{ExactLength(apunte.ConceptoPropio, 3)}" + // Posiciones corregidas según parser (24-27)
                    $"{ExactLength(apunte.ClaveDebeOHaberMovimiento, 1)}" +
                    $"{FormatDecimal(apunte.ImporteMovimiento, 14)}" +
                    $"{ExactLength(apunte.NumeroDocumento, 10)}" +
                    $"{ExactLength(apunte.Referencia1, 12)}" +
                    $"{ExactLength(apunte.Referencia2, 16)}");

                // Si hay registros complementarios de concepto (23), deberían agregarse aquí
                foreach (RegistroComplementarioConcepto concepto in apunte.RegistrosConcepto)
                {
                    _ = sb.AppendLine(
                        $"{ExactLength(concepto.CodigoRegistroConcepto, 2)}" +
                        $"{ExactLength(concepto.CodigoDato, 2)}" +
                        $"{ExactLength(concepto.Concepto, 38)}" +
                        $"{ExactLength(concepto.Concepto2 ?? "", 38)}");
                }

                // Si hay registro de equivalencia de divisas (24)
                if (apunte.ImporteEquivalencia != null && !string.IsNullOrEmpty(apunte.ImporteEquivalencia.CodigoRegistroEquivalencia))
                {
                    _ = sb.AppendLine(
                        $"{ExactLength(apunte.ImporteEquivalencia.CodigoRegistroEquivalencia, 2)}" +
                        $"{ExactLength(apunte.ImporteEquivalencia.CodigoDato, 2)}" +
                        $"{ExactLength(apunte.ImporteEquivalencia.ClaveDivisaOrigen, 3)}" +
                        $"{FormatDecimal(apunte.ImporteEquivalencia.ImporteEquivalencia, 14)}" +
                        $"{ExactLength(apunte.ImporteEquivalencia.CampoLibreEquivalencia, 59)}");
                }
            }

            // Registro final de cuenta - Registro 33
            _ = sb.AppendLine(
                $"{ExactLength(FinalCuenta.CodigoRegistroFinal, 2)}" +
                $"{ExactLength(FinalCuenta.ClaveEntidadFinal, 4)}" +
                $"{ExactLength(FinalCuenta.ClaveOficinaFinal, 4)}" +
                $"{ExactLength(FinalCuenta.NumeroCuentaFinal, 10)}" +
                $"{FinalCuenta.NumeroApuntesDebe:00000}" + // 5 dígitos según el parser (posiciones 20-25)
                $"{FormatDecimal(FinalCuenta.TotalImportesDebe, 14)}" +
                $"{FinalCuenta.NumeroApuntesHaber:00000}" + // 5 dígitos según el parser (posiciones 39-44)
                $"{FormatDecimal(FinalCuenta.TotalImportesHaber, 14)}" +
                $"{ExactLength(FinalCuenta.CodigoSaldoFinal, 1)}" +
                $"{FormatDecimal(FinalCuenta.SaldoFinal, 14)}" +
                $"{ExactLength(FinalCuenta.ClaveDivisaFinal, 3)}" +
                $"{ExactLength(FinalCuenta.CampoLibreFinal, 4)}"); // 4 caracteres según el parser

            // Registro final de fichero - Registro 88
            _ = sb.AppendLine(
                $"{ExactLength(FinalFichero.CodigoRegistroFinFichero, 2)}" +
                $"{ExactLength(FinalFichero.Nueves, 18)}" + // 18 caracteres según el parser
                $"{FinalFichero.NumeroRegistros:000000}" +
                $"{ExactLength(FinalFichero.CampoLibreFinFichero, 54)}"); // 54 caracteres según el parser

            return sb.ToString();
        }

        // Método de utilidad para asegurar longitud exacta
        private string ExactLength(object value, int length)
        {
            string stringValue = value?.ToString() ?? string.Empty;
            return stringValue.Length > length ? stringValue[..length] : stringValue.PadRight(length);
        }

        // Método para formatear valores decimales con precisión de 2 decimales y sin punto decimal
        private string FormatDecimal(decimal value, int length)
        {
            // Multiplicar por 100 para eliminar los decimales
            long valueWithoutDecimal = (long)(value * 100);
            // Formatear con ceros a la izquierda
            return valueWithoutDecimal.ToString().PadLeft(length, '0');
        }
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