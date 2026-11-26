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

        /// <summary>
        /// Nesto#647 (pedido de compra y plantilla, Carlos 26/11/26): lo tecleado o leído tiene forma de código de barras (EAN-8,
        /// UPC-A, EAN-13 o GTIN-14: solo cifras, de 8 a 14). Las referencias de Nesto son más cortas, así que no se confunden.
        /// </summary>
        public static bool PareceCodigoBarras(string texto)
        {
            string limpio = texto?.Trim();
            return !string.IsNullOrEmpty(limpio) && limpio.Length >= 8 && limpio.Length <= 14 && limpio.All(char.IsDigit);
        }

        /// <summary>
        /// Los productos de un código de barras: GET api/Productos/PorCodigoBarras (mira todos los códigos activos, también el principal
        /// de la ficha; NestoAPI#605), una fila por producto. La MISMA llamada para todas las pantallas que no reciben los candidatos en
        /// un 409. Null si la API no contesta bien (quien llama sigue como si no fuera un código).
        /// </summary>
        /// <param name="client">Con BaseAddress y token (el de la fábrica común); lo libera quien lo crea.</param>
        public static async Task<List<ProductoCodigoBarrasDuplicado>> BuscarCandidatosAsync(System.Net.Http.HttpClient client, string codigo, string empresa)
        {
            if (client == null || string.IsNullOrWhiteSpace(codigo))
            {
                return new List<ProductoCodigoBarrasDuplicado>();
            }
            try
            {
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(
                    $"Productos/PorCodigoBarras?codigo={Uri.EscapeDataString(codigo.Trim())}&empresa={Uri.EscapeDataString(empresa?.Trim() ?? string.Empty)}");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                string cuerpo = await response.Content.ReadAsStringAsync();
                return SinRepetir(JsonConvert.DeserializeObject<List<ProductoCodigoBarrasDuplicado>>(cuerpo));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElectorProductoCodigoBarras] PorCodigoBarras: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lo que hay que buscar cuando lo tecleado puede ser un código de barras: si no lo parece o no es de ningún producto, el texto
        /// tal cual; si es de uno, esa referencia; si es de varios, la que elija el usuario en el selector; null si cancela (no se busca nada).
        /// </summary>
        public static async Task<string> ReferenciaDeLoTecleadoAsync(IServicioDialogos dialogos, string texto,
            Func<string, Task<List<ProductoCodigoBarrasDuplicado>>> buscarCandidatos)
        {
            if (!PareceCodigoBarras(texto) || buscarCandidatos == null)
            {
                return texto;
            }
            List<ProductoCodigoBarrasDuplicado> candidatos = SinRepetir(await buscarCandidatos(texto.Trim()));
            if (candidatos.Count == 0)
            {
                return texto;
            }
            return await ElegirAsync(dialogos, candidatos, texto);
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
