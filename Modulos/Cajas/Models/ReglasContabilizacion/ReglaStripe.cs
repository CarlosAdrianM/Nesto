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

            var importesOriginales = apuntesContabilidad.Select(a => a.Debe).ToList();
            var sumaOriginales = importesOriginales.Sum();
            var importeIngresado = apunteBancario.ImporteMovimiento;
            var importeComision = sumaOriginales - importeIngresado;
            var importeOriginal = importeIngresado + importeComision;

            if (importeDescuadre == 0M
                || !VerificarImportesCombinadosPorMovimiento(importesOriginales, importeComision, importeIngresado))
            {
                throw new Exception("Para contabilizar el apunte de banco debe tener seleccionado también el apunte de contabilidad y que el descuadre sea la comisión.");
            }

            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.TipoCuenta = Constantes.TiposCuenta.PROVEEDOR;
            linea1.Cuenta = "1071"; // Stripe
            linea1.Contacto = "0";
            linea1.Concepto = $"Comisión Stripe {importeOriginal:c}-{importeComision:c}={importeIngresado:c} ({importeComision / importeOriginal:p})";

            // Obtener los últimos 10 caracteres
            string referenciaCompleta = apunteBancario.Referencia2.Trim();
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

        // Nesto#406: tarifas OFICIALES de Stripe para negocios EEE (stripe.com/en-es/pricing,
        // verificado 16/07/26). El botón solo se activa con cuadre EXACTO contra alguna
        // combinación de estas tarifas por pago: así detectar el tipo de movimiento sigue
        // significando "sabemos contabilizarlo". Si Stripe cobra algo que no cuadra (error suyo
        // o caso no contemplado), el botón NO se activa: hay que mirar el desglose del fee en el
        // dashboard de Stripe y, si es una tarifa oficial nueva, añadirla aquí.
        private static readonly (decimal Porcentaje, decimal Fijo)[] TarifasStripe =
        {
            (0.015m, 0.25m),  // estándar EEE
            (0.019m, 0.25m),  // premium EEE (comercial/empresa)
            (0.025m, 0.25m),  // tarjetas UK
            (0.0325m, 0.25m)  // internacional (no EEE)
        };

        private bool VerificarImportesCombinadosPorMovimiento(IList<decimal> importesOriginales, decimal importeComision, decimal importeIngresado)
        {
            int numeroPagos = importesOriginales.Count;
            // 4^n combinaciones: con más de 10 pagos (1M combos) no merece la pena la fuerza bruta
            if (numeroPagos == 0 || numeroPagos > 10)
            {
                return false;
            }

            decimal sumaOriginales = Math.Round(importesOriginales.Sum(), 2, MidpointRounding.AwayFromZero);
            long combinaciones = 1;
            for (int i = 0; i < numeroPagos; i++)
            {
                combinaciones *= TarifasStripe.Length;
            }

            for (long mask = 0; mask < combinaciones; mask++)
            {
                decimal comisionTotal = 0m;
                long resto = mask;

                for (int i = 0; i < numeroPagos; i++)
                {
                    var tarifa = TarifasStripe[resto % TarifasStripe.Length];
                    resto /= TarifasStripe.Length;
                    comisionTotal += Math.Round((importesOriginales[i] * tarifa.Porcentaje) + tarifa.Fijo, 2, MidpointRounding.AwayFromZero);
                }

                comisionTotal = Math.Round(comisionTotal, 2, MidpointRounding.AwayFromZero);

                // Comparar comisiones exactas y que la resta (suma original - comisión total) cuadre con lo ingresado
                decimal netoCalculado = Math.Round(sumaOriginales - comisionTotal, 2, MidpointRounding.AwayFromZero);

                if (comisionTotal == importeComision && netoCalculado == importeIngresado)
                {
                    return true; // combinación válida encontrada
                }
            }

            return false;
        }

        public bool EsContabilizable(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || apuntesBancarios.Count() != 1 || !apuntesContabilidad.Any())
            {
                return false;
            }
            var apunteBancario = apuntesBancarios.Single();

            decimal importeIngresado = apunteBancario.ImporteMovimiento;
            // Suma de los importes originales por movimiento (usamos Debe porque dijiste que Importes es readonly)
            var importesOriginales = apuntesContabilidad.Select(a => a.Debe).ToList();
            decimal sumaOriginales = importesOriginales.Sum();
            decimal importeComision = sumaOriginales - importeIngresado;

            if (apunteBancario.ConceptoComun == "02" &&
                apunteBancario.ConceptoPropio == "032" &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                apunteBancario.RegistrosConcepto[0]?.Concepto.ToUpper().Trim() == "STRIPE" &&
                VerificarImportesCombinadosPorMovimiento(importesOriginales, importeComision, importeIngresado))
            {
                return true;
            }

            return false;
        }
    }
}
