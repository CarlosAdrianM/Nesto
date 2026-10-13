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

        /// <summary>
        /// NestoAPI#593: el primer móvil español de la cadena de la ficha (9 cifras que empiezan por 6 o 7, con o sin
        /// prefijo 34/+34/0034), solo con las cifras. Null si no hay ninguno (un fijo no vale para WhatsApp).
        /// </summary>
        public static string PrimerMovil(string telefonos)
        {
            foreach (string telefono in TrocearTelefonos(telefonos))
            {
                var cifras = new System.Text.StringBuilder();
                foreach (char caracter in telefono)
                {
                    if (char.IsDigit(caracter))
                    {
                        _ = cifras.Append(caracter);
                    }
                }
                string numero = cifras.ToString();
                if (numero.Length == 13 && numero.StartsWith("0034"))
                {
                    numero = numero.Substring(4);
                }
                else if (numero.Length == 11 && numero.StartsWith("34"))
                {
                    numero = numero.Substring(2);
                }
                if (numero.Length == 9 && (numero[0] == '6' || numero[0] == '7'))
                {
                    return numero;
                }
            }
            return null;
        }

        /// <summary>NestoAPI#593: el enlace que abre un chat de WhatsApp con ese móvil (de <see cref="PrimerMovil"/>). Null sin móvil.</summary>
        public static string EnlaceWhatsApp(string movil)
            => string.IsNullOrWhiteSpace(movil) ? null : "https://wa.me/34" + movil.Trim();

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
