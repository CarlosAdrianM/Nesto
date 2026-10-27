using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Nesto.Infrastructure.Menu
{
    /// <summary>Por dónde ha coincidido un resultado de la búsqueda (de mejor a peor: así se ordenan).</summary>
    public enum CoincidenciaBusquedaMenu
    {
        /// <summary>El título empieza por lo buscado («pedi» → «Pedidos de venta»).</summary>
        InicioDelTitulo = 0,
        /// <summary>Todas las palabras buscadas están en el título.</summary>
        Titulo = 1,
        /// <summary>Están en la ruta «Sección › Título» («ventas plantilla»).</summary>
        Ruta = 2,
        /// <summary>
        /// Solo están en dónde estaba en la cinta («Pestaña › Grupo › Botón»): quien busca «herramientas» o «proveedores»
        /// por costumbre, también lo encuentra.
        /// </summary>
        Cinta = 3,
    }

    /// <summary>Un resultado de la búsqueda del menú lateral: el elemento y la ruta que se enseña.</summary>
    public sealed class ResultadoBusquedaMenu
    {
        public ResultadoBusquedaMenu(ElementoMenu elemento, string seccion, string titulo, CoincidenciaBusquedaMenu coincidencia)
        {
            Elemento = elemento ?? throw new ArgumentNullException(nameof(elemento));
            Seccion = seccion ?? string.Empty;
            Titulo = titulo ?? string.Empty;
            Coincidencia = coincidencia;
        }

        public ElementoMenu Elemento { get; }

        public string Seccion { get; }

        public string Titulo { get; }

        /// <summary>«Sección › Título»: hay títulos que se repiten en la cinta («Ficha», «Pedidos»), la ruta los distingue.</summary>
        public string Ruta => string.IsNullOrEmpty(Seccion) ? Titulo : Seccion + MapaMenuPorRol.SEPARADOR + Titulo;

        public CoincidenciaBusquedaMenu Coincidencia { get; }

        public override string ToString() => Ruta;
    }

    /// <summary>
    /// Nesto#490 (panel lateral, tramo 2): la búsqueda «Buscar pantalla…» del menú lateral, sin nada de WPF-UI (se prueba
    /// sin ventana). Busca en TODO el catálogo (secciones y el pie: Parámetros y Novedades) por el título y por la ruta
    /// completa «Sección › Título», sin distinguir mayúsculas ni tildes («verifactu», «familias», «remesa»).
    /// <list type="bullet">
    /// <item>Solo salen los elementos que el usuario puede usar: los que el panel muestra (<see cref="ElementoMenuBase.Mostrar"/>:
    /// visibles y con CanExecute). «Opciones de los informes» no es una pantalla: no sale.</item>
    /// <item>Varias palabras: tienen que estar todas, en cualquier orden.</item>
    /// <item>Orden: primero los que empiezan por lo buscado, después los que lo tienen en el título, después en la ruta y,
    /// al final, los que solo coinciden por dónde estaban en la cinta. A igualdad, el orden del panel.</item>
    /// </list>
    /// </summary>
    public static class FiltroCatalogoMenu
    {
        /// <summary>La «sección» con la que salen en la búsqueda los del pie (Parámetros y Novedades).</summary>
        public const string SECCION_PIE = "Nesto";

        private static readonly char[] _separadores = { ' ', '\t', '›', '>', '/', '\\', ',', ';', '.', ':', '-', '(', ')', '«', '»', '"', '\'' };

        /// <summary>Los candidatos, en el orden del panel: sección a sección (como se ven) y, al final, el pie.</summary>
        public static IEnumerable<(ElementoMenu Elemento, string Seccion)> Candidatos(CatalogoMenu catalogo)
        {
            if (catalogo == null)
            {
                yield break;
            }
            var vistos = new HashSet<ElementoMenu>();
            foreach (SeccionMenu seccion in catalogo.Secciones)
            {
                foreach (ElementoMenu elemento in seccion.Elementos.OfType<ElementoMenu>())
                {
                    if (vistos.Add(elemento))
                    {
                        yield return (elemento, seccion.Titulo);
                    }
                }
            }
            foreach (ElementoMenu elemento in catalogo.MenuAplicacion)
            {
                if (vistos.Add(elemento))
                {
                    yield return (elemento, SECCION_PIE);
                }
            }
        }

        /// <summary>Lo que coincide con <paramref name="texto"/>, ya ordenado. Sin texto (o solo espacios), nada.</summary>
        public static IReadOnlyList<ResultadoBusquedaMenu> Filtrar(CatalogoMenu catalogo, string texto)
            => Filtrar(Candidatos(catalogo), texto);

        public static IReadOnlyList<ResultadoBusquedaMenu> Filtrar(IEnumerable<(ElementoMenu Elemento, string Seccion)> candidatos, string texto)
        {
            string[] palabras = Palabras(texto);
            if (palabras.Length == 0 || candidatos == null)
            {
                return Array.Empty<ResultadoBusquedaMenu>();
            }
            string buscado = string.Join(" ", palabras);

            var resultados = new List<ResultadoBusquedaMenu>();
            foreach ((ElementoMenu elemento, string seccion) in candidatos)
            {
                if (elemento == null || !elemento.Mostrar)
                {
                    continue;
                }
                string titulo = Limpiar(elemento.Titulo);
                string tituloNormalizado = Normalizar(titulo);
                CoincidenciaBusquedaMenu? coincidencia = null;
                if (tituloNormalizado.StartsWith(buscado, StringComparison.Ordinal))
                {
                    coincidencia = CoincidenciaBusquedaMenu.InicioDelTitulo;
                }
                else if (TieneTodas(tituloNormalizado, palabras))
                {
                    coincidencia = CoincidenciaBusquedaMenu.Titulo;
                }
                else if (TieneTodas(Normalizar(seccion + " " + titulo), palabras))
                {
                    coincidencia = CoincidenciaBusquedaMenu.Ruta;
                }
                else if (TieneTodas(Normalizar(seccion + " " + titulo + " " + elemento.Clave), palabras))
                {
                    coincidencia = CoincidenciaBusquedaMenu.Cinta;
                }
                if (coincidencia.HasValue)
                {
                    resultados.Add(new ResultadoBusquedaMenu(elemento, seccion, titulo, coincidencia.Value));
                }
            }
            // OrderBy es estable: a igualdad, el orden del panel
            return resultados.OrderBy(r => r.Coincidencia).ToList();
        }

        /// <summary>Sin tildes ni diéresis, en minúsculas y sin el «_» de los aceleradores. «Vídeos» → «videos».</summary>
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return string.Empty;
            }
            string descompuesto = Limpiar(texto).Normalize(NormalizationForm.FormD);
            var resultado = new StringBuilder(descompuesto.Length);
            foreach (char caracter in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                {
                    _ = resultado.Append(caracter);
                }
            }
            return resultado.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        private static string Limpiar(string texto) => (texto ?? string.Empty).Replace("_", string.Empty).Trim();

        private static string[] Palabras(string texto)
            => Normalizar(texto).Split(_separadores, StringSplitOptions.RemoveEmptyEntries);

        private static bool TieneTodas(string donde, string[] palabras)
        {
            foreach (string palabra in palabras)
            {
                if (donde.IndexOf(palabra, StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
