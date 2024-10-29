using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaMiraviaComision : IReglaContabilizacion
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
            var comisionDescontada = -apuntesContabilidad.Where(c => c.Documento?.Trim() == "COMIS_MRVA").Sum(c => c.Importe);
            var importeComision = apuntesContabilidad.Sum(c => c.Importe) - apunteBancario.ImporteMovimiento;
            var importeOriginal = importeIngresado + importeComision + comisionDescontada;

            if (importeDescuadre == 0M
                || !VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0), comisionDescontada))
            {
                throw new Exception("Para contabilizar el apunte de banco debe tener seleccionado también el apunte de contabilidad y que el descuadre sea la comisión.");
            }
            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = "62600027"; // Comisiones Miravia
            linea1.Concepto = $"Comisión Miravia {importeOriginal.ToString("c").Replace(" ", "")}-{importeComision.ToString("c").Replace(" ", "")}={importeIngresado.ToString("c").Replace(" ", "")} ({(importeComision / importeOriginal).ToString("p").Replace(" ", "")})";
            if (comisionDescontada != 0)
            {
                linea1.Concepto += $"-{comisionDescontada.ToString("c").Replace(" ", "")}";
            }
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
            var comisionDescontada = -apuntesContabilidad.Where(c => c.Documento?.Trim() == "COMIS_MRVA").Sum(c => c.Importe);
            var importeComision = apuntesContabilidad.Sum(c => c.Importe) - apunteBancario.ImporteMovimiento;
            var importeOriginal = importeIngresado + importeComision + comisionDescontada;


            if (apunteBancario.ConceptoComun == "02" &&
                apunteBancario.ConceptoPropio == "032" &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                apunteBancario.RegistrosConcepto[0]?.Concepto.ToUpper().Trim() == "ALIPAY (EUROPE) LIMITED SA" &&
                VerificarImportesStandard(importeOriginal, importeComision, importeIngresado, apuntesContabilidad.Count(a => a.Importe > 0 && a.Documento?.Trim() != "COMIS_MRVA"), comisionDescontada)
                )
            {
                return true;
            }

            return false;
        }

        private bool VerificarImportesStandard(decimal importeOriginal, decimal importeComision, decimal importeIngresado, int numeroPagos, decimal comisionDescontada)
        {
            // La comisión de Miravia es del 9% más 1,80 € por cada envío y 0,10 € por cada transferencia
            decimal porcentajeComision = 0.09m;
            decimal fijoEnvio = 1.8m;
            decimal fijoTransferencia = .1M;

            decimal comisionCalculada = importeOriginal * porcentajeComision;
            decimal importeCalculado = importeOriginal - comisionCalculada - (fijoEnvio * numeroPagos) - fijoTransferencia;
            bool cuadraImporte = importeCalculado - importeIngresado < 0.01m;

            // Verificar que la comisión es correcta y que el importe ingresado coincide
            bool resultado = cuadraImporte && importeOriginal - importeComision - comisionDescontada == importeIngresado;
            return resultado;
        }
    }
}
