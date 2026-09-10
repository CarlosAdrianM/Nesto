using System;

namespace Nesto.Modules.Producto.Models
{
    /// <summary>
    /// NestoAPI#477: una referencia dentro de una familia de variantes (color, tapizado...) de la
    /// tienda online. En PrestaShop la familia es UNA ficha —la de la referencia principal— con una
    /// combinación por referencia; la principal es una más de la familia, con su propio valor.
    ///
    /// El dato vive en Nesto y viaja por el bus. El orden de la lista es la posición de la
    /// combinación en la ficha, así que reordenar importa.
    /// </summary>
    public class VarianteModel
    {
        public string Numero { get; set; }
        public string Principal { get; set; }
        public string Atributo { get; set; }
        public string Valor { get; set; }
        public int Orden { get; set; }
        public string Nombre { get; set; }

        public bool EsPrincipal => string.Equals(Numero?.Trim(), Principal?.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>Cómo se ve en la pantalla: "45814 — SILLON DE BARBERO CHECK BR · Color: Marrón".</summary>
        public string Descripcion =>
            $"{Numero?.Trim()} — {Nombre?.Trim()} · {Atributo?.Trim()}: {Valor?.Trim()}{(EsPrincipal ? " (principal)" : string.Empty)}";

        public bool EsLaMisma(string numero)
        {
            return string.Equals(Numero?.Trim(), numero?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
