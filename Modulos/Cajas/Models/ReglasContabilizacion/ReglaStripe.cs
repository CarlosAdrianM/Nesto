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

            // Nesto#406: Importe (Debe - Haber) para que los reembolsos entren en negativo
            var importesOriginales = apuntesContabilidad.Select(a => a.Importe).ToList();
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
            (0.019m, 0.25m),  // premium EEE (comercial/empresa), tarifa anterior: se queda para los movimientos antiguos
            (0.028m, 0.25m),  // Nesto#542: premium EEE actual (stripe.com/es/pricing, verificado 22/10/26)
            (0.025m, 0.25m),  // tarjetas UK
            (0.0325m, 0.25m), // internacional (no EEE)
            (0.0499m, 0.40m)  // Nesto#542: Klarna «a partir de 4,99 % + 0,40 €» (stripe.com/es/pricing, verificado 22/10/26).
                              // Caso real: 149,75 → comisión 7,87 → neto 141,88. Si aparece otro tramo de Klarna
                              // (país o plazo), mirar el desglose en el dashboard de Stripe y añadirlo aquí.
        };

        // Nesto#542: tope de cargos por payout. Ya no hay fuerza bruta (ver VerificarImportesCombinadosPorMovimiento),
        // pero se mantiene un límite holgado por si se selecciona media contabilidad por error.
        private const int MAXIMO_CARGOS = 100;

        private bool VerificarImportesCombinadosPorMovimiento(IList<decimal> importesOriginales, decimal importeComision, decimal importeIngresado)
        {
            // Nesto#406 (caso real 16/07/26: payout de 38,62 = cargos 39,95 + 3,90 - reembolso
            // 3,90, comisiones 1,01 + 0,32 + 0,00): los REEMBOLSOS (importes negativos) llevan
            // comisión 0 (Stripe no devuelve la del cargo original) y solo aportan su importe al
            // neto. Solo los CARGOS (positivos) entran en las combinaciones de tarifas, así que
            // el cuadre sigue siendo EXACTO: un descuadre arbitrario no activa el botón.
            List<decimal> cargos = importesOriginales.Where(i => i > 0).ToList();
            int numeroPagos = cargos.Count;
            if (numeroPagos == 0 || numeroPagos > MAXIMO_CARGOS || importeComision <= 0)
            {
                return false;
            }

            decimal sumaOriginales = Math.Round(importesOriginales.Sum(), 2, MidpointRounding.AwayFromZero);
            // El neto tiene que cuadrar con lo ingresado restando exactamente la comisión
            decimal netoCalculado = Math.Round(sumaOriginales - importeComision, 2, MidpointRounding.AwayFromZero);
            if (netoCalculado != importeIngresado)
            {
                return false;
            }

            // Nesto#542: antes se probaban las T^n combinaciones de tarifas (con 6 tarifas y 10 cargos,
            // ~60 millones) y la pantalla de Bancos se quedaba colgada. Ahora se recorren los cargos
            // guardando solo las comisiones PARCIALES alcanzables (en céntimos, sin repetidos) y se
            // poda cualquier suma que ya se pase de la comisión buscada. El número de estados está
            // acotado por los céntimos de la comisión, así que el coste es lineal en cargos y tarifas.
            // El cuadre sigue siendo EXACTO: un descuadre arbitrario no activa el botón.
            HashSet<decimal> alcanzables = [0m];
            foreach (decimal cargo in cargos)
            {
                List<decimal> comisionesCargo = TarifasStripe
                    .Select(t => Math.Round((cargo * t.Porcentaje) + t.Fijo, 2, MidpointRounding.AwayFromZero))
                    .Distinct()
                    .ToList();
                HashSet<decimal> siguientes = [];
                foreach (decimal parcial in alcanzables)
                {
                    foreach (decimal comision in comisionesCargo)
                    {
                        decimal suma = parcial + comision;
                        if (suma <= importeComision)
                        {
                            _ = siguientes.Add(suma);
                        }
                    }
                }
                if (siguientes.Count == 0)
                {
                    return false;
                }
                alcanzables = siguientes;
            }

            return alcanzables.Contains(importeComision);
        }

        public bool EsContabilizable(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || apuntesBancarios.Count() != 1 || !apuntesContabilidad.Any())
            {
                return false;
            }
            var apunteBancario = apuntesBancarios.Single();

            decimal importeIngresado = apunteBancario.ImporteMovimiento;
            // Nesto#406: Importe (Debe - Haber) para que los reembolsos entren en negativo
            var importesOriginales = apuntesContabilidad.Select(a => a.Importe).ToList();
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
