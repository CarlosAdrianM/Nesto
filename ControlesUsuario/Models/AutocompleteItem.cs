namespace ControlesUsuario.Models
{
    /// <summary>
    /// DTO para items de sugerencia en el autocomplete.
    /// Issue #263 - Carlos 30/01/26
    /// </summary>
    public class AutocompleteItem
    {
        /// <summary>
        /// Código del item (producto o cuenta contable).
        /// Es el valor que se insertará en el TextBox al seleccionar.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Texto principal a mostrar (nombre del producto o cuenta).
        /// </summary>
        public string Texto { get; set; }

        /// <summary>
        /// Texto secundario opcional (familia, subgrupo, etc.).
        /// </summary>
        public string TextoSecundario { get; set; }

        /// <summary>
        /// Texto formateado para mostrar en el popup.
        /// Combina Id y Texto para fácil lectura.
        /// </summary>
        public string TextoMostrar => string.IsNullOrEmpty(TextoSecundario)
            ? $"{Id} - {Texto}"
            : $"{Id} - {Texto} ({TextoSecundario})";
    }
}
