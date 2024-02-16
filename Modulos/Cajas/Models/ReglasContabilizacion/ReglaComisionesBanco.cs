using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaComisionesBanco : IReglaContabilizacion
    {
        public ReglaContabilizacionResponse ApuntesContabilizar(ApunteBancarioDTO apunteBancario, BancoDTO banco, decimal importeDescuadre)
        {
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

        public bool EsContabilizable(ApunteBancarioDTO apunteBancario, ContabilidadDTO apunteContabilidad)
        {
            if (apunteBancario == null)
            {
                return false;
            }

            if (apunteBancario.ConceptoComun == "17" &&
                apunteBancario.ConceptoPropio == "036" &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                apunteBancario.RegistrosConcepto[0]?.Concepto2.Trim() == "PRECIO ED. EXTRACTO")
            {
                return true;
            }

            return false;
        }
    }
}
