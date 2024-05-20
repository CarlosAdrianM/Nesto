using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    class ReglaAmazonPayComision : IReglaContabilizacion
    {
        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || apuntesBancarios.Count() != 1 || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteBancario = apuntesBancarios.Single();
            var importeDescuadre = apuntesBancarios.Sum(b => b.ImporteMovimiento) - apuntesContabilidad.Sum(c => c.Importe);

            var importeIngresado = apunteBancario.ImporteMovimiento;
            var importeComision = -importeDescuadre; ;
            var importeOriginal = importeIngresado + importeComision;

            if (importeDescuadre == 0M
                || !VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0)))
            {
                throw new Exception("Para contabilizar el apunte de banco debe tener seleccionado también el apunte de contabilidad y que el descuadre sea la comisión.");
            }
            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = "62600022"; // Comisiones Amazon Pay
            linea1.Concepto = $"Comisión Amazon Pay {importeOriginal.ToString("c").Replace(" ", "")}-{importeComision.ToString("c").Replace(" ", "")}={importeIngresado.ToString("c").Replace(" ", "")} ({(importeComision / importeOriginal).ToString("p").Replace(" ", "")})";
            linea1.Concepto = FuncionesAuxiliaresReglas.FormatearConcepto(linea1.Concepto);

            // Obtener los últimos 10 caracteres
            string referenciaCompleta = apunteBancario.Referencia2.Trim();
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
            linea1.Fecha = new DateOnly(apunteBancario.FechaOperacion.Year, apunteBancario.FechaOperacion.Month, apunteBancario.FechaOperacion.Day);
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
            if (apuntesBancarios is null || apuntesContabilidad is null || apuntesBancarios.Count() != 1 || !apuntesContabilidad.Any())
            {
                return false;
            }
            var apunteBancario = apuntesBancarios.Single();
            
            var importeIngresado = apunteBancario.ImporteMovimiento;
            var importeComision = apuntesContabilidad.Sum(c => c.Importe) - apunteBancario.ImporteMovimiento;
            var importeOriginal = importeIngresado + importeComision;


            if (apunteBancario.ConceptoComun == "02" &&
                apunteBancario.ConceptoPropio == "032" &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                apunteBancario.RegistrosConcepto[0]?.Concepto.ToLower().Trim() == "amazon payments europe sca" &&
                VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0)))
            {
                return true;
            }

            return false;
        }

        private bool VerificarImportesStandard(decimal importeOriginal, decimal importeComision, decimal importeIngresado, int numeroPagos)
        {
            // La comisión de Amazon Pay es del 2.7% más 0.35 €
            decimal porcentajeComision = 0.027m;
            decimal fijoComision = 0.35m;

            // Calcular comisión esperada
            decimal comisionEsperadaAlza = Math.Round((importeOriginal * porcentajeComision) + (fijoComision * numeroPagos), 2, MidpointRounding.ToPositiveInfinity);
            decimal comisionEsperadaBaja = Math.Round((importeOriginal * porcentajeComision) + (fijoComision * numeroPagos), 2, MidpointRounding.ToNegativeInfinity);

            // Verificar si los valores coinciden
            return (importeComision == comisionEsperadaAlza || importeComision == comisionEsperadaBaja) && importeOriginal - importeComision == importeIngresado;
        }
    }
}
