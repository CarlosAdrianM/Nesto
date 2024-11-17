using System.Collections.Generic;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal interface IReglaContabilizacion
    {
        bool EsContabilizable(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad);
        ReglaContabilizacionResponse ApuntesContabilizar(IEnumerable<ApunteBancarioDTO> apuntesBancarios, IEnumerable<ContabilidadDTO> apuntesContabilidad, BancoDTO banco);
        string Nombre { get; }
    }
}
