using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaStripe : IReglaContabilizacion
    {
        public string Nombre => "Stripe";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteBancario = apuntesBancarios.Single();

            var importeDescuadre = apunteBancario.ImporteMovimiento - apuntesContabilidad.Sum(c => c.Importe);

            var importeIngresado = apunteBancario.ImporteMovimiento;
            var importeComision = -importeDescuadre;
            var importeOriginal = importeIngresado + importeComision;

            if (importeDescuadre == 0M 
                || !(VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0)) 
                    || VerificarImportesPremium(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0))))
            {
                throw new Exception("Para contabilizar el apunte de banco debe tener seleccionado también el apunte de contabilidad y que el descuadre sea la comisión.");
            }
            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.TipoCuenta = Constantes.TiposCuenta.PROVEEDOR;
            linea1.Cuenta = "1071"; // Stripe
            linea1.Contacto = "0";            
            linea1.Concepto = $"Comisión Stripe {importeOriginal.ToString("c")}-{importeComision.ToString("c")}={importeIngresado.ToString("c")} ({(importeComision / importeOriginal).ToString("p")})";

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
            linea1.Debe = importeComision;
            linea1.Contrapartida = banco.CuentaContable;
            lineas.Add(linea1);

            ReglaContabilizacionResponse response = new ReglaContabilizacionResponse
            {
                Lineas = lineas
            };

            return response;
        }

        private bool VerificarImportesStandard(decimal importeOriginal, decimal importeComision, decimal importeIngresado, int numeroPagos)
        {
            // La comisión de Stripe es del 1.5% más 0.25 €
            decimal porcentajeComision = 0.015m;
            decimal fijoComision = 0.25m;
            
            // Calcular comisión esperada
            decimal comisionEsperada = Math.Round(((importeOriginal * porcentajeComision) + fijoComision) * numeroPagos, 2, MidpointRounding.AwayFromZero);
            
            // Verificar si los valores coinciden
            return importeComision == comisionEsperada && importeOriginal - importeComision == importeIngresado;
        }

        private bool VerificarImportesPremium(decimal importeOriginal, decimal importeComision, decimal importeIngresado, int numeroPagos)
        {
            // La comisión de Stripe para tarjetas Premium es del 1.9% más 0.25 €
            decimal fijoComision = 0.25m;
            decimal porcentajeComisionTarjetaPremium = 0.019m;

            // Calcular comisión esperada
            decimal comisionEsperadaPremium = Math.Round(((importeOriginal * porcentajeComisionTarjetaPremium) + fijoComision) * numeroPagos, 2, MidpointRounding.AwayFromZero);

            // Verificar si los valores coinciden
            return importeComision == comisionEsperadaPremium && importeOriginal - importeComision == importeIngresado;
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
                apunteBancario.RegistrosConcepto[0]?.Concepto.ToUpper().Trim() == "STRIPE" &&
                (VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0)) || 
                VerificarImportesPremium(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0))))
            {
                return true;
            }

            return false;
        }
    }
}
