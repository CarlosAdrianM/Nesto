using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.Models
{
    // Nesto#584 (NestoAPI#763): el contrato de api/Clientes/{empresa}/{cliente}/{contacto}/HorarioPreferible y de las propuestas.

    /// <summary>Una franja del horario preferible: 1 = lunes … 7 = domingo; horas «HH:mm».</summary>
    public class FranjaHorarioPreferibleModel
    {
        public int DiaSemana { get; set; }
        public string Desde { get; set; }
        public string Hasta { get; set; }
    }

    /// <summary>El horario preferible de un cliente/contacto (se intenta respetar, sin compromiso).</summary>
    public class HorarioPreferibleModel
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public List<FranjaHorarioPreferibleModel> Franjas { get; set; } = new();
        /// <summary>«L-V 10:00-14:00, 17:00-20:00; S 10:00-13:00», o vacío.</summary>
        public string Texto { get; set; }
        /// <summary>El aviso de la API: se intenta respetar, pero sin compromiso.</summary>
        public string Aviso { get; set; }
        public string Usuario { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public PropuestaHorarioPreferibleModel PropuestaPendiente { get; set; }
    }

    /// <summary>Un horario sacado de los comentarios de la ficha, por revisar.</summary>
    public class PropuestaHorarioPreferibleModel
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string Poblacion { get; set; }
        public string Ruta { get; set; }
        /// <summary>El texto de los comentarios del que sale.</summary>
        public string TextoOrigen { get; set; }
        /// <summary>Lo que proponen las reglas de la API; null si no han sabido sacarlo.</summary>
        public string HorarioPropuesto { get; set; }
        public List<FranjaHorarioPreferibleModel> Franjas { get; set; } = new();
        public string Estado { get; set; }
        public DateTime Fecha { get; set; }
    }
}
