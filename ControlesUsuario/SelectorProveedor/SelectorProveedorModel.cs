using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Models
{
    public class SelectorProveedorModel
    {
        public class ProveedorDTO : IFiltrableItem
        {
            [JsonConverter(typeof(TrimConverter))]
            public string Empresa { get; set; }
            [JsonConverter(typeof(TrimConverter))]
            [JsonProperty("Número")]
            public string Proveedor { get; set; }
            [JsonConverter(typeof(TrimConverter))]
            public string Contacto { get; set; }
            [JsonConverter(typeof(TrimConverter))]
            public string Nombre { get; set; }
            [JsonConverter(typeof(TrimConverter))]
            [JsonProperty("Dirección")]
            public string Direccion { get; set; }
            [JsonConverter(typeof(TrimConverter))]
            [JsonProperty("CodPostal")]
            public string CodigoPostal { get; set; }
            [JsonConverter(typeof(TrimConverter))]
            [JsonProperty("CIF_NIF")]
            public string CifNif { get; set; }
            [JsonConverter(typeof(TrimConverter))]
            [JsonProperty("Teléfono")]
            public string Telefono { get; set; }
            public string Comentarios { get; set; }

            public bool Contains(string filtro) => 
                Proveedor == filtro ||
                CifNif.Contains(filtro) ||
                Direccion.Contains(filtro) ||
                Nombre.Contains(filtro);            
        }
    }
}
