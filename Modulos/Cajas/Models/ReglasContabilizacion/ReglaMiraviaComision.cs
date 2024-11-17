using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaMiraviaComision : IReglaContabilizacion
    {
        public string Nombre => "Comisiones Miravia";

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
            // La comisión de Miravia es del 9% más una tarifa por peso de envío (o de retorno) € por cada envío y 0,10 € por cada transferencia
            decimal porcentajeComision = 0.09m;
            decimal fijoTransferencia = 0.1m;

            // Crear conjunto de valores permitidos para fijoEnvio
            var valoresPermitidos = tarifas.Concat(retornos).Distinct();

            // Verificar si algún valor en 'valoresPermitidos' permite cuadrar los importes
            bool resultado = valoresPermitidos.Any(fijoEnvio =>
            {
                decimal comisionCalculada = importeOriginal * porcentajeComision;
                decimal importeCalculado = importeOriginal - comisionCalculada - (fijoEnvio * numeroPagos) - fijoTransferencia;
                bool cuadraImporte = Math.Abs(importeCalculado - importeIngresado) < 0.01m;

                // Verificar que la comisión es correcta y que el importe ingresado coincide
                return cuadraImporte && importeOriginal - importeComision - comisionDescontada == importeIngresado;
            });

            return resultado;
        }

        private static List<decimal> tarifas = new List<decimal>
        {
            2.40m, 2.50m, 2.70m, 2.80m, 4.00m, 4.20m, 4.30m, 4.47m, 4.78m,
            5.28m, 6.00m, 6.45m, 6.58m, 7.20m, 7.64m, 7.76m, 7.94m, 8.16m,
            8.96m, 9.28m, 9.56m, 9.86m, 10.80m, 11.18m, 11.20m, 12.34m, 12.68m,
            12.93m, 13.87m, 14.00m, 14.76m, 14.87m, 15.40m, 15.82m, 15.98m,
            16.18m, 16.53m, 17.67m
        };

        private static List<decimal> retornos = new List<decimal>
        {
            1.20m, 1.25m, 1.35m, 1.40m, 2.00m, 2.10m, 2.15m, 2.24m, 2.39m,
            2.64m, 3.00m, 3.23m, 3.29m, 3.32m, 3.60m, 3.82m, 3.88m, 3.97m,
            4.08m, 4.48m, 4.64m, 4.78m, 4.93m, 5.40m, 5.59m, 5.60m, 6.17m,
            6.34m, 6.47m, 6.94m, 7.00m, 7.38m, 7.44m, 7.70m, 7.91m, 7.99m,
            8.09m, 8.27m, 8.84m
        };

        
    }
}
