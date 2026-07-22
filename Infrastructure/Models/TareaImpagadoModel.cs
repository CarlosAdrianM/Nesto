using System;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Efecto de un asiento de impagados con los datos del cliente, para crear las tareas de
    /// Planner de gestión de cobro (Nesto#340, Fase 1C.14 slice 8). El DTO del API ya viene
    /// con nombres ASCII y los textos recortados.
    /// </summary>
    public class TareaImpagadoModel
    {
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Importe { get; set; }
        public string Concepto { get; set; }
        public string Vendedor { get; set; }
        public string NombreCliente { get; set; }
        public string Direccion { get; set; }
        public string Ruta { get; set; }
        public string NombreEmpresa { get; set; }
    }
}
