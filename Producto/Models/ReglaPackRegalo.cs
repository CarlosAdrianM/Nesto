using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modules.Producto.Models
{
    /// <summary>
    /// Sugerencia 780 (Laura, aprobada por Carlos 11/11/26): los productos de «Pack Regalo» (COS/PRG) no pueden estar en
    /// ninguna otra categoría web, y Pack Regalo tiene que ser la categoría PRINCIPAL (el subgrupo de la ficha), nunca
    /// una secundaria. Aquí solo se impide AÑADIR lo que la incumple; lo que ya la incumple se avisa (<see cref="Aviso"/>)
    /// y se puede quitar y guardar. La API de hoy no lo valida (ver el informe de la sugerencia).
    /// <para>EXCEPCIÓN: el Outlet manda (Carlos 11/11/26). Un Pack Regalo puede tener además las categorías de Outlet
    /// (PEL/OUT, COS/OUM y COS/OUT, las de ReglasOutlet de NestoAPI), que el cuadre nocturno del Outlet pone él solo a
    /// los productos en estado 4. Tener solo esas no es incumplir la regla.</para>
    /// </summary>
    public static class ReglaPackRegalo
    {
        public const string GRUPO = "COS";
        public const string SUBGRUPO = "PRG";

        /// <summary>Las categorías de Outlet (las mismas que ReglasOutlet.TODAS de NestoAPI).</summary>
        private static readonly (string Grupo, string Subgrupo)[] CATEGORIAS_OUTLET =
        {
            ("PEL", "OUT"), // Outlet Peluquería
            ("COS", "OUM"), // Outlet Maquillaje y Uñas
            ("COS", "OUT")  // Outlet Estética
        };

        internal const string MOTIVO_PRINCIPAL_PACK_REGALO =
            "Este producto es un Pack Regalo (COS/PRG): los packs regalo no pueden estar en ninguna otra categoría web, " +
            "salvo en las de Outlet.";
        internal const string MOTIVO_PACK_REGALO_COMO_SECUNDARIA =
            "Pack Regalo (COS/PRG) no se puede poner como categoría secundaria: tiene que ser la categoría principal " +
            "del producto (el subgrupo de la ficha).";
        internal const string AVISO_PRINCIPAL_CON_SECUNDARIAS =
            "Atención: es un Pack Regalo (COS/PRG) y no debería estar en ninguna otra categoría salvo las de Outlet. " +
            "Quite las demás de la lista y guarde.";
        internal const string AVISO_PACK_REGALO_COMO_SECUNDARIA =
            "Atención: tiene Pack Regalo (COS/PRG) como categoría secundaria. Pack Regalo tiene que ser la categoría " +
            "principal (el subgrupo de la ficha), no una secundaria.";

        public static bool EsPackRegalo(string grupo, string subgrupo)
            => string.Equals(grupo?.Trim(), GRUPO, StringComparison.OrdinalIgnoreCase)
            && string.Equals(subgrupo?.Trim(), SUBGRUPO, StringComparison.OrdinalIgnoreCase);

        /// <summary>¿Es una de las categorías de Outlet (PEL/OUT, COS/OUM, COS/OUT)?</summary>
        public static bool EsCategoriaOutlet(string grupo, string subgrupo)
            => CATEGORIAS_OUTLET.Any(c => string.Equals(grupo?.Trim(), c.Grupo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(subgrupo?.Trim(), c.Subgrupo, StringComparison.OrdinalIgnoreCase));

        /// <summary>Por qué no se puede añadir esa secundaria a un producto con esa principal; null si se puede.</summary>
        public static string MotivoNoSePuedeAnnadir(string grupoPrincipal, string subgrupoPrincipal, string grupo, string subgrupo)
        {
            if (EsPackRegalo(grupo, subgrupo))
            {
                return MOTIVO_PACK_REGALO_COMO_SECUNDARIA;
            }
            if (EsPackRegalo(grupoPrincipal, subgrupoPrincipal) && !EsCategoriaOutlet(grupo, subgrupo))
            {
                return MOTIVO_PRINCIPAL_PACK_REGALO;
            }
            return null;
        }

        /// <summary>Si lo que tiene ya incumple la regla, el aviso para la pantalla; null si la cumple.</summary>
        public static string Aviso(string grupoPrincipal, string subgrupoPrincipal, IEnumerable<CategoriaSecundariaModel> secundarias)
        {
            List<CategoriaSecundariaModel> lista = (secundarias ?? Enumerable.Empty<CategoriaSecundariaModel>())
                .Where(c => c != null)
                .ToList();
            if (EsPackRegalo(grupoPrincipal, subgrupoPrincipal))
            {
                return lista.Any(c => !EsCategoriaOutlet(c.Grupo, c.Subgrupo)) ? AVISO_PRINCIPAL_CON_SECUNDARIAS : null;
            }
            return lista.Any(c => c.EsLaMisma(GRUPO, SUBGRUPO)) ? AVISO_PACK_REGALO_COMO_SECUNDARIA : null;
        }
    }
}
