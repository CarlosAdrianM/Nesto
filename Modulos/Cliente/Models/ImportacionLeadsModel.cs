using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.Models
{
    // NestoAPI#623 corte 4: el contrato de POST api/Clientes/Origenes/ImportarLeads.

    /// <summary>Qué pasa (o pasaría, en el ensayo) con un lead del fichero de Meta.</summary>
    public class LeadImportadoModel
    {
        public int Fila { get; set; }
        public string Lead { get; set; }
        public DateTime? FechaLead { get; set; }
        public string Nombre { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
        public string CodigoPostal { get; set; }
        public bool AceptaComunicaciones { get; set; }
        /// <summary>CREAR (ensayo) / CREADO / YA_EXISTIA / ERROR.</summary>
        public string Codigo { get; set; }
        public string Accion { get; set; }
        public string Motivo { get; set; }
        public string Cliente { get; set; }
        public string Vendedor { get; set; }

        /// <summary>Para la rejilla: «Sí» / «No».</summary>
        public string Comunicaciones => AceptaComunicaciones ? "Sí" : "No";
    }

    public class ResultadoImportacionLeadsModel
    {
        public bool Ensayo { get; set; }
        public string Origen { get; set; }
        public int Leidos { get; set; }
        public int Creados { get; set; }
        public int YaExistian { get; set; }
        public int Errores { get; set; }
        public List<LeadImportadoModel> Leads { get; set; } = new List<LeadImportadoModel>();
    }
}
