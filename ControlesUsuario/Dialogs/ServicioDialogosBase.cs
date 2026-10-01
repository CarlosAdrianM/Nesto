using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3): núcleo común de <see cref="IServicioDialogos"/>. Los diálogos
    /// genéricos (aviso, error, confirmación, importe, texto) son siempre un <see cref="ShowDialog"/>
    /// de NotificationDialog, ConfirmationDialog, InputAmountDialog o InputTextDialog con unos
    /// parámetros fijos; esa traducción vive aquí UNA vez (antes estaba en las extensiones del
    /// IDialogService de Prism). Cada implementación solo dice CÓMO se abre un diálogo:
    /// <see cref="ServicioDialogosPrism"/> con el IDialogService de Prism y
    /// <see cref="ServicioDialogosNesto"/> con una ventana propia.
    /// </summary>
    public abstract class ServicioDialogosBase : IServicioDialogos
    {
        public const string NotificationDialog = "NotificationDialog";
        public const string ConfirmationDialog = "ConfirmationDialog";
        public const string InputAmountDialog = "InputAmountDialog";
        public const string InputTextDialog = "InputTextDialog";
        public const string InputDateDialog = "InputDateDialog";

        public DateTime? GetDate(string title, string message, DateTime? defaultDate)
        {
            DateTime? fecha = null;
            var parametros = new ParametrosDialogo { { "title", title }, { "message", message } };
            if (defaultDate.HasValue)
            {
                parametros.Add("defaultDate", defaultDate.Value);
            }
            ShowDialog(InputDateDialog, parametros, r =>
            {
                if (r.Result == ResultadoBoton.OK && r.Parameters.ContainsKey("date"))
                {
                    fecha = r.Parameters.GetValue<DateTime>("date");
                }
            });
            return fecha;
        }

        /// <summary>Abre modal el diálogo registrado con ese nombre.</summary>
        public abstract void ShowDialog(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback);

        /// <summary>Abre no modal el diálogo registrado con ese nombre, desde cualquier hilo.</summary>
        public abstract void Show(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback);

        public virtual Task<ResultadoDialogo> ShowDialogAsync(string name, ParametrosDialogo parameters = null)
        {
            var tcs = new TaskCompletionSource<ResultadoDialogo>();
            ShowDialog(name, parameters, tcs.SetResult);
            return tcs.Task;
        }

        public void ShowNotification(string message)
            => ShowDialog(NotificationDialog, new ParametrosDialogo { { "message", message } }, null);

        public void ShowNotification(string title, string message)
            => ShowDialog(NotificationDialog, new ParametrosDialogo { { "title", title }, { "message", message } }, null);

        public void ShowError(string message)
            => ShowDialog(NotificationDialog, new ParametrosDialogo { { "title", "¡Error!" }, { "message", ExtraerMensajeLimpio(message) } }, null);

        public void ShowConfirmation(string message, Action<ResultadoDialogo> callBack)
            => ShowDialog(ConfirmationDialog, new ParametrosDialogo { { "message", message } }, callBack);

        public void ShowConfirmation(string title, string message, Action<ResultadoDialogo> callBack)
            => ShowDialog(ConfirmationDialog, new ParametrosDialogo { { "title", title }, { "message", message } }, callBack);

        public bool ShowConfirmationAnswer(string title, string message)
        {
            bool confirmado = false;
            ShowDialog(ConfirmationDialog, new ParametrosDialogo { { "title", title }, { "message", message } },
                r => confirmado = r.Result == ResultadoBoton.OK);
            return confirmado;
        }

        public Task<bool> ShowConfirmationAsync(string message) => ShowConfirmationAsync("Confirmación", message);

        public Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();
            ShowDialog(ConfirmationDialog, new ParametrosDialogo { { "title", title }, { "message", message } },
                r => tcs.SetResult(r.Result == ResultadoBoton.OK));
            return tcs.Task;
        }

        public void ShowInputAmount(string message, Action<ResultadoDialogo> callback)
            => ShowDialog(InputAmountDialog, new ParametrosDialogo { { "message", message } }, callback);

        public void ShowInputAmount(string title, string message, Action<ResultadoDialogo> callback)
            => ShowDialog(InputAmountDialog, new ParametrosDialogo { { "title", title }, { "message", message } }, callback);

        public void ShowInputAmount(string title, string message, string defaultAmount, Action<ResultadoDialogo> callback)
            => ShowDialog(InputAmountDialog, new ParametrosDialogo { { "title", title }, { "message", message }, { "defaultAmount", defaultAmount } }, callback);

        public decimal? GetAmount(string title, string message)
        {
            decimal? importe = null;
            ShowDialog(InputAmountDialog, new ParametrosDialogo { { "title", title }, { "message", message } }, r =>
            {
                if (r.Result == ResultadoBoton.OK && r.Parameters.ContainsKey("amount"))
                {
                    importe = r.Parameters.GetValue<decimal>("amount");
                }
            });
            return importe;
        }

        public void ShowInputText(string message, Action<ResultadoDialogo> callback)
            => ShowDialog(InputTextDialog, new ParametrosDialogo { { "message", message } }, callback);

        public void ShowInputText(string title, string message, Action<ResultadoDialogo> callback)
            => ShowDialog(InputTextDialog, new ParametrosDialogo { { "title", title }, { "message", message } }, callback);

        public void ShowInputText(string title, string message, string defaultText, Action<ResultadoDialogo> callback)
            => ShowDialog(InputTextDialog, new ParametrosDialogo { { "title", title }, { "message", message }, { "defaultText", defaultText } }, callback);

        public string GetText(string title, string message)
            => LeerTexto(new ParametrosDialogo { { "title", title }, { "message", message } });

        public string GetText(string title, string message, string defaultText)
            => LeerTexto(new ParametrosDialogo { { "title", title }, { "message", message }, { "defaultText", defaultText } });

        private string LeerTexto(ParametrosDialogo parametros)
        {
            string texto = null;
            ShowDialog(InputTextDialog, parametros, r =>
            {
                if (r.Result == ResultadoBoton.OK && r.Parameters.ContainsKey("text"))
                {
                    texto = r.Parameters.GetValue<string>("text");
                }
            });
            return texto;
        }

        /// <summary>
        /// Extrae un mensaje de error limpio, eliminando JSON si está presente.
        /// Carlos 20/11/24: Soluciona problema de mostrar JSON completo en errores de APIs externas.
        ///
        /// Formato esperado del JSON (según GlobalExceptionFilter de NestoAPI):
        /// { "error": { "code": "ERROR_CODE", "message": "Mensaje de error legible", "details": { ... } } }
        /// </summary>
        internal static string ExtraerMensajeLimpio(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || !message.Contains('{'))
            {
                return message;
            }

            int indexJson = message.IndexOf('{');
            try
            {
                string jsonPart = indexJson == 0 ? message : message.Substring(indexJson);
                var errorResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonPart);
                if (errorResponse != null && errorResponse.ContainsKey("error"))
                {
                    var errorObj = JsonSerializer.Deserialize<Dictionary<string, object>>(errorResponse["error"].ToString());
                    if (errorObj != null && errorObj.ContainsKey("message"))
                    {
                        string errorMessage = errorObj["message"].ToString();
                        return string.IsNullOrWhiteSpace(errorMessage) ? message : errorMessage;
                    }
                }
            }
            catch
            {
                // Si falla el parseo JSON, quedarse con el texto de antes del JSON
                if (indexJson > 0)
                {
                    return message.Substring(0, indexJson).Trim('\r', '\n', ' ', '.');
                }
            }

            return message;
        }
    }
}
