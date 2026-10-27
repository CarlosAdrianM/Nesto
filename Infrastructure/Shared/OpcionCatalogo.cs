namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#558: una entrada de un catálogo cerrado de la API (código que se manda, texto que se enseña). Es lo que
    /// enlazan las combos y los chips (SelectedValuePath = Codigo, DisplayMemberPath = Nombre).
    /// </summary>
    public class OpcionCatalogo
    {
        public OpcionCatalogo(string codigo, string nombre)
        {
            Codigo = codigo;
            Nombre = nombre;
        }

        /// <summary>El código tal como lo espera la API (null en la opción «ninguno»).</summary>
        public string Codigo { get; }

        public string Nombre { get; }

        public override string ToString() => Nombre;
    }
}
