using Nesto.Infrastructure.Contracts;
using Nesto.Models;

namespace Nesto.Modulos.CanalesExternos
{
    public class PedidoCanalExterno : IFiltrableItem
    {
        public string PedidoCanalId { get; set; }
        public int PedidoNestoId { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string TelefonoFijo { get; set; }
        public string TelefonoMovil { get; set; }
        public string CorreoElectronico { get; set; }
        public string PaisISO { get; set; }
        public string Observaciones { get; set; }
        public string UltimoSeguimiento { get; set; }
        public PedidoVentaDTO Pedido { get; set; }
        public string Almacen { get; set; }

        public bool Contains(string filtro)
        {
            return (PedidoNestoId.ToString() == filtro) || (Nombre != null && Nombre.ToLower().ToString().Contains(filtro)) || 
                (Observaciones != null && Observaciones.ToLower().ToString().Contains(filtro)) || (Direccion != null && Direccion.ToLower().ToString().Contains(filtro)) || 
                (CodigoPostal != null && CodigoPostal.ToString() == filtro) || (Poblacion != null && Poblacion.ToLower().ToString().Contains(filtro)) ||
                (TelefonoFijo != null && TelefonoFijo.ToString() == filtro) || (TelefonoMovil != null && TelefonoMovil.ToString() == filtro);
        }
    }
}
