using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    class ReglaAsociacionEsteticistas : IReglaContabilizacion
    {
        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteBancario = apuntesBancarios.First();
            var apunteContabilidad = apuntesContabilidad.First();

            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = "62920000";
            var profesora = apunteBancario.RegistrosConcepto[3]?.ConceptoCompleto?.Length >= 25 ?
                apunteBancario.RegistrosConcepto[3]?.ConceptoCompleto?.Substring(25).Trim() :
                apunteBancario.RegistrosConcepto[4]?.ConceptoCompleto?.Substring(25).Trim();
            profesora = profesora?.Replace("NUEVA VISION", string.Empty);
            var concepto = $"{apunteBancario.RegistrosConcepto[2]?.Concepto?.Trim()} {profesora}";
            linea1.Concepto = FuncionesAuxiliaresReglas.FormatearConcepto(concepto);

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
            if (apunteBancario.RegistrosConcepto[3].Concepto2.ToUpper().Contains("NOELIA") ||
                apunteBancario.RegistrosConcepto[3].Concepto2.ToUpper().Contains("ELENA"))
            {
                linea1.Delegacion = "ALC";
            }
            else
            {
                linea1.Delegacion = "REI";
            }
            linea1.Departamento = "CUR";
            linea1.CentroCoste = "PA"; // Pilar Álvarez

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

            if (apunteBancario.ConceptoComun == "03" &&
                apunteBancario.ConceptoPropio == "049" &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                apunteBancario.RegistrosConcepto[0]?.Concepto.ToUpper().Trim() == "COREASOCIACION DE ESTETICISTAS DE LA C")
            {
                return true;
            }

            return false;
        }
    }
}
