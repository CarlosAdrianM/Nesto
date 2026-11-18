using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario
{
    /// <summary>
    /// Tamaño de letra del nombre y la dirección del cliente que pinta el selector de clientes debajo de la caja de búsqueda
    /// (Carlos 18/11/26, continuación de la sugerencia 1331). Tres tamaños cerrados, no un número libre, para que el alto
    /// del control cambie poco y de forma previsible. Normal es el de siempre (y el más pequeño).
    /// </summary>
    public enum TamanoLetraSelectorCliente
    {
        Normal,
        Grande,
        MuyGrande
    }

    /// <summary>
    /// LA REGLA (pura) del tamaño de letra del selector de clientes: qué se guarda en el parámetro de usuario
    /// <see cref="Parametros.Claves.TamanoLetraSelectorCliente"/>, cómo se lee y cuántos puntos es cada tamaño.
    /// </summary>
    public static class ReglaTamanoLetraSelectorCliente
    {
        /// <summary>Los puntos de hoy (FontSize del nombre y de la dirección en el selector). No cambiar: Normal = como siempre.</summary>
        public const double PUNTOS_NORMAL = 10;
        public const double PUNTOS_GRANDE = 13;
        public const double PUNTOS_MUY_GRANDE = 16;

        public const string VALOR_NORMAL = "Normal";
        public const string VALOR_GRANDE = "Grande";
        public const string VALOR_MUY_GRANDE = "MuyGrande";

        /// <summary>Del valor del parámetro al tamaño. Sin valor o con uno que no se entiende, Normal.</summary>
        public static TamanoLetraSelectorCliente DesdeParametro(string valor)
        {
            string limpio = (valor ?? string.Empty).Replace(" ", string.Empty).Trim();
            if (string.Equals(limpio, VALOR_GRANDE, StringComparison.OrdinalIgnoreCase))
            {
                return TamanoLetraSelectorCliente.Grande;
            }
            if (string.Equals(limpio, VALOR_MUY_GRANDE, StringComparison.OrdinalIgnoreCase))
            {
                return TamanoLetraSelectorCliente.MuyGrande;
            }
            return TamanoLetraSelectorCliente.Normal;
        }

        /// <summary>Del tamaño al valor que se guarda en el parámetro.</summary>
        public static string AParametro(TamanoLetraSelectorCliente tamano) => tamano switch
        {
            TamanoLetraSelectorCliente.Grande => VALOR_GRANDE,
            TamanoLetraSelectorCliente.MuyGrande => VALOR_MUY_GRANDE,
            _ => VALOR_NORMAL
        };

        /// <summary>Los puntos del nombre y de la dirección (los dos llevan el mismo, como hoy).</summary>
        public static double Puntos(TamanoLetraSelectorCliente tamano) => tamano switch
        {
            TamanoLetraSelectorCliente.Grande => PUNTOS_GRANDE,
            TamanoLetraSelectorCliente.MuyGrande => PUNTOS_MUY_GRANDE,
            _ => PUNTOS_NORMAL
        };

        /// <summary>El texto de cada tamaño en la ruedita del selector.</summary>
        public static string Descripcion(TamanoLetraSelectorCliente tamano) => tamano switch
        {
            TamanoLetraSelectorCliente.Grande => "Grande",
            TamanoLetraSelectorCliente.MuyGrande => "Muy grande",
            _ => "Normal"
        };

        public static IReadOnlyList<TamanoLetraSelectorCliente> Todos { get; } = new[]
        {
            TamanoLetraSelectorCliente.Normal,
            TamanoLetraSelectorCliente.Grande,
            TamanoLetraSelectorCliente.MuyGrande
        };
    }

    /// <summary>
    /// La preferencia del usuario para TODOS los selectores de clientes de la sesión. El arranque de Nesto la lee una vez
    /// (<see cref="CargarAsync"/>) y la ruedita del selector la cambia (<see cref="Establecer"/>); cada selector cargado
    /// escucha <see cref="Cambiado"/> mientras está en pantalla y se repinta al momento. El control nunca llama a la API
    /// por su cuenta para esto.
    /// </summary>
    public static class PreferenciaTamanoLetraSelectorCliente
    {
        private static TamanoLetraSelectorCliente _actual = TamanoLetraSelectorCliente.Normal;

        public static TamanoLetraSelectorCliente Actual => _actual;

        /// <summary>Salta (en el hilo de quien llama a Establecer) cuando cambia el tamaño.</summary>
        public static event EventHandler Cambiado;

        public static void Establecer(TamanoLetraSelectorCliente tamano)
        {
            if (_actual == tamano)
            {
                return;
            }
            _actual = tamano;
            Cambiado?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Lee el parámetro del usuario (empresa por defecto) y lo aplica. Sin fila, sin red o con un valor que no se
        /// entiende, se queda en Normal. Nunca lanza.
        /// </summary>
        public static async Task CargarAsync(IConfiguracion configuracion)
        {
            if (configuracion is null)
            {
                return;
            }
            string valor;
            try
            {
                valor = await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.TamanoLetraSelectorCliente);
            }
            catch
            {
                return;
            }
            Establecer(ReglaTamanoLetraSelectorCliente.DesdeParametro(valor));
        }

        /// <summary>Solo para las pruebas: vuelve a Normal sin avisar a nadie.</summary>
        internal static void ReiniciarParaPruebas()
        {
            _actual = TamanoLetraSelectorCliente.Normal;
            Cambiado = null;
        }
    }
}
