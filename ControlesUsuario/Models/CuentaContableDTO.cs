namespace ControlesUsuario.Models
{
    /// <summary>
    /// DTO para cuenta contable.
    /// Issue #258 - Carlos 11/12/25
    /// </summary>
    public class CuentaContableDTO
    {
        public string Cuenta { get; set; }
        public string Nombre { get; set; }
        public string Iva { get; set; }
        public string DescripcionCompleta => $"{Cuenta} -> {Nombre}";
    }
}
