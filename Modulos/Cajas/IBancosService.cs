using Nesto.Modulos.Cajas.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas
{
    public interface IBancosService
    {
        Task<List<ApunteBancarioDTO>> CargarFicheroCuaderno43(string contenido);
    }
}
