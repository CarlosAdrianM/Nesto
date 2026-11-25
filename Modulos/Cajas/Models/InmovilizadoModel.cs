using System;
using System.Collections.Generic;

namespace Nesto.Modulos.Cajas.Models
{
    /// <summary>
    /// Inmovilizados (Carlos 25/11/26): una fila de GET api/Inmovilizados. <see cref="Codigo"/> es la nomenclatura de la casa
    /// («I/1823»). Los campos son tolerantes: lo que falte se queda vacío.
    /// </summary>
    public class InmovilizadoModel
    {
        public string? Empresa { get; set; }
        public string? Numero { get; set; }
        public string? Codigo { get; set; }
        public string? Descripcion { get; set; }
        public string? Grupo { get; set; }
        public string? Delegacion { get; set; }
        public string? Estado { get; set; }
        public string? EstadoDescripcion { get; set; }
        public DateTime? FechaCreacion { get; set; }

        /// <summary>El código de la API o, si no viene, «I/» + el número.</summary>
        public string CodigoParaMostrar => !string.IsNullOrWhiteSpace(Codigo)
            ? Codigo!.Trim()
            : Nesto.Infrastructure.Shared.CodigoInmovilizado.Formatear(Numero);

        /// <summary>La descripción del estado o, si no viene, el estado tal cual.</summary>
        public string EstadoParaMostrar => !string.IsNullOrWhiteSpace(EstadoDescripcion)
            ? EstadoDescripcion!.Trim()
            : Estado?.Trim() ?? string.Empty;
    }

    /// <summary>El cuerpo de POST api/Inmovilizados/Etiquetas/Imprimir.</summary>
    public class PeticionImprimirEtiquetasInmovilizado
    {
        public string Empresa { get; set; } = string.Empty;
        public List<string> Numeros { get; set; } = new List<string>();
        public int Copias { get; set; } = 1;
        /// <summary>Sin impresora, la API elige la de la delegación del inmovilizado (ALG/REI/ALC) o, si no, la del usuario.</summary>
        public string? Impresora { get; set; }
    }

    /// <summary>La respuesta de POST api/Inmovilizados/Etiquetas/Imprimir.</summary>
    public class RespuestaImprimirEtiquetasInmovilizado
    {
        public int Impresas { get; set; }
        public List<EtiquetaInmovilizadoImpresa> Etiquetas { get; set; } = new List<EtiquetaInmovilizadoImpresa>();
    }

    public class EtiquetaInmovilizadoImpresa
    {
        public string? Numero { get; set; }
        public string? Codigo { get; set; }
        public string? Descripcion { get; set; }
        public string? Delegacion { get; set; }
        public string? Impresora { get; set; }
        public bool Ok { get; set; }
        public string? Motivo { get; set; }
    }
}
