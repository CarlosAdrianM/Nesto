using System.Collections.Generic;

namespace ControlesUsuario.Models
{
    /// <summary>
    /// Parámetro de usuario que el PROPIO usuario puede cambiarse desde Nesto (caso real
    /// 20/08/26: Tienda Online alterna AMZ/ALG según facture FBA o rutas). El catálogo de qué
    /// es editable, por quién y con qué valores vive en NestoAPI: aquí solo se pinta.
    /// </summary>
    public class ParametroEditableModel
    {
        public string Clave { get; set; }
        public string Descripcion { get; set; }
        public string ValorActual { get; set; }
        /// <summary>Valor "titular" (el de antes del primer cambio): el arranque de Nesto
        /// ofrece restaurarlo para que un cambio temporal no se quede puesto por olvido.</summary>
        public string ValorTitular { get; set; }
        public List<OpcionParametroModel> Opciones { get; set; }
    }

    public class OpcionParametroModel
    {
        public string Valor { get; set; }
        public string Descripcion { get; set; }
        public string TextoFormateado => $"{Valor} - {Descripcion}";
    }
}
