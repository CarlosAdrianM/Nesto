using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#558 (NestoAPI#716, 2.1): resultado del rapport / motivo de no compra. Copia EXACTA del catálogo de la API
    /// (<c>NestoAPI/Infraestructure/Rapports/ResultadoRapport.cs</c>): los códigos son los que se mandan en
    /// <c>SeguimientoClienteDTO.Resultado</c> y los nombres los que se pintan en los chips. Un solo sitio para todas las
    /// pantallas; si la API añade un valor, se añade aquí.
    /// </summary>
    public static class ResultadoRapport
    {
        public const string PEDIDO = "Pedido";
        public const string PEDIRA_MAS_TARDE = "PediraMasTarde";
        public const string NO_NECESITA = "NoNecesita";
        public const string PRECIO = "Precio";
        public const string COMPETENCIA = "Competencia";
        public const string CERRADO_TRASPASO = "CerradoTraspaso";
        public const string SIN_STOCK = "SinStock";
        public const string NO_DECIDE = "NoDecide";
        public const string SOLO_INFORMACION = "SoloInformacion";
        public const string OTRO = "Otro";

        /// <summary>Código → nombre para mostrar.</summary>
        public static readonly IReadOnlyDictionary<string, string> Nombres = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            { PEDIDO, "Pedido" },
            { PEDIRA_MAS_TARDE, "Pedirá más tarde" },
            { NO_NECESITA, "No necesita" },
            { PRECIO, "Precio" },
            { COMPETENCIA, "Competencia" },
            { CERRADO_TRASPASO, "Cerrado / traspaso" },
            { SIN_STOCK, "Sin stock" },
            { NO_DECIDE, "No decide" },
            { SOLO_INFORMACION, "Solo información" },
            { OTRO, "Otro" }
        };

        /// <summary>Los códigos, en el orden en que se pintan los chips.</summary>
        public static readonly IReadOnlyList<string> Codigos = new ReadOnlyCollection<string>(new[]
        {
            PEDIDO, PEDIRA_MAS_TARDE, NO_NECESITA, PRECIO, COMPETENCIA, CERRADO_TRASPASO, SIN_STOCK, NO_DECIDE, SOLO_INFORMACION, OTRO
        });

        /// <summary>Las opciones (código + nombre) en orden, para enlazar los chips.</summary>
        public static readonly IReadOnlyList<OpcionCatalogo> Opciones = new ReadOnlyCollection<OpcionCatalogo>(
            Codigos.Select(c => new OpcionCatalogo(c, Nombres[c])).ToList());

        public static bool EsValido(string resultado)
            => !string.IsNullOrWhiteSpace(resultado) && Nombres.ContainsKey(resultado.Trim());

        /// <summary>El nombre para mostrar de un código; el propio código si no está en el catálogo; vacío si no hay.</summary>
        public static string NombreDe(string resultado)
        {
            if (string.IsNullOrWhiteSpace(resultado))
            {
                return string.Empty;
            }
            return Nombres.TryGetValue(resultado.Trim(), out string nombre) ? nombre : resultado.Trim();
        }
    }
}
