using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490: bolsa de parámetros sin tipos de Prism, base de <see cref="ParametrosDialogo"/> (4C.2)
    /// y <see cref="ParametrosNavegacion"/> (4C.4). Admite el inicializador de colección
    /// (<c>{ { "clave", valor } }</c>). <see cref="GetValue{T}"/> se comporta como el de Prism: clave
    /// inexistente o valor null devuelve default(T) y, si el tipo no casa, intenta convertirlo.
    /// </summary>
    public abstract class ParametrosNesto : IEnumerable<KeyValuePair<string, object>>
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
