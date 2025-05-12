using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaAdelantosNomina : IReglaContabilizacion
    {
        public string Nombre => "Adelantos nómina";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            ApunteBancarioDTO apunteBancario = apuntesBancarios.First();
            _ = apuntesContabilidad.First();

            List<PreContabilidadDTO> lineas = new();
            PreContabilidadDTO linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = "46000000";
            string concepto = apunteBancario.RegistrosConcepto[2]?.ConceptoCompleto?.Trim() ?? string.Empty;
            concepto = $"Adelanto nómina {concepto}";
            linea1.Concepto = concepto[..Math.Min(50, concepto.Length)];

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

            return (
                (apunteBancario.ConceptoComun == "99" && apunteBancario.ConceptoPropio == "067") ||
                (apunteBancario.ConceptoComun == "04" && apunteBancario.ConceptoPropio == "002")
                ) &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                apunteBancario.RegistrosConcepto[1]?.Concepto.ToUpper().Trim() == "ADELANTO NOMINA NUEVA VISION";
        }
    }
}
