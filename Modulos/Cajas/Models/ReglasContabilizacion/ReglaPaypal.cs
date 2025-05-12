using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaPaypal : IReglaContabilizacion
    {
        public string Nombre => "Paypal";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || apuntesBancarios.Count() < 1 || apuntesBancarios.Count() > 2 || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            ApunteBancarioDTO apunteBancario = apuntesBancarios.Single(a => a.ImporteMovimiento > 0);

            decimal importeDescuadre = apuntesBancarios.Sum(a => a.ImporteMovimiento) - apuntesContabilidad.Sum(c => c.Importe);

            decimal importeIngresado = apuntesBancarios.Sum(a => a.ImporteMovimiento);
            decimal importeComision = -importeDescuadre;
            decimal importeOriginal = importeIngresado + importeComision;

            if (importeDescuadre == 0M
                || !(VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0))
                    || VerificarImportesInternacional(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0))))
            {
                throw new Exception("Para contabilizar el apunte de banco debe tener seleccionado también el apunte de contabilidad y que el descuadre sea la comisión.");
            }
            List<PreContabilidadDTO> lineas = [];
            PreContabilidadDTO linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE;
            linea1.Cuenta = "62600020"; // Comisiones Paypal
            //linea1.Contacto = "0";
            linea1.Concepto = $"Comisión Paypal {importeOriginal:c}-{importeComision:c}={importeIngresado:c} ({importeComision / importeOriginal:p})";

            // Obtener los últimos 10 caracteres
            string referenciaCompleta = apunteBancario.NumeroDocumento.Trim();
            int longitud = referenciaCompleta.Length;
            int caracteresDeseados = 10;
            string ultimos10Caracteres;
            if (longitud >= caracteresDeseados)
            {
                ultimos10Caracteres = referenciaCompleta[(longitud - caracteresDeseados)..];
            }
            else
            {
                // Manejar el caso donde la cadena es menor a 10 caracteres si es necesario
                ultimos10Caracteres = referenciaCompleta;
            }
            linea1.Documento = ultimos10Caracteres;
            linea1.Fecha = new DateOnly(apunteBancario.FechaOperacion.Year, apunteBancario.FechaOperacion.Month, apunteBancario.FechaOperacion.Day);
            linea1.Delegacion = "ALG";
            linea1.CentroCoste = "CA";
            linea1.Departamento = "ADM";
            linea1.FormaVenta = "VAR";
            linea1.Debe = importeComision;
            linea1.Contrapartida = banco.CuentaContable;
            lineas.Add(linea1);

            ReglaContabilizacionResponse response = new()
            {
                Lineas = lineas
            };

            return response;
        }

        public bool EsContabilizable(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || apuntesBancarios.Count() < 1 || apuntesBancarios.Count() > 2 || !apuntesContabilidad.Any())
            {
                return false;
            }

            if (apuntesBancarios.Count() == 1) // es un traspaso automático de Paypal CV a Caixabanl
            {
                ApunteBancarioDTO apunteBancario = apuntesBancarios.Single();

                decimal importeIngresado = apunteBancario.ImporteMovimiento;
                decimal importeComision = apuntesContabilidad.Sum(c => c.Importe) - apunteBancario.ImporteMovimiento;
                decimal importeOriginal = importeIngresado + importeComision;


                if (apunteBancario.ConceptoComun == "02" &&
                    apunteBancario.ConceptoPropio == "032" &&
                    apunteBancario.RegistrosConcepto != null &&
                    apunteBancario.RegistrosConcepto.Any() &&
                    apunteBancario.RegistrosConcepto[0] != null &&
                    apunteBancario.RegistrosConcepto[0].Concepto.ToUpper().Trim().StartsWith("PAYPAL") &&
                    (VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0)) ||
                    VerificarImportesInternacional(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0))))

                {
                    return true;
                }
            }
            else
            {
                ApunteBancarioDTO apunteBancarioVenta = apuntesBancarios.FirstOrDefault(a => a.ImporteMovimiento > 0);
                ApunteBancarioDTO apunteBancarioComision = apuntesBancarios.FirstOrDefault(a => a.ImporteMovimiento < 0);

                if (apunteBancarioVenta is null || apunteBancarioComision is null)
                {
                    return false;
                }

                decimal importeIngresado = apuntesBancarios.Sum(a => a.ImporteMovimiento);
                decimal importeComision = apuntesContabilidad.Sum(c => c.Importe) - apuntesBancarios.Sum(a => a.ImporteMovimiento);
                decimal importeOriginal = importeIngresado + importeComision;

                if ((apunteBancarioVenta.ConceptoComun == "02" || apunteBancarioVenta.ConceptoComun == "03") &&
                    apunteBancarioVenta.ConceptoPropio == "PPA" &&
                    (VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0)) ||
                    VerificarImportesInternacional(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0))))

                {
                    return true;
                }
            }

            return false;
        }

        private bool VerificarImportesStandard(decimal importeOriginal, decimal importeComision, decimal importeIngresado, int numeroPagos)
        {
            if (importeComision < 0 || importeOriginal == importeIngresado)
            {
                return false;
            }
            // La comisión de Paypal es del 2.9% más 0.35 €
            decimal porcentajeComision = 0.029m;
            decimal fijoComision = 0.35m;

            // Calcular comisión esperada
            decimal comisionEsperada = Math.Round(((importeOriginal * porcentajeComision) + fijoComision) * numeroPagos, 2, MidpointRounding.AwayFromZero);

            // Verificar si los valores coinciden
            return importeComision <= comisionEsperada && importeOriginal - importeComision == importeIngresado;
        }

        private bool VerificarImportesInternacional(decimal importeOriginal, decimal importeComision, decimal importeIngresado, int numeroPagos)
        {
            // La comisión de Paypal para transacciones internacionales lleva un 1,99% adicional
            // La comisión de Paypal es del 2.9% más 0.35 €
            decimal porcentajeComision = 0.029m;
            decimal porcentajeComisionInternacional = 0.0199m;
            decimal fijoComision = 0.35m;

            // Calcular comisión esperada
            decimal comisionEsperadaAlza = Math.Round(((importeOriginal * (porcentajeComision + porcentajeComisionInternacional)) + fijoComision) * numeroPagos, 2, MidpointRounding.AwayFromZero);
            decimal comisionEsperadaBaja = Math.Round(((importeOriginal * (porcentajeComision + porcentajeComisionInternacional)) + fijoComision) * numeroPagos, 2, MidpointRounding.ToZero);

            // Verificar si los valores coinciden
            return (importeComision == comisionEsperadaAlza || importeComision == comisionEsperadaBaja) && importeOriginal - importeComision == importeIngresado;
        }
    }
}
