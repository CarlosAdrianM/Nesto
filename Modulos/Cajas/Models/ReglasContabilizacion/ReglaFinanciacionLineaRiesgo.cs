using Nesto.Modulos.Cajas.ViewModels;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using ControlesUsuario.Dialogs;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaFinanciacionLineaRiesgo : IReglaContabilizacion
    {
        private readonly IDialogService _dialogService;
        private readonly IRecursosHumanosService _recursosHumanosService;

        public ReglaFinanciacionLineaRiesgo(IDialogService dialogService, IRecursosHumanosService recursosHumanosService)
        {
            _dialogService = dialogService;
            _recursosHumanosService = recursosHumanosService;
        }

        public string Nombre => "Línea de riesgo Caixabank";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            var apuntesBancariosCargo = apuntesBancarios.Where(c => c.ImporteMovimiento < 0);
            var apuntesBancariosAbono = apuntesBancarios.Where(c => c.ImporteMovimiento > 0);
            
            var diasAplazados = 90;
            var fechaOperacion = apuntesBancariosAbono.Max(a => a.FechaOperacion);
            var fechaVencimiento = fechaOperacion.AddDays(diasAplazados);
            // Creamos una tarea para ajustar la fecha
            var task = AjustarFechaNoFestiva(fechaVencimiento);

            // Mientras la tarea no esté completada, seguimos ejecutando tareas pendientes del contexto
            while (!task.IsCompleted)
            {
                System.Threading.Thread.Sleep(10); // Evitar que el ciclo sea demasiado agresivo
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { }));
            }
            fechaVencimiento = task.Result;
            diasAplazados = (int)(fechaVencimiento - fechaOperacion).TotalDays;

            var importeDescuadre = apuntesBancariosCargo.Sum(c => c.ImporteMovimiento) + apuntesBancariosAbono.Sum(a => a.ImporteMovimiento);

            //var importeIngresado = apuntesBancariosAbono.Sum(a => a.ImporteMovimiento);
            var importeIntereses = -importeDescuadre; 
            var importeOriginal = -apuntesBancariosCargo.Sum(c => c.ImporteMovimiento);
            var tipoInteres = (importeIntereses / diasAplazados * 360) / importeOriginal; // Aplazamos 90 días y quiero mostrar el interés anual (Euribor 3M + 1,50%)

            if (!_dialogService.ShowConfirmationAnswer("Contabilizar", $"¿Desea contabilizar los intereses de {importeIntereses.ToString("c")} ({tipoInteres.ToString("p")}) y vencimiento {fechaVencimiento.ToShortDateString()}?"))
            {
                return null;
            }

            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = "66200005";
            linea1.Concepto = $"Interés financiación póliza riesgo ({tipoInteres.ToString("p")})";

            // Obtener los últimos 10 caracteres
            string referenciaCompleta = apuntesContabilidad.Max(c => c.Id).ToString();
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
            linea1.Fecha = new DateOnly(fechaOperacion.Year, fechaOperacion.Month, fechaOperacion.Day);
            linea1.Delegacion = "ALG";
            linea1.FormaVenta = "VAR";
            linea1.Departamento = "ADM";
            linea1.CentroCoste = "CA";
            linea1.Debe = importeIntereses;
            lineas.Add(linea1);

            foreach (var abono in apuntesBancariosAbono)
            {
                var linea2 = BancosViewModel.CrearPrecontabilidadDefecto();
                linea2.Diario = "_ConcBanco";
                linea2.Cuenta = banco.CuentaContable;
                linea2.Concepto = $"Financiación póliza riesgo {apuntesContabilidad.Max(c => c.Concepto)}";
                linea2.Documento = ultimos10Caracteres;
                linea2.Fecha = new DateOnly(fechaOperacion.Year, fechaOperacion.Month, fechaOperacion.Day);
                linea2.Delegacion = "ALG";
                linea2.FormaVenta = "VAR";
                linea2.Departamento = "ADM";
                linea2.CentroCoste = "CA";
                linea2.Debe = abono.ImporteMovimiento;
                lineas.Add(linea2);
            }
            
            foreach (var cargo in apuntesBancariosCargo)
            {
                var linea3 = BancosViewModel.CrearPrecontabilidadDefecto();
                linea3.Diario = "_ConcBanco";
                linea3.Cuenta = "52000014";
                linea3.Concepto = $"Financiación póliza riesgo {apuntesContabilidad.Max(c => c.Concepto)}";
                linea3.Documento = ultimos10Caracteres;
                linea3.Fecha = new DateOnly(fechaOperacion.Year, fechaOperacion.Month, fechaOperacion.Day);
                linea3.Delegacion = "ALG";
                linea3.FormaVenta = "VAR";
                linea3.Departamento = "ADM";
                linea3.CentroCoste = "CA";
                linea3.Haber = -cargo.ImporteMovimiento;
                lineas.Add(linea3);

                var linea4 = BancosViewModel.CrearPrecontabilidadDefecto();
                linea4.Asiento = 2;
                linea4.Diario = "_ConcBanco";
                linea4.Cuenta = "52000014";
                linea4.Concepto = $"Financiación póliza riesgo {apuntesContabilidad.Max(c => c.Concepto)}";
                linea4.Documento = ultimos10Caracteres;
                linea4.Fecha = new DateOnly(fechaVencimiento.Year, fechaVencimiento.Month, fechaVencimiento.Day);
                linea4.Delegacion = "ALG";
                linea4.FormaVenta = "VAR";
                linea4.Departamento = "ADM";
                linea4.CentroCoste = "CA";
                linea4.Debe = -cargo.ImporteMovimiento;
                linea4.Contrapartida = banco.CuentaContable;
                lineas.Add(linea4);
            }
            


            ReglaContabilizacionResponse response = new ReglaContabilizacionResponse
            {
                Lineas = lineas
            };

            return response;
        }

        public bool EsContabilizable(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any() || apuntesBancarios.Count() / apuntesContabilidad.Count() != 2)
            {
                return false;
            }
            var apuntesBancariosCargo = apuntesBancarios.Where(b => b.ImporteMovimiento < 0);
            var apuntesBancariosAbono = apuntesBancarios.Where(b => b.ImporteMovimiento > 0);
            //var apunteContabilidad = apuntesContabilidad.Single();

            if (!apuntesBancariosCargo.Any() || !apuntesBancariosAbono.Any() || apuntesBancariosCargo.Sum(b => b.ImporteMovimiento) != apuntesContabilidad.Sum(c => c.Importe))
            {
                return false;
            }

            if (apuntesBancariosAbono.All(b => b.ConceptoComun == "02") &&
                apuntesBancariosAbono.All(b => b.ConceptoPropio == "036") &&
                apuntesBancariosAbono.All(b => b.RegistrosConcepto != null) &&
                apuntesBancariosAbono.All(b => b.RegistrosConcepto.Any()) &&
                apuntesBancariosAbono.All(b => b.RegistrosConcepto[0]?.Concepto2.Trim() == "R2F 0627302000063")
               )
            {
                return true;
            }

            return false;
        }

        private async Task<DateTime> AjustarFechaNoFestiva(DateTime fecha)
        {
            while (await _recursosHumanosService.EsFestivo(fecha, string.Empty).ConfigureAwait(false))
            {
                fecha = fecha.AddDays(-1);
            }
            return fecha;
        }
    }
}
