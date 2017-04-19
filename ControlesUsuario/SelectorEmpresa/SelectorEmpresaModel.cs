using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Models
{
    public class Empresa
    {
        [JsonProperty("Número")]
        public string empresa { get; set; }
        [JsonProperty("Nombre")]
        public string nombre { get; set; }
    }
}
