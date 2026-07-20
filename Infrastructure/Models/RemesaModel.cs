using System;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Remesa de cobros ligera para el grid de RemesasViewModel (Nesto#340, Fase 1C.14 slice 2).
    /// El DTO del API ya viene con nombres ASCII (Numero), así que no necesita JsonProperty.
    /// Solo lectura en la UI, así que no necesita INotifyPropertyChanged.
    /// </summary>
    public class RemesaModel
    {
        public int Numero { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Importe { get; set; }
        public string Banco { get; set; }
    }
}
