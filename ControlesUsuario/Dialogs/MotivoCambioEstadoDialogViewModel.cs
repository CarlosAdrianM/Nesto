using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System.Collections.Generic;
using System.Linq;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#558 (NestoAPI#716, 2.2): al cambiar el estado de un cliente a una baja (o al dejar de visitarlo), pide el
    /// motivo (desplegable con el catálogo <see cref="MotivoCambioEstadoCliente"/>) y un comentario (200 caracteres).
    /// Los dos son opcionales: «Aceptar» devuelve OK con lo que haya (puede ser nada) en <see cref="PARAMETRO_MOTIVO"/>
    /// y <see cref="PARAMETRO_COMENTARIO"/>; «Sin motivo» (o cerrar) devuelve Cancel. Quien lo abre nunca bloquea el
    /// guardado por esto: con Cancel manda el cambio sin motivo, como hasta ahora.
    /// </summary>
    public class MotivoCambioEstadoDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "MotivoCambioEstadoDialog";
        public const string PARAMETRO_TITULO = "title";
        public const string PARAMETRO_MENSAJE = "message";
        public const string PARAMETRO_MOTIVO = "motivo";
        public const string PARAMETRO_COMENTARIO = "comentario";

        private string _message;
        private string _motivo;
        private string _comentario;

        public MotivoCambioEstadoDialogViewModel()
        {
            Title = "Motivo del cambio de estado";
            Message = "Si lo sabe, indique por qué cambia el estado del cliente. Puede dejarlo en blanco.";
            AceptarCommand = new RelayCommand(Aceptar);
            SinMotivoCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>El catálogo, en orden, con una primera opción vacía para «ninguno».</summary>
        public IReadOnlyList<OpcionCatalogo> Motivos { get; } =
            new[] { new OpcionCatalogo(null, "(sin motivo)") }.Concat(MotivoCambioEstadoCliente.Opciones).ToList();

        /// <summary>El código elegido (SelectedValue de la combo), o null.</summary>
        public string Motivo
        {
            get => _motivo;
            set => SetProperty(ref _motivo, value);
        }

        public string Comentario
        {
            get => _comentario;
            set => SetProperty(ref _comentario, value);
        }

        public int LongitudComentario => MotivoCambioEstadoCliente.LONGITUD_COMENTARIO;

        public RelayCommand AceptarCommand { get; }

        /// <summary>«Sin motivo»: se cierra sin nada; el cambio de estado sigue adelante igual.</summary>
        public RelayCommand SinMotivoCommand { get; }

        private void Aceptar()
        {
            var respuesta = new MotivoCambioEstado(Motivo, Comentario);
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo
            {
                { PARAMETRO_MOTIVO, respuesta.Motivo },
                { PARAMETRO_COMENTARIO, respuesta.Comentario }
            }));
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            if (parameters.ContainsKey(PARAMETRO_TITULO))
            {
                Title = parameters.GetValue<string>(PARAMETRO_TITULO);
            }
            if (parameters.ContainsKey(PARAMETRO_MENSAJE))
            {
                Message = parameters.GetValue<string>(PARAMETRO_MENSAJE);
            }
        }
    }

    /// <summary>
    /// Nesto#558: la pregunta del motivo, para que la ficha del cliente y el rapport (dejar de visitar) la hagan igual.
    /// Devuelve lo contestado, o null si la persona dice «Sin motivo», cierra, o no rellena nada: en todos esos casos
    /// no se manda nada a la API. Nunca lanza: si el diálogo no está registrado, también devuelve null.
    /// </summary>
    public static class PeticionMotivoCambioEstado
    {
        public static MotivoCambioEstado Pedir(IServicioDialogos dialogos, string titulo, string mensaje)
        {
            if (dialogos == null)
            {
                return null;
            }
            MotivoCambioEstado respuesta = null;
            var parametros = new ParametrosDialogo
            {
                { MotivoCambioEstadoDialogViewModel.PARAMETRO_TITULO, titulo },
                { MotivoCambioEstadoDialogViewModel.PARAMETRO_MENSAJE, mensaje }
            };
            try
            {
                dialogos.ShowDialog(MotivoCambioEstadoDialogViewModel.NOMBRE, parametros, r =>
                {
                    if (r == null || r.Result != ResultadoBoton.OK)
                    {
                        return;
                    }
                    var contestado = new MotivoCambioEstado(
                        r.Parameters.GetValue<string>(MotivoCambioEstadoDialogViewModel.PARAMETRO_MOTIVO),
                        r.Parameters.GetValue<string>(MotivoCambioEstadoDialogViewModel.PARAMETRO_COMENTARIO));
                    respuesta = contestado.EstaVacio ? null : contestado;
                });
            }
            catch
            {
                // Un diálogo sin registrar (tests, composición vieja) no puede impedir el cambio de estado.
                return null;
            }
            return respuesta;
        }
    }
}
