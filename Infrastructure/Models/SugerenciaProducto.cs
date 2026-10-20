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
    }

    /// <summary>Nesto#537: lo que contesta el vendedor dentro de la ficha («✓ Añadir» y «✗ No le interesa»).</summary>
    public static class RespuestasSugerenciaProducto
    {
        public const string ANADIDO = "Anadido";
        public const string NO_LE_INTERESA = "NoLeInteresa";
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
        /// <summary>"CompradoJunto", "LeToca" o "Potenciar".</summary>
        public string Motivo { get; set; }
        /// <summary>La frase que puede decir el vendedor («Lo compra cada ~45 días; el último, hace 52.»).</summary>
        public string Texto { get; set; }
        /// <summary>1..4: el hueco del panel (la sustituta ocupa el de la rechazada).</summary>
        public int Posicion { get; set; }

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
                    default:
                        return "💡";
                }
            }
        }

        /// <summary>«Comprado junto», «Ya le toca» o «A potenciar».</summary>
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

        /// <summary>El tooltip: producto, nombre entero, motivo y la frase.</summary>
        public string Ayuda
        {
            get
            {
                string cabecera = $"{Producto?.Trim()} {Nombre?.Trim()}".Trim();
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
        /// <summary>"Anadido" o "NoLeInteresa".</summary>
        public string Respuesta { get; set; }
        /// <summary>El carrito de ahora (opcional: sin él la API usa el del último Calcular).</summary>
        public List<ProductoCarritoSugerenciaDTO> Productos { get; set; }
    }

    /// <summary>Nesto#537: respuesta de .../Respuesta.</summary>
    public class ResultadoRespuestaSugerenciaProductoDTO
    {
        public int IdSugerencia { get; set; }
        public string Respuesta { get; set; }
        /// <summary>Con «NoLeInteresa», la que ocupa su hueco. Null si no hay otra (o con «Anadido»).</summary>
        public SugerenciaProductoDTO Sustituta { get; set; }
        public List<SugerenciaProductoDTO> Sugerencias { get; set; } = new List<SugerenciaProductoDTO>();
    }
}
