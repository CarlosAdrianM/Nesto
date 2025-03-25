using Nesto.Modulos.Cajas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Interfaces
{
    public interface IBancoConciliacion
    {
        BancoDTO Banco { get; set; }
        Task<ContenidoCuaderno43> CargarFicheroMovimientos(string contenidoFichero);
        Task<List<MovimientoTPV>> CargarFicheroMovimientosTarjeta(string contenidoFichero);
        bool CumpleCondicionesTPV(ApunteBancarioWrapper apunteBancoSeleccionado);
        string ParametroRutaFicherosMovimientos { get; }
    }
}
