using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Nesto#647 (Carlos 23/11/26): cuando un código de barras leído o tecleado lo tienen varios productos, Nesto pregunta
    /// SIEMPRE cuál es, en todas las pantallas, con el mismo selector de Nesto#368 (<c>SelectorProductoDuplicadoDialog</c>).
    /// Este es el núcleo común: leer los candidatos del 409 de la API (sea la lista a secas de GET api/Productos o el
    /// {Message, Productos} de inventario, reposiciones y demás) y enseñar el selector. Cada pantalla solo decide qué hace
    /// con la referencia elegida (normalmente, repetir la misma petición con ella).
    /// </summary>
    public static class ElectorProductoCodigoBarras
    {
        public const string NOMBRE_DIALOGO = "SelectorProductoDuplicadoDialog";

        /// <summary>
        /// Los candidatos de un cuerpo de respuesta (normalmente un 409): una lista [{Producto, Nombre}] o un objeto con
        /// «Productos» (o «Candidatos»). Sin repetir referencia. Lista vacía si el cuerpo no trae candidatos o no es JSON.
        /// </summary>
        public static List<ProductoCodigoBarrasDuplicado> LeerCandidatos(string cuerpo)
        {
            if (string.IsNullOrWhiteSpace(cuerpo))
            {
                return new List<ProductoCodigoBarrasDuplicado>();
            }
            try
            {
                JToken token = JToken.Parse(cuerpo);
                JToken lista = token;
                if (token is JObject objeto)
                {
                    lista = Propiedad(objeto, "Productos") ?? Propiedad(objeto, "Candidatos");
                }
                if (!(lista is JArray array))
                {
                    return new List<ProductoCodigoBarrasDuplicado>();
                }
                return SinRepetir(array.ToObject<List<ProductoCodigoBarrasDuplicado>>());
            }
            catch (JsonException)
            {
                return new List<ProductoCodigoBarrasDuplicado>();
            }
        }

        /// <summary>Hay que preguntar: dos o más productos distintos.</summary>
        public static bool HayQuePreguntar(IEnumerable<ProductoCodigoBarrasDuplicado> candidatos)
        {
            return SinRepetir(candidatos).Count > 1;
        }

        /// <summary>
        /// La referencia elegida, o null si el usuario cancela o no hay candidatos. Con un solo candidato no se pregunta: es ese.
        /// </summary>
        public static async Task<string> ElegirAsync(IServicioDialogos dialogos, IEnumerable<ProductoCodigoBarrasDuplicado> candidatos, string codigo = null)
        {
            List<ProductoCodigoBarrasDuplicado> distintos = SinRepetir(candidatos);
            if (distintos.Count == 0)
            {
                return null;
            }
            if (distintos.Count == 1)
            {
                return distintos[0].Producto;
            }
            if (dialogos == null)
            {
                return null;
            }
            ParametrosDialogo parametros = new ParametrosDialogo
            {
                { "title", Titulo(codigo) },
                { "candidatos", distintos }
            };
            ResultadoDialogo resultado = await dialogos.ShowDialogAsync(NOMBRE_DIALOGO, parametros);
            if (resultado == null || resultado.Result != ResultadoBoton.OK || resultado.Parameters == null)
            {
                return null;
            }
            string elegido = resultado.Parameters.GetValue<string>("producto")?.Trim();
            return string.IsNullOrEmpty(elegido) ? null : elegido;
        }

        /// <summary>El título del selector.</summary>
        public static string Titulo(string codigo)
        {
            string limpio = codigo?.Trim();
            return string.IsNullOrEmpty(limpio)
                ? "Código de barras de varios productos"
                : $"El código de barras {limpio} es de varios productos";
        }

        private static List<ProductoCodigoBarrasDuplicado> SinRepetir(IEnumerable<ProductoCodigoBarrasDuplicado> candidatos)
        {
            if (candidatos == null)
            {
                return new List<ProductoCodigoBarrasDuplicado>();
            }
            return candidatos
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Producto))
                .GroupBy(c => c.Producto.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new ProductoCodigoBarrasDuplicado { Producto = g.Key, Nombre = g.First().Nombre?.Trim() })
                .ToList();
        }

        private static JToken Propiedad(JObject objeto, string nombre)
        {
            return objeto.Properties()
                .FirstOrDefault(p => string.Equals(p.Name, nombre, StringComparison.OrdinalIgnoreCase))?.Value;
        }
    }
}
