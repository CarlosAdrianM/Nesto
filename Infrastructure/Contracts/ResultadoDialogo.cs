using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.2): botón con el que se cerró un diálogo. Mismos valores que el
    /// <c>ButtonResult</c> de Prism (que a su vez son los de Win32), para poder convertir con un cast.
    /// </summary>
    public enum ResultadoBoton
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Abort = 3,
        Retry = 4,
        Ignore = 5,
        Yes = 6,
        No = 7
    }

    /// <summary>
    /// Nesto#490 (4C.2): resultado de un diálogo de <see cref="IServicioDialogos"/>, sin tipos de Prism.
    /// </summary>
    public class ResultadoDialogo
    {
        public ResultadoDialogo(ResultadoBoton result, ParametrosDialogo parameters = null)
        {
            Result = result;
            Parameters = parameters ?? new ParametrosDialogo();
        }

        public ResultadoBoton Result { get; }
        public ParametrosDialogo Parameters { get; }
    }

    /// <summary>
    /// Nesto#490 (4C.2): parámetros de ida y vuelta de un diálogo, sin tipos de Prism. Admite el
    /// inicializador de colección igual que <c>DialogParameters</c>:
    /// <c>new ParametrosDialogo { { "title", "..." }, { "message", "..." } }</c>.
    /// <see cref="GetValue{T}"/> se comporta como el de Prism: clave inexistente o valor null
    /// devuelve default(T) y, si el tipo no casa, intenta convertirlo.
    /// </summary>
    public class ParametrosDialogo : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly List<KeyValuePair<string, object>> _entradas = new();

        public int Count => _entradas.Count;

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (KeyValuePair<string, object> entrada in _entradas)
                {
                    yield return entrada.Key;
                }
            }
        }

        public void Add(string key, object value)
        {
            _entradas.Add(new KeyValuePair<string, object>(key, value));
        }

        public bool ContainsKey(string key)
        {
            return _entradas.Exists(e => e.Key == key);
        }

        public T GetValue<T>(string key)
        {
            return TryGetValue(key, out T valor) ? valor : default;
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            foreach (KeyValuePair<string, object> entrada in _entradas)
            {
                if (entrada.Key == key)
                {
                    value = Convertir<T>(entrada.Value);
                    return true;
                }
            }
            value = default;
            return false;
        }

        private static T Convertir<T>(object valor)
        {
            if (valor == null)
            {
                return default;
            }
            if (valor is T tipado)
            {
                return tipado;
            }
            Type destino = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (destino.IsEnum)
            {
                return valor is string texto
                    ? (T)Enum.Parse(destino, texto)
                    : (T)Enum.ToObject(destino, valor);
            }
            return (T)Convert.ChangeType(valor, destino, CultureInfo.CurrentCulture);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _entradas.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
