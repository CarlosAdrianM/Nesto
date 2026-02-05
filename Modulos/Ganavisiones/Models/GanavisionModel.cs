using System;

namespace Nesto.Modulos.Ganavisiones.Models
{
    public class GanavisionModel
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string ProductoId { get; set; }
        public string ProductoNombre { get; set; }
        public int Ganavisiones { get; set; }
        public DateTime FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        public string Usuario { get; set; }
    }

    public class GanavisionCreateModel
    {
        public string Empresa { get; set; }
        public string ProductoId { get; set; }
        public int? Ganavisiones { get; set; }
        public DateTime FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }
}
