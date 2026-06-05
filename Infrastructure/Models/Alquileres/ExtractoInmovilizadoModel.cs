using System;

namespace Nesto.Infrastructure.Models.Alquileres
{
    /// <summary>
    /// Una línea del extracto de inmovilizado de un alquiler, para la pestaña "Inmovilizados".
    /// Solo lectura en la UI (Nesto#340, Fase 1C.2). Sustituye al uso directo de la entidad EF
    /// ExtractoInmovilizado; el grid auto-genera las columnas a partir de estas propiedades.
    /// </summary>
    public class ExtractoInmovilizadoModel
    {
        public int NumeroOrden { get; set; }
        public DateTime Fecha { get; set; }
        public string Concepto { get; set; }
        public string NumeroDocumento { get; set; }
        public decimal Importe { get; set; }
        public decimal ImportePendiente { get; set; }
        public short Estado { get; set; }
    }
}
