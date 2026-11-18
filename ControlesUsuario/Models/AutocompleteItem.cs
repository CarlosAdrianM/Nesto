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
        /// Sugerencia 1339: tamaño del producto (null si no se conoce: el buscador de la API aún no lo manda, o es una cuenta).
        /// </summary>
        public decimal? Tamanno { get; set; }

        /// <summary>Sugerencia 1339: unidad del tamaño («ml», «gr»…). Sin ella no se pinta el tamaño.</summary>
        public string UnidadMedida { get; set; }

        /// <summary>
        /// Texto formateado para mostrar en el popup.
        /// Combina Id y Texto para fácil lectura; con tamaño, detrás del nombre («44256 - HIDRATANTE OPTIMA CREAM 50 ml (Belclinic)»).
        /// </summary>
        public string TextoMostrar
        {
            get
            {
                string texto = TamanoProducto.NombreConTamano(Texto, Tamanno, UnidadMedida);
                return string.IsNullOrEmpty(TextoSecundario)
                    ? $"{Id} - {texto}"
                    : $"{Id} - {texto} ({TextoSecundario})";
            }
        }
    }
}
