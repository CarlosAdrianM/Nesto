using ControlesUsuario.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    public interface ISelectorClienteService
    {
        Task<ObservableCollection<ClienteDTO>> BuscarClientes(string empresa, string vendedor, string filtro);
        Task<ClienteDTO> CargarCliente(string empresa, string cliente, string contacto);
    }
}
