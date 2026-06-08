namespace ControlesUsuario.Models
{
    /// <summary>
    /// Candidato devuelto por la API cuando un código de barras corresponde a varios
    /// productos (NestoAPI#213 devuelve 409 con la lista). El cliente muestra un selector
    /// y reintenta la consulta con el Número elegido, que resuelve de forma única. Nesto#368.
    /// </summary>
    public class ProductoCodigoBarrasDuplicado
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
    }
}
