using System;
using System.Data.Entity.Validation;

namespace Nesto.Infrastructure.Shared
{
    public static class DbValidationErrorHelper
    {
        public static string ExtraerMensajeError(Exception ex)
        {
            if (ex is DbEntityValidationException dbEx)
            {
                string mensajeError = dbEx.Message;
                foreach (DbEntityValidationResult errorValidacion in dbEx.EntityValidationErrors)
                {
                    foreach (DbValidationError textoError in errorValidacion.ValidationErrors)
                    {
                        mensajeError += Environment.NewLine + textoError.ErrorMessage;
                    }
                }
                return mensajeError;
            }

            return ex.InnerException?.Message ?? ex.Message;
        }
    }
}
