using Prism.Events;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Events
{
    /// <summary>
    /// Nesto#419 / NestoAPI#333: se publica al liquidar efectos en el Extracto de Cliente. La
    /// ventana de Remesas lo escucha y actualiza EN SITIO los efectos afectados (importe pendiente
    /// y resaltado de negativos) SIN recargar los candidatos, para no perder las marcas que el
    /// usuario tuviera hechas en la selección de la remesa.
    /// </summary>
    public class EfectosLiquidadosEvent : PubSubEvent<EfectosLiquidadosPayload> { }

    public class EfectosLiquidadosPayload
    {
        public string Empresa { get; set; }

        /// <summary>Cliente cuyos efectos se han liquidado (para actualizar su resaltado).</summary>
        public string Cliente { get; set; }

        /// <summary>Id del movimiento (Nº Orden del ExtractoCliente, el mismo que usan los
        /// candidatos a remesa) -> importe pendiente TRAS liquidar (0 = saldado por completo).</summary>
        public IDictionary<int, decimal> NuevosImportesPendientes { get; set; }

        /// <summary>Tras liquidar, ¿el cliente sigue teniendo movimientos negativos pendientes?
        /// Sirve para quitar (o mantener) el resaltado naranja de sus efectos en Remesas.</summary>
        public bool ClienteSigueConNegativos { get; set; }
    }
}
