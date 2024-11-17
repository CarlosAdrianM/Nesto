using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaSegurosSaludPrivados : IReglaContabilizacion
    {
        public string Nombre => "Seguros de salud privados";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteBancario = apuntesBancarios.First();
            var apunteContabilidad = apuntesContabilidad.First();
            var importeDescuadre = apuntesBancarios.Sum(b => b.ImporteMovimiento) - apuntesContabilidad.Sum(c => c.Importe);

            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = "64900005";
            linea1.Concepto = "Seguros salud privados";
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

            var concepto = apunteBancario.RegistrosConcepto[0]?.Concepto.Trim();
            if (concepto != null && concepto.Length > 4)
            {
                concepto = apunteBancario.RegistrosConcepto[0]?.Concepto.Trim().Substring(4);
            }
            else
            {
                concepto = string.Empty;
            }

            if (apunteBancario.ConceptoComun == "15" &&
                apunteBancario.ConceptoPropio == "051" &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                (apunteBancario.RegistrosConcepto[0]?.Concepto2.Trim() == "PACK MULTISEGUROS") ||
                concepto == "DKV SEGUROS Y REASEGUROS, S.A.")
            {
                return true;
            }

            return false;
        }
    }
}
