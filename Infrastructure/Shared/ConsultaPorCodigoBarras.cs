using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#647 (cola, Carlos 26/11/26): la llamada GET api/Productos/PorCodigoBarras (NestoAPI#605: todos los códigos de barras
    /// activos de un producto, también el principal de la ficha; una fila por producto y código) escrita UNA vez. Vive aquí, en
    /// Infrastructure, para que la usen también los servicios de este proyecto (recepción de reposiciones), que no ven
    /// ControlesUsuario; el núcleo de las pantallas, <c>ControlesUsuario.Services.ElectorProductoCodigoBarras</c>, la usa por dentro.
    /// Cada servicio decide qué hace si la API no contesta bien.
    /// </summary>
    public static class ConsultaPorCodigoBarras
    {
        /// <summary>Una fila de la respuesta (lo que se usa de ella).</summary>
        public sealed class Fila
        {
            public string Producto { get; set; }
            public string Nombre { get; set; }
        }

        /// <summary>PURA. La ruta relativa de la llamada, con el código y la empresa escapados.</summary>
        public static string Ruta(string codigo, string empresa)
            => $"Productos/PorCodigoBarras?codigo={Uri.EscapeDataString(codigo?.Trim() ?? string.Empty)}&empresa={Uri.EscapeDataString(empresa?.Trim() ?? string.Empty)}";

        /// <summary>
        /// PURA. Los productos del cuerpo de la respuesta: uno por referencia (sin repetir, sin mayúsculas), sin espacios de relleno y
        /// sin filas vacías. Lista vacía si el cuerpo está vacío. Lanza <see cref="JsonException"/> si no es una lista JSON.
        /// </summary>
        public static List<Fila> LeerFilas(string cuerpo)
        {
            if (string.IsNullOrWhiteSpace(cuerpo))
            {
                return new List<Fila>();
            }
            List<Fila> filas = JsonConvert.DeserializeObject<List<Fila>>(cuerpo) ?? new List<Fila>();
            return filas
                .Where(f => f != null && !string.IsNullOrWhiteSpace(f.Producto))
                .GroupBy(f => f.Producto.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new Fila { Producto = g.Key, Nombre = g.First().Nombre?.Trim() })
                .ToList();
        }
    }
}
