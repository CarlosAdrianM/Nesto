using System;

namespace ControlesUsuario.Behaviors
{
    /// <summary>
    /// Helper para conversión y validación de cuentas contables.
    /// Permite usar formato abreviado con punto (.) que se expande a la longitud completa del plan contable.
    /// </summary>
    /// <remarks>
    /// Ejemplos de conversión (con LongitudPlanContable = 8):
    /// - "572.13" → "57200013"
    /// - "57." → "57000000"
    /// - "4300.1" → "43000001"
    /// - "57200013" → "57200013" (sin cambios si ya tiene la longitud correcta)
    /// </remarks>
    public static class CuentaContableHelper
    {
        /// <summary>
        /// Longitud del plan contable en dígitos.
        /// </summary>
        public const int LongitudPlanContable = 8;

        /// <summary>
        /// Convierte una cuenta en formato abreviado a formato completo.
        /// El punto (.) se sustituye por ceros hasta alcanzar la longitud del plan contable.
        /// </summary>
        /// <param name="cuentaAbreviada">Cuenta en formato abreviado (ej: "572.13", "57.", "4300.1")</param>
        /// <returns>Cuenta en formato completo con la longitud del plan contable</returns>
        /// <exception cref="ArgumentException">Si la cuenta resultante excede la longitud del plan contable</exception>
        public static string ExpandirCuenta(string cuentaAbreviada)
        {
            if (string.IsNullOrWhiteSpace(cuentaAbreviada))
            {
                return string.Empty;
            }

            var cuenta = cuentaAbreviada.Trim();

            // Si no contiene punto, verificar longitud y rellenar con ceros si es necesario
            if (!cuenta.Contains("."))
            {
                if (cuenta.Length > LongitudPlanContable)
                {
                    throw new ArgumentException(
                        $"La cuenta '{cuenta}' excede la longitud máxima del plan contable ({LongitudPlanContable} dígitos).",
                        nameof(cuentaAbreviada));
                }
                // Si ya tiene la longitud correcta, devolver tal cual
                if (cuenta.Length == LongitudPlanContable)
                {
                    return cuenta;
                }
                // Si es más corta, rellenar con ceros a la derecha
                return cuenta.PadRight(LongitudPlanContable, '0');
            }

            // Separar por el punto
            var partes = cuenta.Split('.');
            if (partes.Length != 2)
            {
                throw new ArgumentException(
                    $"La cuenta '{cuenta}' tiene un formato inválido. Solo se permite un punto.",
                    nameof(cuentaAbreviada));
            }

            var parteIzquierda = partes[0];
            var parteDerecha = partes[1];

            // Calcular cuántos ceros necesitamos en medio
            var longitudTotal = parteIzquierda.Length + parteDerecha.Length;
            if (longitudTotal > LongitudPlanContable)
            {
                throw new ArgumentException(
                    $"La cuenta '{cuenta}' excede la longitud máxima del plan contable ({LongitudPlanContable} dígitos).",
                    nameof(cuentaAbreviada));
            }

            var cerosNecesarios = LongitudPlanContable - longitudTotal;
            var ceros = new string('0', cerosNecesarios);

            return parteIzquierda + ceros + parteDerecha;
        }

        /// <summary>
        /// Intenta convertir una cuenta en formato abreviado a formato completo.
        /// </summary>
        /// <param name="cuentaAbreviada">Cuenta en formato abreviado</param>
        /// <param name="cuentaExpandida">Cuenta en formato completo (si la conversión tuvo éxito)</param>
        /// <returns>True si la conversión fue exitosa, False en caso contrario</returns>
        public static bool TryExpandirCuenta(string cuentaAbreviada, out string cuentaExpandida)
        {
            try
            {
                cuentaExpandida = ExpandirCuenta(cuentaAbreviada);
                return true;
            }
            catch (ArgumentException)
            {
                cuentaExpandida = null;
                return false;
            }
        }

        /// <summary>
        /// Verifica si una cadena tiene el formato correcto para una cuenta contable.
        /// </summary>
        /// <param name="cuenta">Cuenta a validar</param>
        /// <returns>True si la cuenta tiene exactamente la longitud del plan contable y solo contiene dígitos</returns>
        public static bool EsCuentaValida(string cuenta)
        {
            if (string.IsNullOrWhiteSpace(cuenta))
            {
                return false;
            }

            var cuentaTrimmed = cuenta.Trim();
            if (cuentaTrimmed.Length != LongitudPlanContable)
            {
                return false;
            }

            // Verificar que solo contiene dígitos
            foreach (var c in cuentaTrimmed)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Convierte una cuenta a formato abreviado si es posible (para mostrar al usuario).
        /// </summary>
        /// <param name="cuentaCompleta">Cuenta en formato completo (8 dígitos)</param>
        /// <returns>Cuenta en formato abreviado si tiene ceros en medio, o la cuenta original si no</returns>
        public static string AbreviarCuenta(string cuentaCompleta)
        {
            if (string.IsNullOrWhiteSpace(cuentaCompleta))
            {
                return string.Empty;
            }

            var cuenta = cuentaCompleta.Trim();
            if (cuenta.Length != LongitudPlanContable)
            {
                return cuenta;
            }

            // Encontrar el primer y último índice de ceros consecutivos
            int primerCero = -1;
            int ultimoCero = -1;

            for (int i = 0; i < cuenta.Length; i++)
            {
                if (cuenta[i] == '0')
                {
                    if (primerCero == -1)
                    {
                        primerCero = i;
                    }
                    ultimoCero = i;
                }
                else if (primerCero != -1)
                {
                    // Encontramos dígitos después de los ceros
                    break;
                }
            }

            // Si hay ceros en medio, abreviar
            if (primerCero > 0 && ultimoCero < cuenta.Length - 1)
            {
                var parteIzquierda = cuenta.Substring(0, primerCero);
                var parteDerecha = cuenta.Substring(ultimoCero + 1);
                if (!string.IsNullOrEmpty(parteDerecha))
                {
                    return parteIzquierda + "." + parteDerecha;
                }
            }

            // Si termina en ceros, mostrar solo la parte significativa con punto
            if (ultimoCero == cuenta.Length - 1 && primerCero > 0)
            {
                return cuenta.Substring(0, primerCero) + ".";
            }

            return cuenta;
        }
    }
}
