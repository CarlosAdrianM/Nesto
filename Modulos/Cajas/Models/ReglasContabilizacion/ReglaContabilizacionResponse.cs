using System.Collections.Generic;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class ReglaContabilizacionResponse
    {
        internal List<PreContabilidadDTO> Lineas { get; set; }
        internal bool CrearFacturas { get; set; } = false;
        internal bool CrearPagosFacturas { get; set; } = false;
        internal string Documento { get; set; } // para poner datos que nos interese guardar
    }
}
