using ControlesUsuario.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    public interface ISelectorClienteService
    {
        Task<ObservableCollection<ClienteDTO>> BuscarClientes(string empresa, string vendedor, string filtro);
        Task<ClienteDTO> CargarCliente(string empresa, string cliente, string contacto);

        // Sugerencia 582: para un vendedor telefónico, el contacto en estado 9 primero (lo decide la API).
        Task<ObservableCollection<ClienteDTO>> BuscarClientesContactoTelefonicoPrimero(string empresa, string vendedor, string filtro);
        Task<ClienteDTO> CargarClienteContactoTelefonico(string empresa, string cliente, string vendedor);
    }
}
