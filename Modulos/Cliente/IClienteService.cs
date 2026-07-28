using Nesto.Models;
using Nesto.Models.Nesto.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    public interface IClienteService
    {
        Task<RespuestaNifNombreCliente> ValidarNif(string nif, string nombre);
        // direccionVerificada=true cuando dirección y CP vienen del combo de Places (Nesto#409):
        // el servidor se salta el geocoding y solo normaliza para la BD.
        // pais (Nesto#436): ISO-2 del país de la DIRECCIÓN; para país != ES el servidor no valida
        // contra la tabla española de CPs ni geocodifica.
        Task<RespuestaDatosGeneralesClientes> ValidarDatosGenerales(string direccion, string codigoPostal, string telefono, bool direccionVerificada = false, string pais = null);
        Task<RespuestaDatosBancoCliente> ValidarDatosPago(string formaPago, string plazosPago, string iban);
        Task<Clientes> CrearCliente(ClienteCrear cliente);
        Task<Clientes> ModificarCliente(ClienteCrear cliente);
        Task<ClienteCrear> LeerClienteCrear(string empresa, string cliente, string contacto);
        // NestoAPI#306 / Nesto#409: autocompletado de direcciones (Google Places vía NestoAPI).
        // pais (Nesto#436): ISO-2 donde buscar; vacío = España.
        Task<List<SugerenciaDireccionModel>> BuscarSugerenciasDireccion(string texto, string sessionToken, string pais = null);
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
        /// <summary>Nesto#436: nombre del país de la dirección ("Italia").</summary>
        public string Pais { get; set; }
        /// <summary>Nesto#436: ISO-2 del país ("IT"), para no asumir España en la ficha.</summary>
        public string PaisIso { get; set; }
    }
}