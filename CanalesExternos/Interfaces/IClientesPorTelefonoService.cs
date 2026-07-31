using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Interfaces
{
    /// <summary>
    /// Nesto#340: datos de cliente que necesitan los pedidos de canales externos, servidos por
    /// GET api/Clientes/PorTelefono (la búsqueda por teléfono ya no consulta la BD con EF).
    /// </summary>
    public class ClientePorTelefono
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string ContactoCobro { get; set; }
        public string Vendedor { get; set; }
        public string Iva { get; set; }
        public string ComentarioPicking { get; set; }
        public string Nombre { get; set; }
    }

    public interface IClientesPorTelefonoService
    {
        Task<List<ClientePorTelefono>> BuscarClientesPorTelefonoAsync(string telefono);
    }
}
