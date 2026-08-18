using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Punto único para la lógica de teléfonos de toda la solución (Nesto#444): trocear la
    /// cadena de la ficha del cliente, y en el futuro el formateo/normalización que hoy
    /// vive repartido (p. ej. en CrearCliente). Si hay que hacer algo con teléfonos, se
    /// hace aquí.
    /// </summary>
    public static class TelefonoHelper
    {
        private const int DIGITOS_MINIMOS_TELEFONO_COMPLETO = 9;

        /// <summary>
        /// Trocea la cadena de teléfonos de la ficha en teléfonos individuales.
        /// Separadores duros: / \ ; , — el espacio solo separa cuando TODOS los trozos
        /// tienen 9+ dígitos, porque también se usa dentro de un mismo número
        /// ("91 698 57 05"). Formatos calibrados contra datos reales de la tabla Clientes;
        /// lo que no se reconoce se devuelve tal cual (no inventar).
        /// </summary>
        public static IReadOnlyList<string> TrocearTelefonos(string telefonos)
        {
            if (string.IsNullOrWhiteSpace(telefonos))
            {
                return Array.Empty<string>();
            }

            var resultado = new List<string>();
            string[] segmentos = telefonos.Split(new[] { '/', '\\', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string segmento in segmentos)
            {
                string limpio = segmento.Trim();
                if (limpio.Length == 0)
                {
                    continue;
                }
                string[] trozos = limpio.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (trozos.Length > 1 && TodosSonTelefonosCompletos(trozos))
                {
                    resultado.AddRange(trozos);
                }
                else
                {
                    resultado.Add(limpio);
                }
            }
            return resultado;
        }

        private static bool TodosSonTelefonosCompletos(IEnumerable<string> trozos)
        {
            foreach (string trozo in trozos)
            {
                int digitos = 0;
                foreach (char caracter in trozo)
                {
                    if (char.IsDigit(caracter))
                    {
                        digitos++;
                    }
                }
                if (digitos < DIGITOS_MINIMOS_TELEFONO_COMPLETO)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
