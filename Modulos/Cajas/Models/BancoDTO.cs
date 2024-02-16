using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Models
{
    public class BancoDTO
    {
        public string Empresa { get;set; }
        [JsonProperty("Número")]
        public string Codigo { get;set; }
        [JsonProperty("Descripción")]
        public string Nombre { get;set; }
        [JsonProperty("Cuenta_Contable")]
        public string CuentaContable { get;set; }
        public string Entidad { get; set; }
        [JsonProperty("Sucursal")] 
        public string Oficina { get; set; }
        [JsonProperty("Nº_Cuenta")]
        public string NumeroCuenta { get; set; }
    }
}
