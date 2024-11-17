using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaAmazonPayComision : IReglaContabilizacion
    {
        public string Nombre => "Amazon Pay";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            var importeDescuadre = apuntesBancarios.Sum(b => b.ImporteMovimiento) - apuntesContabilidad.Sum(c => c.Importe);

            var importeIngresado = apuntesBancarios.Sum(b => b.ImporteMovimiento);
            var comisionDescontada = -apuntesContabilidad.Where(c => c.Documento?.Trim() == "COMIS_AMZ").Sum(c => c.Importe);
            var importeComision = apuntesContabilidad.Sum(c => c.Importe) - importeIngresado;
            var importeOriginal = importeIngresado + importeComision + comisionDescontada;

            if (importeDescuadre == 0M
                || !VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0), comisionDescontada))
            {
                throw new Exception("Para contabilizar el apunte de banco debe tener seleccionado también el apunte de contabilidad y que el descuadre sea la comisión.");
            }
            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = "62600022"; // Comisiones Amazon Pay
            linea1.Concepto = $"Comisión Amazon Pay {importeOriginal.ToString("c").Replace(" ", "")}-{importeComision.ToString("c").Replace(" ", "")}={importeIngresado.ToString("c").Replace(" ", "")} ({(importeComision / importeOriginal).ToString("p").Replace(" ", "")})";
            if (comisionDescontada != 0)
            {
                linea1.Concepto += $"-{comisionDescontada.ToString("c").Replace(" ", "")}";
            }
            linea1.Concepto = FuncionesAuxiliaresReglas.FormatearConcepto(linea1.Concepto);

            // Obtener los últimos 10 caracteres
            string referenciaCompleta = apuntesBancarios.First().Referencia2.Trim();
            int longitud = referenciaCompleta.Length;
            int caracteresDeseados = 10;
            string ultimos10Caracteres;
            if (longitud >= caracteresDeseados)
            {
                ultimos10Caracteres = referenciaCompleta.Substring(longitud - caracteresDeseados);
            }
            else
            {
                // Manejar el caso donde la cadena es menor a 10 caracteres si es necesario
                ultimos10Caracteres = referenciaCompleta;
            }
            linea1.Documento = ultimos10Caracteres;
            linea1.Fecha = new DateOnly(apuntesBancarios.First().FechaOperacion.Year, apuntesBancarios.First().FechaOperacion.Month, apuntesBancarios.First().FechaOperacion.Day);
            linea1.Delegacion = "ALG";
            linea1.FormaVenta = "VAR";
            linea1.Departamento = "ADM";
            linea1.CentroCoste = "CA";
            linea1.Debe = importeComision;
            linea1.Contrapartida = banco.CuentaContable;
            lineas.Add(linea1);

            ReglaContabilizacionResponse response = new ReglaContabilizacionResponse
            {
                Lineas = lineas
            };

            return response;
        }

        public bool EsContabilizable(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return false;
            }            
            
            var importeIngresado = apuntesBancarios.Sum(b => b.ImporteMovimiento);
            var comisionDescontada = -apuntesContabilidad.Where(c => c.Documento?.Trim() == "COMIS_AMZ").Sum(c => c.Importe);
            var importeComision = apuntesContabilidad.Sum(c => c.Importe) - importeIngresado;
            var importeOriginal = importeIngresado + importeComision + comisionDescontada;


            if (apuntesBancarios.All(b => 
                    b.ConceptoComun == "02" &&
                    b.ConceptoPropio == "032" &&
                    b.RegistrosConcepto != null &&
                    b.RegistrosConcepto.Any() &&
                    b.RegistrosConcepto[0]?.Concepto.ToLower().Trim() == "amazon payments europe sca"
                ) &&
                VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0 && a.Documento?.Trim() != "COMIS_AMZ"), comisionDescontada)
                )
            {
                return true;
            }

            return false;
        }

        private bool VerificarImportesStandard(decimal importeOriginal, decimal importeComision, decimal importeIngresado, int numeroPagos, decimal comisionDescontada)
        {
            // La comisión de Amazon Pay es del 2.7% más 0.35 € por cada pago
            decimal porcentajeComision = 0.027m;
            decimal fijoComision = 0.35m;

            // Calcular la parte variable (2.7% del importe original)
            decimal importeVariable = Math.Round(importeOriginal * porcentajeComision, 2);

            // La diferencia entre la comisión calculada y la parte variable debe ser múltiplo de 0.35 €
            decimal parteFijaCalculada = importeComision - importeVariable;

            bool esMultiploDeFijo = Math.Abs(parteFijaCalculada % fijoComision) <= 0.01m;

            // Verificar que la comisión es correcta y que el importe ingresado coincide
            return esMultiploDeFijo && importeOriginal - importeComision - comisionDescontada == importeIngresado;
        }

    }
}
