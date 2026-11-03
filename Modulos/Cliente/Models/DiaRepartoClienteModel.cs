using System;

namespace Nesto.Modulos.Cliente.Models
{
    // Nesto#606 (NestoAPI#799): el contrato de api/Clientes/DiaReparto.

    /// <summary>
    /// El «día de reparto propio» de un cliente/contacto: excepción al día que le toca por su código postal. Ruta «01» = lunes …
    /// «05» = viernes.
    /// </summary>
    public class DiaRepartoClienteModel
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Ruta { get; set; }
        public string Dia { get; set; }
        public string Motivo { get; set; }
        public string Usuario { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }

    /// <summary>Cuerpo del PUT: <c>Ruta</c> null quita la excepción.</summary>
    public class GuardarDiaRepartoClienteModel
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Ruta { get; set; }
        public string Motivo { get; set; }
    }

    /// <summary>Respuesta del PUT.</summary>
    public class DiaRepartoGuardadoModel
    {
        public string Ruta { get; set; }
        public string Dia { get; set; }
        public int PedidosRecalculados { get; set; }
    }
}
