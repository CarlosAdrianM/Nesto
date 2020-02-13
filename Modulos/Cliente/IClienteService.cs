using Nesto.Models;
using Nesto.Models.Nesto.Models;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    public interface IClienteService
    {
        Task<RespuestaNifNombreCliente> ValidarNif(string nif, string nombre);
        Task<RespuestaDatosGeneralesClientes> ValidarDatosGenerales(string direccion, string codigoPostal, string telefono);
        Task<RespuestaDatosBancoCliente> ValidarDatosPago(string formaPago, string plazosPago, string iban);
        Task<Clientes> CrearCliente(ClienteCrear cliente);
        Task<Clientes> ModificarCliente(ClienteCrear cliente);
        Task<ClienteCrear> LeerClienteCrear(string empresa, string cliente, string contacto);
    }
}