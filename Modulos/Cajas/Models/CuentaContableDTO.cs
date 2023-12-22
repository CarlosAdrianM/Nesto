namespace Nesto.Modulos.Cajas.Models
{
    public class CuentaContableDTO
    {
        public string Cuenta { get; set; }
        public string Nombre { get; set; }
        public string DescripcionCompleta => $"{Cuenta} -> {Nombre}";
    }
}
