namespace ControlesUsuario.Models
{
    /// <summary>
    /// Modelo para las series usadas en el SelectorSerie.
    /// Carlos 09/12/25: Issue #245
    /// </summary>
    public class SerieItem
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }

        /// <summary>
        /// Texto formateado para mostrar en el ComboBox.
        /// </summary>
        public string TextoFormateado => $"{Codigo} - {Nombre}";
    }
}
