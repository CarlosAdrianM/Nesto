using Prism.Services.Dialogs;
using System;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    public static class DialogServiceExtensions
    {
        public static void ShowNotification(this IDialogService dialogService, string message)
        {
            DialogParameters p = new()
            {
                { "message", message }
            };
            dialogService.ShowDialog("NotificationDialog", p, null);
        }

        public static void ShowNotification(this IDialogService dialogService, string title, string message)
        {
            DialogParameters p = new()
            {
                { "title", title },
                { "message", message }
            };
            dialogService.ShowDialog("NotificationDialog", p, null);
        }

        public static void ShowError(this IDialogService dialogService, string message)
        {
            // Carlos 20/11/24: Extraer mensaje limpio en caso de que contenga JSON
            string cleanMessage = ExtraerMensajeLimpio(message);

            DialogParameters p = new()
            {
                { "title", "¡Error!" },
                { "message", cleanMessage }
            };
            dialogService.ShowDialog("NotificationDialog", p, null);
        }

        /// <summary>
        /// Extrae un mensaje de error limpio, eliminando JSON si está presente.
        /// Carlos 20/11/24: Soluciona problema de mostrar JSON completo en errores de APIs externas.
        ///
        /// Formato esperado del JSON (según GlobalExceptionFilter de NestoAPI):
        /// {
        ///   "error": {
        ///     "code": "ERROR_CODE",
        ///     "message": "Mensaje de error legible",
        ///     "details": { ... }
        ///   }
        /// }
        /// </summary>
        private static string ExtraerMensajeLimpio(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return message;

            // Si el mensaje contiene JSON, intentar parsearlo para extraer error.message
            if (message.Contains('{'))
            {
                try
                {
                    // Buscar el inicio del JSON
                    int indexJson = message.IndexOf('{');
                    string jsonPart = indexJson == 0 ? message : message.Substring(indexJson);

                    // Intentar parsear el JSON
                    var errorResponse = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(jsonPart);

                    if (errorResponse != null && errorResponse.ContainsKey("error"))
                    {
                        // Obtener el objeto "error"
                        var errorObj = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(errorResponse["error"].ToString());

                        if (errorObj != null && errorObj.ContainsKey("message"))
                        {
                            // Extraer el mensaje limpio desde error.message
                            string errorMessage = errorObj["message"].ToString();
                            return string.IsNullOrWhiteSpace(errorMessage) ? message : errorMessage;
                        }
                    }
                }
                catch
                {
                    // Si falla el parseo JSON, intentar extraer texto antes del JSON
                    int indexJson = message.IndexOf('{');
                    if (indexJson > 0)
                    {
                        return message.Substring(0, indexJson).Trim('\r', '\n', ' ', '.');
                    }
                }
            }

            return message;
        }

        public static void ShowConfirmation(this IDialogService dialogService, string message, Action<IDialogResult> callBack)
        {
            DialogParameters p = new()
            {
                { "message", message }
            };
            dialogService.ShowDialog("ConfirmationDialog", p, callBack);
        }

        public static void ShowConfirmation(this IDialogService dialogService, string title, string message, Action<IDialogResult> callBack)
        {
            DialogParameters p = new()
            {
                { "title", title },
                { "message", message }
            };
            dialogService.ShowDialog("ConfirmationDialog", p, callBack);
        }

        public static bool ShowConfirmationAnswer(this IDialogService dialogService, string title, string message)
        {
            bool confirmed = false;

            DialogParameters p = new()
            {
                { "title", title },
                { "message", message }
            };
            dialogService.ShowDialog("ConfirmationDialog", p, r => { confirmed = r.Result == ButtonResult.OK; });

            return confirmed;
        }

        public static Task<bool> ShowConfirmationAsync(this IDialogService dialogService, string message)
        {
            return dialogService.ShowConfirmationAsync("Confirmación", message);
        }

        public static Task<bool> ShowConfirmationAsync(this IDialogService dialogService, string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();

            var p = new DialogParameters
        {
            { "title", title },
            { "message", message }
        };

            dialogService.ShowDialog("ConfirmationDialog", p, r =>
            {
                tcs.SetResult(r.Result == ButtonResult.OK);
            });

            return tcs.Task;
        }

        public static void ShowInputAmount(this IDialogService dialogService, string message, Action<IDialogResult> callback)
        {
            DialogParameters p = new()
            {
                { "message", message }
            };
            dialogService.ShowDialog("InputAmountDialog", p, callback);
        }

        public static void ShowInputAmount(this IDialogService dialogService, string title, string message, Action<IDialogResult> callback)
        {
            DialogParameters p = new()
            {
                { "title", title },
                { "message", message }
            };
            dialogService.ShowDialog("InputAmountDialog", p, callback);
        }

        public static void ShowInputAmount(this IDialogService dialogService, string title, string message, string defaultAmount, Action<IDialogResult> callback)
        {
            DialogParameters p = new()
            {
                { "title", title },
                { "message", message },
                { "defaultAmount", defaultAmount }
            };
            dialogService.ShowDialog("InputAmountDialog", p, callback);
        }

        // Método de conveniencia para obtener directamente el valor decimal
        public static decimal? GetAmount(this IDialogService dialogService, string title, string message)
        {
            decimal? result = null;

            DialogParameters p = new()
            {
                { "title", title },
                { "message", message }
            };

            dialogService.ShowDialog("InputAmountDialog", p, r =>
            {
                if (r.Result == ButtonResult.OK && r.Parameters.ContainsKey("amount"))
                {
                    result = r.Parameters.GetValue<decimal>("amount");
                }
            });

            return result;
        }

        public static Task<IDialogResult> ShowDialogAsync(
            this IDialogService dialogService,
            string name,
            IDialogParameters parameters = null)
        {
            var tcs = new TaskCompletionSource<IDialogResult>();

            dialogService.ShowDialog(name, parameters, tcs.SetResult);

            return tcs.Task;
        }

        public static void ShowInputText(this IDialogService dialogService, string message, Action<IDialogResult> callback)
        {
            DialogParameters p = new()
            {
                { "message", message }
            };
            dialogService.ShowDialog("InputTextDialog", p, callback);
        }

        public static void ShowInputText(this IDialogService dialogService, string title, string message, Action<IDialogResult> callback)
        {
            DialogParameters p = new()
            {
                { "title", title },
                { "message", message }
            };
            dialogService.ShowDialog("InputTextDialog", p, callback);
        }

        public static void ShowInputText(this IDialogService dialogService, string title, string message, string defaultText, Action<IDialogResult> callback)
        {
            DialogParameters p = new()
            {
                { "title", title },
                { "message", message },
                { "defaultText", defaultText }
            };
            dialogService.ShowDialog("InputTextDialog", p, callback);
        }

        public static string GetText(this IDialogService dialogService, string title, string message)
        {
            string result = null;

            DialogParameters p = new()
            {
                { "title", title },
                { "message", message }
            };

            dialogService.ShowDialog("InputTextDialog", p, r =>
            {
                if (r.Result == ButtonResult.OK && r.Parameters.ContainsKey("text"))
                {
                    result = r.Parameters.GetValue<string>("text");
                }
            });

            return result;
        }

        public static string GetText(this IDialogService dialogService, string title, string message, string defaultText)
        {
            string result = null;

            DialogParameters p = new()
            {
                { "title", title },
                { "message", message },
                { "defaultText", defaultText }
            };

            dialogService.ShowDialog("InputTextDialog", p, r =>
            {
                if (r.Result == ButtonResult.OK && r.Parameters.ContainsKey("text"))
                {
                    result = r.Parameters.GetValue<string>("text");
                }
            });

            return result;
        }
    }
}
