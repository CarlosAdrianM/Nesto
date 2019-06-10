using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.Models
{
    public class ClienteCrear
    {
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string CodigoPostal { get; set; }
        public string Direccion { get; set; }
        public bool EsContacto { get; set; }
        public short? Estado { get; set; }
        public bool Estetica { get; set; }
        public string FormaPago { get; set; }
        public string Iban { get; set; }
        public string Nif { get; set; }
        public string Nombre { get; set; }
        public bool Peluqueria { get; set; }
        public string PlazosPago { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string Ruta { get; set; }
        public string Telefono { get; set; }
        public string VendedorEstetica { get; set; }
        public string VendedorPeluqueria { get; set; }

        public string Usuario { get; set; }


        public virtual ICollection<PersonaContactoDTO> PersonasContacto { get; set; }
    }
}
