using System;
using System.Threading;
using System.Windows;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// <see cref="IServicioPortapapeles"/> con el portapapeles de Windows. Si otro programa lo tiene abierto (CLIPBRD_E_CANT_OPEN),
    /// lo reintenta un par de veces antes de rendirse.
    /// </summary>
    public class ServicioPortapapeles : IServicioPortapapeles
    {
        private const int INTENTOS = 3;

        public bool Copiar(string texto)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return false;
            }
            for (int intento = 1; intento <= INTENTOS; intento++)
            {
                try
                {
                    Clipboard.SetDataObject(texto, true);
                    return true;
                }
                catch (Exception) when (intento < INTENTOS)
                {
                    Thread.Sleep(50);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }
    }
}
