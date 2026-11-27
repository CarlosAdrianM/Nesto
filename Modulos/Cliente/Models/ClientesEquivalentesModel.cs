using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>NestoAPI#876 (27/11/26): una ficha de un grupo de clientes equivalentes (FichaEquivalenteDTO de la API).</summary>
    public class FichaEquivalenteModel
    {
        public string Cliente { get; set; }
        public string Nombre { get; set; }
        public short? Estado { get; set; }
        public bool Activa { get; set; }
        public string Nif { get; set; }
        public bool EsLaConsultada { get; set; }
        /// <summary>A qué ficha apunta esta como su anterior (null = a ninguna).</summary>
        public string ClienteAnterior { get; set; }
        /// <summary>«Duplicado» o «CambioFiscal» (null si no consta).</summary>
        public string Motivo { get; set; }
        public string Comentario { get; set; }
        public string Usuario { get; set; }
        public DateTime? Fecha { get; set; }
    }

    /// <summary>GET api/Clientes/{empresa}/{cliente}/Equivalentes (ClientesEquivalentesDTO).</summary>
    public class ClientesEquivalentesModel
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public List<FichaEquivalenteModel> Fichas { get; set; } = new List<FichaEquivalenteModel>();
    }

    /// <summary>Cuerpo de POST api/Clientes/Equivalentes (PeticionEnlaceEquivalenteDTO).</summary>
    public class PeticionEnlaceEquivalenteModel
    {
        public string Empresa { get; set; }
        /// <summary>La ficha que queda.</summary>
        public string Cliente { get; set; }
        /// <summary>La otra ficha.</summary>
        public string ClienteAnterior { get; set; }
        /// <summary>«Duplicado» o «CambioFiscal».</summary>
        public string Motivo { get; set; }
        public string Comentario { get; set; }
    }

    /// <summary>Respuesta de POST api/Clientes/Equivalentes y de DELETE …/Equivalentes/{clienteAnterior} (ResultadoEnlaceEquivalenteDTO).</summary>
    public class ResultadoEnlaceEquivalenteModel
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string ClienteAnterior { get; set; }
        public string Motivo { get; set; }
        public bool Cambiado { get; set; }
        public bool YaEstaba { get; set; }
        public string EnlaceCliente { get; set; }
        public string EnlaceClienteAnterior { get; set; }
        public bool Encadenado { get; set; }
        public string Mensaje { get; set; }
        public List<string> Avisos { get; set; } = new List<string>();
        public List<FichaEquivalenteModel> Fichas { get; set; } = new List<FichaEquivalenteModel>();
    }
}
