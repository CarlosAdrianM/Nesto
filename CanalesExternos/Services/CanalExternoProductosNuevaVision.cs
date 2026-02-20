using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Services
{
    public class CanalExternoProductosNuevaVision : ICanalExternoProductos
    {
        private readonly ICanalesExternosProductosService _servicio;

        public CanalExternoProductosNuevaVision(ICanalesExternosProductosService servicio)
        {
            _servicio = servicio;
        }

        public string Nombre => "Servidores Nueva Visión";

        public async Task ActualizarProducto(ProductoCanalExterno producto)
        {
            await _servicio.SaveProductoAsync(producto);
        }
    }
}
