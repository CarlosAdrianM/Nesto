using System.ComponentModel.DataAnnotations;

namespace Nesto.Modulos.Cliente.Models
{
    public class PersonaContactoDTO
    {
        public string Nombre { get; set; }
        [EmailAddress]
        public string CorreoElectronico { get; set; }
    }
}
