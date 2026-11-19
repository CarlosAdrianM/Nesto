using Nesto.Infrastructure.Shared;

namespace Nesto.Modulos.Cliente.Models
{
    // NestoAPI#879 (corte 3): lo que lee y escribe la pestaña «Direcciones» de la ficha del cliente.

    /// <summary>Un contacto (dirección) del cliente, de GET api/PlantillaVentas/DireccionesEntrega (solo los activos).</summary>
    public class DireccionClienteModel
    {
        public string contacto { get; set; }
        /// <summary>La ficha principal: la dirección de FACTURACIÓN.</summary>
        public bool clientePrincipal { get; set; }
        /// <summary>El ContactoDefecto de la principal: la dirección de ENTREGA POR DEFECTO.</summary>
        public bool esDireccionPorDefecto { get; set; }
        public string nombre { get; set; }
        public string direccion { get; set; }
        public string direccionCompleta { get; set; }
        public string codigoPostal { get; set; }
        public string poblacion { get; set; }
        public string provincia { get; set; }
        public short? estado { get; set; }

        public string DireccionMostrar => DireccionParaMostrar.Elegir(direccion, direccionCompleta)?.Trim();
    }

    /// <summary>Cuerpo del PUT api/Clientes/ContactoDefecto.</summary>
    public class CambiarContactoDefectoModel
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
    }

    /// <summary>Respuesta de GET/PUT api/Clientes/ContactoDefecto.</summary>
    public class ContactoDefectoClienteModel
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string ContactoFacturacion { get; set; }
        public string ContactoDefecto { get; set; }
        public bool Cambiado { get; set; }
    }
}
