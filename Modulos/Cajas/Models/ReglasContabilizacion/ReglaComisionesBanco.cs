using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaComisionesBanco : IReglaContabilizacion
    {
        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || !apuntesBancarios.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteBancario = apuntesBancarios.First();
            //var apunteContabilidad = apuntesContabilidad.First();
            //var importeDescuadre = apuntesBancarios.Sum(b => b.ImporteMovimiento) - apuntesContabilidad.Sum(c => c.Importe);

            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = "62600004";
            linea1.Concepto = $"Comisión {banco.Nombre.Trim()} {apunteBancario.RegistrosConcepto[0]?.Concepto2}" ;
            linea1.Concepto = FuncionesAuxiliaresReglas.FormatearConcepto(linea1.Concepto);

            // Obtener los últimos 10 caracteres
            string referenciaCompleta = apunteBancario.RegistrosConcepto[1]?.Concepto;
            string ultimos10Caracteres = FuncionesAuxiliaresReglas.UltimosDiezCaracteres(referenciaCompleta);
            linea1.Documento = ultimos10Caracteres;
            linea1.Fecha = new DateOnly(apunteBancario.FechaOperacion.Year, apunteBancario.FechaOperacion.Month, apunteBancario.FechaOperacion.Day);
            linea1.Delegacion = "ALG";
            linea1.Departamento = "ADM";
            linea1.CentroCoste = "CA";
            linea1.Debe = -apunteBancario.ImporteMovimiento;
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
            if (apuntesBancarios is null || !apuntesBancarios.Any())
            {
                return false;
            }
            var apunteBancario = apuntesBancarios.First();

            if ((apunteBancario.ConceptoComun == "17" || apunteBancario.ConceptoComun == "12" || apunteBancario.ConceptoComun == "02") &&
                (apunteBancario.ConceptoPropio == "036" || apunteBancario.ConceptoPropio == "040") &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                (
                    (apunteBancario.RegistrosConcepto[0]?.Concepto2.Trim() == "PRECIO ED. EXTRACTO" && -apunteBancario.ImporteMovimiento == 12M) ||
                    apunteBancario.RegistrosConcepto[0]?.Concepto2.Trim() == "PRECIO ABONO TRF." ||
                    apunteBancario.RegistrosConcepto[0]?.Concepto2.Trim() == "SERV. EM. TRANSF." ||
                    apunteBancario.RegistrosConcepto[0]?.Concepto2.Trim() == "C. SERVIC. PAYGOLD" ||
                    (apunteBancario.RegistrosConcepto[0]?.Concepto2.Trim() == "MANTENIMIENTO TPV" && -apunteBancario.ImporteMovimiento == 5.45M) ||
                    (apunteBancario.RegistrosConcepto[0]?.Concepto2.Trim() == "BONIF. MULTIDIVISA" && apunteBancario.ImporteMovimiento > 0M)
                ))
            {
                return true;
            }

            return false;
        }
    }
}
