using Nesto.Models;
using Nesto.Models.Nesto.Models;
using System.Collections.Generic;
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
        // NestoAPI#306 / Nesto#409: autocompletado de direcciones (Google Places vía NestoAPI)
        Task<List<SugerenciaDireccionModel>> BuscarSugerenciasDireccion(string texto, string sessionToken);
        Task<DireccionDetalleModel> LeerDetalleDireccion(string placeId, string sessionToken);
    }

    /// <summary>Nesto#409: una sugerencia del combo de direcciones (Places).</summary>
    public class SugerenciaDireccionModel
    {
        public string Descripcion { get; set; }
        public string PlaceId { get; set; }
    }

    /// <summary>Nesto#409: la dirección seleccionada, con calle/número/CP ya troceados.</summary>
    public class DireccionDetalleModel
    {
        public string Calle { get; set; }
        public string Numero { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string DireccionFormateada { get; set; }
    }
}