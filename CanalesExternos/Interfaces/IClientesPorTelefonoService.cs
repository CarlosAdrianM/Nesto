using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Interfaces
{
    /// <summary>
    /// Nesto#340: datos de cliente que necesitan los pedidos de canales externos, servidos por
    /// GET api/Clientes/PorTelefono y GET api/Clientes/PorNif (las búsquedas ya no consultan
    /// la BD con EF).
    /// </summary>
    public class ClientePorTelefono
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string ContactoCobro { get; set; }
        // Nesto#340: el pedido de Prestashop se crea sobre el contacto por defecto
        public string ContactoDefecto { get; set; }
        public string Vendedor { get; set; }
        public string Iva { get; set; }
        public string ComentarioPicking { get; set; }
        public string Nombre { get; set; }
    }

    public interface IClientesPorTelefonoService
    {
        Task<List<ClientePorTelefono>> BuscarClientesPorTelefonoAsync(string telefono);
        // Nesto#340: búsqueda por NIF (exacto y, si no hay, Contains) para los pedidos de
        // Prestashop; el filtro de principales activos lo aplica el servidor
        Task<List<ClientePorTelefono>> BuscarClientesPorNifAsync(string nif);
    }
}
