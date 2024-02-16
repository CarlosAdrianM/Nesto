using System.Collections.Generic;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal interface IReglaContabilizacion
    {
        bool EsContabilizable(ApunteBancarioDTO apunteBancario, ContabilidadDTO apunteContabilidad);
        ReglaContabilizacionResponse ApuntesContabilizar(ApunteBancarioDTO apunteBancario, BancoDTO banco, decimal importeDescuadre);
    }
}
