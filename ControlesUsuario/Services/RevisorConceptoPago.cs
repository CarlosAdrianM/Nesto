using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using System;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>NestoAPI#609: el paso previo a crear o enviar un enlace de pago.</summary>
    public interface IRevisorConceptoPago
    {
        /// <summary>
        /// El concepto con el que seguir: el corregido si la API propone cambios y el usuario los acepta; si
        /// no (los rechaza, no hay cambios, la API falla o no tiene el endpoint), el escrito tal cual.
        /// </summary>
        Task<string> ElegirConcepto(string concepto, string empresa, string cliente);
    }

    /// <summary>
    /// NestoAPI#609: pide la revisión del concepto y, si hay cambios, enseña «¿Quisiste decir…?» con los
    /// cambios resaltados (<see cref="RevisionConceptoDialogViewModel"/>). Nunca cambia el concepto sin que el
    /// usuario lo vea, y nunca impide seguir.
    /// </summary>
    public class RevisorConceptoPago : IRevisorConceptoPago
    {
        private readonly IServicioRevisionConcepto _servicio;
        private readonly IServicioDialogos _dialogos;

        public RevisorConceptoPago(IServicioRevisionConcepto servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
            _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
        }

        public async Task<string> ElegirConcepto(string concepto, string empresa, string cliente)
        {
            if (string.IsNullOrWhiteSpace(concepto))
            {
                return concepto;
            }
            RevisionConcepto revision;
            try
            {
                revision = await _servicio.Revisar(concepto, empresa, cliente);
            }
            catch (Exception)
            {
                return concepto;
            }
            if (revision == null || !revision.HayCambios || string.IsNullOrWhiteSpace(revision.Propuesto)
                || revision.Propuesto == concepto)
            {
                return concepto;
            }

            var parametros = new ParametrosDialogo
            {
                { RevisionConceptoDialogViewModel.PARAMETRO_ORIGINAL, concepto },
                { RevisionConceptoDialogViewModel.PARAMETRO_REVISION, revision }
            };
            ResultadoDialogo resultado = await _dialogos.ShowDialogAsync(RevisionConceptoDialogViewModel.NOMBRE, parametros);
            return resultado?.Result == ResultadoBoton.OK
                && resultado.Parameters.ContainsKey(RevisionConceptoDialogViewModel.PARAMETRO_CONCEPTO)
                ? resultado.Parameters.GetValue<string>(RevisionConceptoDialogViewModel.PARAMETRO_CONCEPTO)
                : concepto;
        }
    }
}
