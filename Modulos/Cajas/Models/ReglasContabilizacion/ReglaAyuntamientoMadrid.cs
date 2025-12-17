using ControlesUsuario.Dialogs;
using Nesto.Modulos.Cajas.ViewModels;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaAyuntamientoMadrid : IReglaContabilizacion
    {
        private readonly IDialogService _dialogService;

        public ReglaAyuntamientoMadrid(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public string Nombre => "Ayuntamiento Madrid";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }

            ApunteBancarioDTO apunteBancario = apuntesBancarios.First();
            ContabilidadDTO apunteContabilidad = apuntesContabilidad.First();

            // Buscar el registro que contiene el concepto de Madrid
            var registroMadrid = apunteBancario.RegistrosConcepto.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r?.ConceptoCompleto) &&
                (r.ConceptoCompleto.StartsWith("COREAYUNTAMIENTO DE MADRID") ||
                 r.ConceptoCompleto.StartsWith("PAGO AYTO. MADRID")));

            if (registroMadrid == null)
            {
                return new ReglaContabilizacionResponse();
            }

            // Determinar si necesitamos pedir concepto adicional
            bool esPagoAytoMadrid = registroMadrid.ConceptoCompleto.StartsWith("PAGO AYTO. MADRID");
            string conceptoAdicional = string.Empty;

            if (esPagoAytoMadrid)
            {
                conceptoAdicional = _dialogService.GetText("Concepto adicional", "Introduzca el concepto adicional para el pago:");
                if (conceptoAdicional == null)
                {
                    // Usuario canceló
                    return null;
                }
            }

            List<PreContabilidadDTO> lineas = [];
            PreContabilidadDTO linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";

            // Determinar la cuenta según el concepto
            string conceptoCompleto = apunteBancario.RegistrosConcepto.Count > 2
                ? apunteBancario.RegistrosConcepto[2].ConceptoCompleto
                : apunteBancario.RegistrosConcepto[0].ConceptoCompleto;

            string textoConcepto = $"{conceptoCompleto} {conceptoAdicional}";

            if (textoConcepto.Contains("IBI", StringComparison.OrdinalIgnoreCase))
            {
                linea1.Cuenta = "63100000";
            }
            else if (textoConcepto.Contains("IAE", StringComparison.OrdinalIgnoreCase))
            {
                linea1.Cuenta = "63100001";
            }
            else
            {
                linea1.Cuenta = "63100003";
            }


            // Construir el concepto
            if (esPagoAytoMadrid)
            {
                linea1.Concepto = $"Ayto. Madrid {conceptoAdicional}";
            }
            else
            {
                linea1.Concepto = conceptoCompleto?.Trim()[4..];
                linea1.Concepto = FuncionesAuxiliaresReglas.FormatearConcepto(linea1.Concepto);
            }

            // Obtener los últimos 10 caracteres de la referencia
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
                ultimos10Caracteres = referenciaCompleta;
            }
            linea1.Documento = ultimos10Caracteres;
            linea1.Fecha = new DateOnly(apunteBancario.FechaOperacion.Year, apunteBancario.FechaOperacion.Month, apunteBancario.FechaOperacion.Day);
            linea1.Delegacion = "REI";
            linea1.Departamento = "ADM";
            linea1.CentroCoste = "CA";
            linea1.Debe = -apunteBancario.ImporteMovimiento;
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
            if (apuntesBancarios is null || !apuntesBancarios.Any())
            {
                return false;
            }
            ApunteBancarioDTO apunteBancario = apuntesBancarios.First();

            return apunteBancario.ConceptoComun == "17" &&
                apunteBancario.ConceptoPropio == "016" &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any(r =>
                    !string.IsNullOrEmpty(r?.ConceptoCompleto) &&
                    (r.ConceptoCompleto.StartsWith("COREAYUNTAMIENTO DE MADRID") ||
                     r.ConceptoCompleto.StartsWith("PAGO AYTO. MADRID")));
        }
    }
}
