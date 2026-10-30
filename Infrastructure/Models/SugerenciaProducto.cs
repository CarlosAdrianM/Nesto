using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Nesto#537 (épica NestoAPI#669, API NestoAPI#670): de dónde sale una sugerencia de producto, tal cual la manda la API.
    /// </summary>
    public static class MotivosSugerenciaProducto
    {
        public const string COMPRADO_JUNTO = "CompradoJunto";
        public const string LE_TOCA = "LeToca";
        public const string POTENCIAR = "Potenciar";
        /// <summary>NestoAPI#671: lo compran clientes que compran lo mismo que este.</summary>
        public const string PARECIDOS = "Parecidos";
    }

    /// <summary>Nesto#537: lo que contesta el vendedor dentro de la ficha («✓ Añadir», «✗ No le interesa» y «🚫 Quitar»).</summary>
    public static class RespuestasSugerenciaProducto
    {
        public const string ANADIDO = "Anadido";
        /// <summary>Se lo ha ofrecido y el cliente no lo quiere.</summary>
        public const string NO_LE_INTERESA = "NoLeInteresa";
        /// <summary>
        /// «🚫 Quitar»: el vendedor no se lo ofrece (sabe que no lo quiere o no se lo puede vender). No se le vuelve a sugerir a
        /// ese cliente en 60 días, sale otra en su lugar y la API lo registra aparte del rechazo del cliente.
        /// </summary>
        public const string DESCARTADO_VENDEDOR = "DescartadoVendedor";
    }

    public class ProductoCarritoSugerenciaDTO
    {
        public string Producto { get; set; }
        public int Cantidad { get; set; }
    }

    /// <summary>Nesto#537: cuerpo de POST api/SugerenciasProductos/Calcular.</summary>
    public class CalcularSugerenciasProductosDTO
    {
        public const string APLICACION_NESTO = "Nesto";

        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        /// <summary>El vendedor que hace el pedido: la API enseña solo productos de su grupo.</summary>
        public string Vendedor { get; set; }
        /// <summary>La sesión de esta plantilla. Null la primera vez (la API la crea y la devuelve).</summary>
        public Guid? IdSesion { get; set; }
        public string Aplicacion { get; set; } = APLICACION_NESTO;
        public List<ProductoCarritoSugerenciaDTO> Productos { get; set; } = new List<ProductoCarritoSugerenciaDTO>();
    }

    /// <summary>Nesto#537: una ficha del panel «Puede ofrecer».</summary>
    public class SugerenciaProductoDTO
    {
        /// <summary>Lo que cabe en la ficha; el nombre entero va en el tooltip.</summary>
        public const int LARGO_NOMBRE_CORTO = 26;

        public int IdSugerencia { get; set; }
        public string Producto { get; set; }
        public string Nombre { get; set; }
        /// <summary>"CompradoJunto", "LeToca", "Potenciar" o "Parecidos".</summary>
        public string Motivo { get; set; }
        /// <summary>La frase que puede decir el vendedor («Lo compra cada ~45 días; el último, hace 52.»).</summary>
        public string Texto { get; set; }
        /// <summary>1..4: el hueco del panel (la sustituta ocupa el de la rechazada).</summary>
        public int Posicion { get; set; }

        // Nesto#537 (segunda vuelta): lo que la API manda para la tarjeta. Opcional: una API anterior no lo manda.

        /// <summary>La foto de la tienda. Null si no tiene.</summary>
        public string UrlFoto { get; set; }
        public short? Tamano { get; set; }
        public string UnidadMedida { get; set; }
        /// <summary>La familia (su descripción).</summary>
        public string Familia { get; set; }
        /// <summary>«Crema Reafirmante 500 ml», ya formateado por la API.</summary>
        public string NombreConTamano { get; set; }

        public bool TieneFoto => !string.IsNullOrWhiteSpace(UrlFoto);

        /// <summary>
        /// El nombre de la tarjeta: el que manda la API con el tamaño; si no lo manda (API anterior), se forma aquí igual
        /// («nombre tamaño unidad»; sin tamaño, el nombre solo). Sin nombre, el código del producto.
        /// </summary>
        public string NombreMostrar
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(NombreConTamano))
                {
                    return NombreConTamano.Trim();
                }
                string nombre = string.IsNullOrWhiteSpace(Nombre) ? Producto?.Trim() : Nombre.Trim();
                if (string.IsNullOrEmpty(nombre) || !Tamano.HasValue || Tamano.Value == 0)
                {
                    return nombre;
                }
                string unidad = UnidadMedida?.Trim();
                return string.IsNullOrEmpty(unidad) ? $"{nombre} {Tamano.Value}" : $"{nombre} {Tamano.Value} {unidad}";
            }
        }

        /// <summary>La línea pequeña de la tarjeta: el código y la familia («38000 · Lisap Milano»).</summary>
        public string ProductoYFamilia
        {
            get
            {
                string producto = Producto?.Trim();
                string familia = Familia?.Trim();
                if (string.IsNullOrEmpty(familia))
                {
                    return producto;
                }
                return string.IsNullOrEmpty(producto) ? familia : $"{producto} · {familia}";
            }
        }

        public string Icono
        {
            get
            {
                switch (Motivo)
                {
                    case MotivosSugerenciaProducto.COMPRADO_JUNTO:
                        return "🔗";
                    case MotivosSugerenciaProducto.LE_TOCA:
                        return "🔁";
                    case MotivosSugerenciaProducto.POTENCIAR:
                        return "⭐";
                    case MotivosSugerenciaProducto.PARECIDOS:
                        return "👥";
                    default:
                        return "💡";
                }
            }
        }

        /// <summary>«Comprado junto», «Ya le toca», «A potenciar» o «Clientes parecidos».</summary>
        public string NombreMotivo
        {
            get
            {
                switch (Motivo)
                {
                    case MotivosSugerenciaProducto.COMPRADO_JUNTO:
                        return "Comprado junto";
                    case MotivosSugerenciaProducto.LE_TOCA:
                        return "Ya le toca";
                    case MotivosSugerenciaProducto.POTENCIAR:
                        return "A potenciar";
                    case MotivosSugerenciaProducto.PARECIDOS:
                        return "Clientes parecidos";
                    default:
                        return "Sugerencia";
                }
            }
        }

        public string NombreCorto
        {
            get
            {
                string nombre = string.IsNullOrWhiteSpace(Nombre) ? Producto?.Trim() : Nombre.Trim();
                if (nombre == null || nombre.Length <= LARGO_NOMBRE_CORTO)
                {
                    return nombre;
                }
                return nombre.Substring(0, LARGO_NOMBRE_CORTO - 1).TrimEnd() + "…";
            }
        }

        /// <summary>El tooltip: producto, nombre entero (con el tamaño), familia, motivo y la frase.</summary>
        public string Ayuda
        {
            get
            {
                string cabecera = $"{Producto?.Trim()} {NombreMostrar?.Trim()}".Trim();
                if (Producto != null && string.Equals(NombreMostrar?.Trim(), Producto.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    cabecera = Producto.Trim();
                }
                if (!string.IsNullOrWhiteSpace(Familia))
                {
                    cabecera = string.IsNullOrEmpty(cabecera) ? Familia.Trim() : $"{cabecera} ({Familia.Trim()})";
                }
                string motivo = string.IsNullOrWhiteSpace(Texto) ? NombreMotivo : $"{NombreMotivo}: {Texto.Trim()}";
                return string.IsNullOrEmpty(cabecera) ? motivo : cabecera + Environment.NewLine + motivo;
            }
        }
    }

    /// <summary>Nesto#537: respuesta de Calcular.</summary>
    public class SugerenciasProductosDTO
    {
        public Guid IdSesion { get; set; }
        public List<SugerenciaProductoDTO> Sugerencias { get; set; } = new List<SugerenciaProductoDTO>();
    }

    /// <summary>Nesto#537: cuerpo de POST api/SugerenciasProductos/{IdSugerencia}/Respuesta.</summary>
    public class RespuestaSugerenciaProductoDTO
    {
        /// <summary>"Anadido", "NoLeInteresa" o "DescartadoVendedor".</summary>
        public string Respuesta { get; set; }
        /// <summary>El carrito de ahora (opcional: sin él la API usa el del último Calcular).</summary>
        public List<ProductoCarritoSugerenciaDTO> Productos { get; set; }
    }

    /// <summary>
    /// NestoAPI#751: el resumen personal del vendedor (GET api/SugerenciasProductos/MiResumen) para la cabecera de «Puede
    /// ofrecer». Solo lo suyo; los textos los monta la API (los mismos en NestoApp). Aquí solo se usan la frase y la ayuda.
    /// </summary>
    public class ResumenSugerenciasVendedorDTO
    {
        public int? AceptadasDeCadaDiez { get; set; }
        /// <summary>Una línea: «Esta semana: 3 productos añadidos desde las sugerencias, 120,00 € en ventas. Cuanto más marque...».</summary>
        public string Frase { get; set; }
        /// <summary>La evolución por semanas y para qué sirve marcar (va en la ayuda de la cabecera).</summary>
        public string Ayuda { get; set; }
    }

    /// <summary>Nesto#537: respuesta de .../Respuesta.</summary>
    public class ResultadoRespuestaSugerenciaProductoDTO
    {
        public int IdSugerencia { get; set; }
        public string Respuesta { get; set; }
        /// <summary>Con «NoLeInteresa» o «DescartadoVendedor», la que ocupa su hueco. Null si no hay otra (o con «Anadido»).</summary>
        public SugerenciaProductoDTO Sustituta { get; set; }
        public List<SugerenciaProductoDTO> Sugerencias { get; set; } = new List<SugerenciaProductoDTO>();
    }
}
