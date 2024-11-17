using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaGastoPeriodico : IReglaContabilizacion
    {
        private readonly IContabilidadService _contabilidadService;

        public ReglaGastoPeriodico(IContabilidadService contabilidadService)
        {
            _contabilidadService = contabilidadService;
        }

        public string Nombre => "Gasto periódico";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteBancario = apuntesBancarios.First();
            var apunteContabilidad = apuntesContabilidad.First();
            var importeDescuadre = apuntesBancarios.Sum(b => b.ImporteMovimiento) - apuntesContabilidad.Sum(c => c.Importe);

            string textoConcepto = apunteBancario.RegistrosConcepto[0].Concepto.ToLower().Trim();
            if (textoConcepto.StartsWith("core"))
            {
                textoConcepto = textoConcepto.Substring(4);
            }

            var cuentasContables = Task.Run(async () => await _contabilidadService.LeerCuentasPorConcepto(Constantes.Empresas.EMPRESA_DEFECTO, textoConcepto, DateTime.Today.AddYears(-2), DateTime.Today)).GetAwaiter().GetResult();
            if (cuentasContables is null || cuentasContables.Count != 1)
            {
                return null;
            }
            var cuentaContable = cuentasContables.Single();

            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = cuentaContable.Cuenta;
            linea1.Concepto = $"{textoConcepto} {apunteBancario.RegistrosConcepto[2]?.ConceptoCompleto}";
            linea1.Concepto = FuncionesAuxiliaresReglas.FormatearConcepto(linea1.Concepto);

            // Obtener los últimos 10 caracteres
            string referenciaCompleta = apunteBancario.RegistrosConcepto[1]?.Concepto;
            string ultimos10Caracteres = FuncionesAuxiliaresReglas.UltimosDiezCaracteres(referenciaCompleta);
            linea1.Documento = ultimos10Caracteres;
            linea1.Fecha = new DateOnly(apunteBancario.FechaOperacion.Year, apunteBancario.FechaOperacion.Month, apunteBancario.FechaOperacion.Day);
            linea1.Delegacion = cuentaContable.Delegacion;
            linea1.Departamento = cuentaContable.Departamento;
            linea1.CentroCoste = cuentaContable.CentroCoste;
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
                (apunteBancario.ConceptoPropio == "035" || apunteBancario.ConceptoPropio == "038") &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                apunteBancario.RegistrosConcepto[0] != null)
            {
                string textoConcepto = apunteBancario.RegistrosConcepto[0].Concepto.ToLower().Trim();
                if (textoConcepto.StartsWith("core"))
                {
                    textoConcepto = textoConcepto.Substring(4);
                }
                var cuentasContables = Task.Run(async () => await _contabilidadService.LeerCuentasPorConcepto(Constantes.Empresas.EMPRESA_DEFECTO, textoConcepto, DateTime.Today.AddYears(-2), DateTime.Today)).GetAwaiter().GetResult();
                if (cuentasContables is null || cuentasContables.Count != 1)
                {
                    return false;
                }
                
                return true;
            }

            return false;
        }
    }
}
