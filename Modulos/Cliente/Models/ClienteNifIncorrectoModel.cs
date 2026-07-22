using System;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>
    /// Nesto#417: fila del listado de clientes con NIF incorrecto contra el censo de la AEAT
    /// (GET api/Clientes/NifIncorrectos). El servidor ya lo devuelve priorizado: primero los
    /// que tienen pedido pendiente de servir o facturar.
    /// </summary>
    public class ClienteNifIncorrectoModel
    {
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public string Nif { get; set; }
        public string ResultadoAeat { get; set; }
        public DateTime FechaValidacion { get; set; }
        public string Vendedor { get; set; }
        public bool TienePedidoPendiente { get; set; }
    }

    /// <summary>
    /// Nesto#417: respuesta de POST api/Clientes/CorregirNif — la corrección centralizada
    /// (revalida contra la AEAT, propaga a todos los contactos y corrige las facturas sin
    /// declarar a Verifactu).
    /// </summary>
    public class ResultadoCorreccionNifModel
    {
        public bool Corregido { get; set; }
        public string Nif { get; set; }
        public string ResultadoAeat { get; set; }
        public string NombreAeat { get; set; }
        public int ContactosActualizados { get; set; }
        public int FacturasActualizadas { get; set; }
        public string Motivo { get; set; }
    }
}
