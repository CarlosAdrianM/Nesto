﻿using Prism.Services.Dialogs;
using System;

namespace ControlesUsuario.Dialogs
{
    public static class DialogServiceExtensions
    {
        public static void ShowNotification(this IDialogService dialogService, string message)
        {
            DialogParameters p = new DialogParameters
            {
                { "message", message }
            };
            dialogService.ShowDialog("NotificationDialog", p, null);
        }

        public static void ShowNotification(this IDialogService dialogService, string title, string message)
        {
            DialogParameters p = new DialogParameters
            {
                { "title", title },
                { "message", message }
            };
            dialogService.ShowDialog("NotificationDialog", p, null);
        }

        public static void ShowError(this IDialogService dialogService, string message)
        {
            DialogParameters p = new DialogParameters
            {
                { "title", "¡Error!" },
                { "message", message }
            };
            dialogService.ShowDialog("NotificationDialog", p, null);
        }

        public static void ShowConfirmation(this IDialogService dialogService, string message, Action<IDialogResult> callBack)
        {
            DialogParameters p = new DialogParameters
            {
                { "message", message }
            };
            dialogService.ShowDialog("ConfirmationDialog", p, callBack);
        }

        public static void ShowConfirmation(this IDialogService dialogService, string title, string message, Action<IDialogResult> callBack)
        {
            DialogParameters p = new DialogParameters
            {
                { "title", title },
                { "message", message }
            };
            dialogService.ShowDialog("ConfirmationDialog", p, callBack);
        }

        public static bool ShowConfirmationAnswer(this IDialogService dialogService, string title, string message)
        {
            bool confirmed = false;

            DialogParameters p = new DialogParameters
            {
                { "title", title },
                { "message", message }
            };
            dialogService.ShowDialog("ConfirmationDialog", p, r => { confirmed = r.Result == ButtonResult.OK; });

            return confirmed;
        }


        public static void ShowInputAmount(this IDialogService dialogService, string message, Action<IDialogResult> callback)
        {
            DialogParameters p = new DialogParameters
            {
                { "message", message }
            };
            dialogService.ShowDialog("InputAmountDialog", p, callback);
        }

        public static void ShowInputAmount(this IDialogService dialogService, string title, string message, Action<IDialogResult> callback)
        {
            DialogParameters p = new DialogParameters
            {
                { "title", title },
                { "message", message }
            };
            dialogService.ShowDialog("InputAmountDialog", p, callback);
        }

        public static void ShowInputAmount(this IDialogService dialogService, string title, string message, string defaultAmount, Action<IDialogResult> callback)
        {
            DialogParameters p = new DialogParameters
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

            DialogParameters p = new DialogParameters
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
    }
}
