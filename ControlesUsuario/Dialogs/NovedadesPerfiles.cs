using System;
using System.Collections.Generic;
using System.Linq;
using Nesto.Infrastructure.Contracts;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Sugerencia 551 (Alberto Sancho): lo que necesitan las novedades para saber a quién afectan. Una instancia por
    /// ventana, compartida por todas las novedades. Los perfiles del usuario llegan de la API después de abrir la
    /// ventana: hasta entonces (o si la API no los conoce) todas cuentan como suyas. A quién afecta cada novedad se pone
    /// por script, como los adjuntos (decisión de Carlos, 09/10/26): aquí no se edita.
    /// </summary>
    public class ContextoPerfilesNovedades
    {
        /// <summary>null = todavía sin saber (o la API no lo sabe): no se filtra nada.</summary>
        public PerfilesUsuarioNovedades MisPerfiles { get; internal set; }

        /// <summary>Se le filtra algo: tiene perfiles y no las ve todas por su puesto.</summary>
        public bool TieneFiltro => MisPerfiles != null && !MisPerfiles.VeTodas && (MisPerfiles.Perfiles?.Count ?? 0) > 0;

        /// <summary>Si una novedad con esos perfiles sale por defecto (sin perfiles = para todos).</summary>
        public bool EsParaMi(IReadOnlyCollection<string> perfilesNovedad)
        {
            if (!TieneFiltro || perfilesNovedad == null || perfilesNovedad.Count == 0)
            {
                return true;
            }
            return perfilesNovedad.Any(p => MisPerfiles.Perfiles.Any(m => string.Equals(m, p, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>«Almacén», «Almacén y Tiendas», «Vendedores, Almacén y Tiendas».</summary>
        internal static string Unir(IReadOnlyList<string> perfiles)
        {
            if (perfiles == null || perfiles.Count == 0)
            {
                return string.Empty;
            }
            return perfiles.Count == 1
                ? perfiles[0]
                : string.Join(", ", perfiles.Take(perfiles.Count - 1)) + " y " + perfiles[perfiles.Count - 1];
        }
    }

    /// <summary>
    /// Sugerencia 551: a quién afecta una novedad («Para: Almacén y Tiendas»). Sin perfiles es para todos y no se dice
    /// nada. Solo lectura: los perfiles se ponen por script.
    /// </summary>
    public class PerfilesNovedadItem
    {
        public PerfilesNovedadItem(IEnumerable<string> perfiles)
        {
            Actuales = (perfiles ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Vacía = para todos.</summary>
        public IReadOnlyList<string> Actuales { get; }

        /// <summary>«Para: Almacén y Tiendas». Sin perfiles, nada.</summary>
        public string Texto => Actuales.Count > 0 ? "Para: " + ContextoPerfilesNovedades.Unir(Actuales) : null;

        public bool HayTexto => !string.IsNullOrEmpty(Texto);
    }
}
