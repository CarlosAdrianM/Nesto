using System;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Interfaces
{
    public interface IRecursosHumanosService
    {
        Task<bool> EsFestivo(DateTime fecha, string delegacion);
    }
}
