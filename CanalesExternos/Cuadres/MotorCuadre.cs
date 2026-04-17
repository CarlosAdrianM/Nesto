using Nesto.Modulos.CanalesExternos.Models.Cuadres;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.CanalesExternos.Cuadres
{
    /// <summary>
    /// Motor genérico de conciliación estilo reconciliación bancaria. Toma dos conjuntos
    /// (lado Nesto y lado canal externo), empareja por una clave común y clasifica cada
    /// elemento en uno de los cuatro bloques del <see cref="ResultadoCuadre{TClave}"/>:
    /// cuadrados, solo en Nesto, solo en Amazon, o con importes distintos.
    ///
    /// Puro: sin IO, sin estado. Los datos concretos los aportan quienes llaman
    /// (clientes REST + SDK de Amazon).
    /// </summary>
    public static class MotorCuadre
    {
        public static ResultadoCuadre<TClave> Conciliar<TNesto, TAmazon, TClave>(
            IEnumerable<TNesto> nesto,
            IEnumerable<TAmazon> amazon,
            Func<TNesto, TClave> claveNesto,
            Func<TAmazon, TClave> claveAmazon,
            Func<TNesto, decimal> importeNesto,
            Func<TAmazon, decimal> importeAmazon,
            Func<TClave, string> descripcion = null)
        {
            if (nesto == null) throw new ArgumentNullException(nameof(nesto));
            if (amazon == null) throw new ArgumentNullException(nameof(amazon));
            if (claveNesto == null) throw new ArgumentNullException(nameof(claveNesto));
            if (claveAmazon == null) throw new ArgumentNullException(nameof(claveAmazon));
            if (importeNesto == null) throw new ArgumentNullException(nameof(importeNesto));
            if (importeAmazon == null) throw new ArgumentNullException(nameof(importeAmazon));

            // Agrupamos por clave sumando importes: si una clave aparece varias veces en un
            // lado (p. ej. varios eventos Amazon del mismo InvoiceId) se consolida.
            var nestoPorClave = nesto
                .GroupBy(claveNesto)
                .ToDictionary(g => g.Key, g => g.Sum(importeNesto));
            var amazonPorClave = amazon
                .GroupBy(claveAmazon)
                .ToDictionary(g => g.Key, g => g.Sum(importeAmazon));

            var resultado = new ResultadoCuadre<TClave>();
            var todasLasClaves = nestoPorClave.Keys.Union(amazonPorClave.Keys);

            foreach (var clave in todasLasClaves)
            {
                bool enNesto = nestoPorClave.TryGetValue(clave, out decimal impN);
                bool enAmazon = amazonPorClave.TryGetValue(clave, out decimal impA);

                var elemento = new ElementoCuadre<TClave>
                {
                    Clave = clave,
                    Descripcion = descripcion?.Invoke(clave),
                    ExisteEnNesto = enNesto,
                    ExisteEnAmazon = enAmazon,
                    ImporteNesto = enNesto ? (decimal?)impN : null,
                    ImporteAmazon = enAmazon ? (decimal?)impA : null
                };

                if (!enNesto)
                {
                    resultado.SoloEnAmazon.Add(elemento);
                }
                else if (!enAmazon)
                {
                    resultado.SoloEnNesto.Add(elemento);
                }
                else if (impN == impA)
                {
                    resultado.Cuadrados.Add(elemento);
                }
                else
                {
                    resultado.ImportesDistintos.Add(elemento);
                }
            }

            return resultado;
        }

        /// <summary>
        /// Concilia solo por presencia: la clave existe en Nesto, en Amazon o en ambos.
        /// No compara importes (úsalo cuando uno de los lados no los expone: p. ej. el
        /// endpoint actual de Nesto solo devuelve InvoiceId → NumFactura). Resultado:
        /// - existe en ambos → <see cref="ResultadoCuadre{TClave}.Cuadrados"/>
        /// - existe solo en uno → el bloque correspondiente
        /// - <see cref="ResultadoCuadre{TClave}.ImportesDistintos"/> siempre vacío.
        /// </summary>
        public static ResultadoCuadre<TClave> ConciliarPorPresencia<TNesto, TAmazon, TClave>(
            IEnumerable<TNesto> nesto,
            IEnumerable<TAmazon> amazon,
            Func<TNesto, TClave> claveNesto,
            Func<TAmazon, TClave> claveAmazon,
            Func<TClave, string> descripcion = null)
        {
            if (nesto == null) throw new ArgumentNullException(nameof(nesto));
            if (amazon == null) throw new ArgumentNullException(nameof(amazon));
            if (claveNesto == null) throw new ArgumentNullException(nameof(claveNesto));
            if (claveAmazon == null) throw new ArgumentNullException(nameof(claveAmazon));

            var clavesNesto = new HashSet<TClave>(nesto.Select(claveNesto));
            var clavesAmazon = new HashSet<TClave>(amazon.Select(claveAmazon));

            var resultado = new ResultadoCuadre<TClave>();
            foreach (var clave in clavesNesto.Union(clavesAmazon))
            {
                bool enN = clavesNesto.Contains(clave);
                bool enA = clavesAmazon.Contains(clave);

                var elemento = new ElementoCuadre<TClave>
                {
                    Clave = clave,
                    Descripcion = descripcion?.Invoke(clave),
                    ExisteEnNesto = enN,
                    ExisteEnAmazon = enA
                };

                if (enN && enA) resultado.Cuadrados.Add(elemento);
                else if (enN) resultado.SoloEnNesto.Add(elemento);
                else resultado.SoloEnAmazon.Add(elemento);
            }

            return resultado;
        }
    }
}
