using System;

namespace Nesto.Modulos.Cajas.Models
{
    public class ConciliacionEliminadaDTO
    {
        public int Id { get; set; }
        public int? ApunteBancoId { get; set; }
        public int? ApunteContabilidadId { get; set; }
        public decimal ImportePunteado { get; set; }
        public string SimboloPunteo { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
