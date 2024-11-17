using ControlesUsuario.Dialogs;
using Nesto.Modulos.Cajas.ViewModels;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaInteresesAplazamientoConfirming : IReglaContabilizacion
    {
        private readonly IDialogService _dialogService;

        public ReglaInteresesAplazamientoConfirming(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public string Nombre => "Intereses aplazamiento confirming";

        public ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco)
        {
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return new ReglaContabilizacionResponse();
            }
            var apunteBancario = apuntesBancarios.First();
            var apunteContabilidad = apuntesContabilidad.First();
            var importeDescuadre = apuntesBancarios.Sum(b => b.ImporteMovimiento) - apuntesContabilidad.Sum(c => c.Importe);

            var importeIngresado = apunteBancario.ImporteMovimiento;
            var importeIntereses = -importeDescuadre; ;
            var importeOriginal = importeIngresado + importeIntereses;
            var tipoInteres = -(importeIntereses * 12) / importeOriginal; // Aplazamos un mes y quiero mostrar el interés anual

            if (!_dialogService.ShowConfirmationAnswer("Contabilizar", $"¿Desea contabilizar los intereses de {importeIntereses.ToString("c")} ({tipoInteres.ToString("p")})?"))
            {
                return null;
            }

            var lineas = new List<PreContabilidadDTO>();
            var linea1 = BancosViewModel.CrearPrecontabilidadDefecto();
            linea1.Diario = "_ConcBanco";
            linea1.Cuenta = "62600003";
            linea1.Concepto = $"Interés aplazamiento confirming ({tipoInteres.ToString("p")})";

            // Obtener los últimos 10 caracteres
            string referenciaCompleta = apunteBancario.RegistrosConcepto[1].Concepto.Trim();
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
            linea1.Debe = importeIntereses;
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
            if (apuntesBancarios is null || apuntesContabilidad is null || !apuntesBancarios.Any() || !apuntesContabilidad.Any())
            {
                return false;
            }
            var apunteBancario = apuntesBancarios.First();
            var apunteContabilidad = apuntesContabilidad.First();

            if (apunteContabilidad.Documento == "APLAZO_CNF" &&
                apunteBancario.ConceptoComun == "99" &&
                apunteBancario.ConceptoPropio == "079" &&
                apunteBancario.RegistrosConcepto != null &&
                apunteBancario.RegistrosConcepto.Any() &&
                (
                    apunteBancario.RegistrosConcepto[0]?.Concepto2.Trim() == "CONF:CARGO FACTURAS"
                ))
            {
                return true;
            }

            return false;
        }
    }
}
