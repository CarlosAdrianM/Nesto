using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Models
{
    // NestoAPI#553: recibir una reposición en la tienda con el mismo contrato que Ariadna
    // (api/Almacen/Recepciones, tipo REPO). Espejo de RecepcionesDTO.cs de NestoAPI.

    /// <summary>Una reposición por recibir en el almacén (GET api/Almacen/Recepciones, solo las de tipo REPO).</summary>
    public class RecepcionPendiente
    {
        public string Tipo { get; set; }
        /// <summary>El número de traspaso.</summary>
        public string Documento { get; set; }
        /// <summary>«Reposición 80878».</summary>
        public string Titulo { get; set; }
        public DateTime? Fecha { get; set; }
        public int Lineas { get; set; }
        public int Unidades { get; set; }
    }

    public class LineaRecepcionReposicion
    {
        public string Producto { get; set; }
        public string Descripcion { get; set; }
        /// <summary>El código de barras principal (el de la ficha). Null si no tiene: <see cref="SinCodigo"/>.</summary>
        public string CodigoBarras { get; set; }
        /// <summary>
        /// NestoAPI#605: todos los códigos activos del producto (el principal, el primero). Vacía si no tiene o si la API es
        /// anterior a NestoAPI#605: entonces se casa solo por el principal.
        /// </summary>
        public List<string> CodigosBarras { get; set; } = new List<string>();
        public bool SinCodigo { get; set; }
        public bool CodigoDuplicado { get; set; }
        /// <summary>Unidades enviadas.</summary>
        public int Cantidad { get; set; }
    }

    /// <summary>GET api/Almacen/Recepciones/REPO/{traspaso}.</summary>
    public class RecepcionReposicion
    {
        public string Tipo { get; set; }
        public string Documento { get; set; }
        public string Titulo { get; set; }
        public string Almacen { get; set; }
        /// <summary>Quien pregunta la puede terminar (su AlmacénPedidoVta es el de destino).</summary>
        public bool PuedeTerminar { get; set; }
        public bool SeTerminaDesdeAqui { get; set; }
        public List<LineaRecepcionReposicion> Lineas { get; set; } = new List<LineaRecepcionReposicion>();

        // Nesto#515 (NestoAPI#600): los textos de la confirmación, del tipo. Null con una API anterior: se usan los de antes.

        /// <summary>«¿Terminar la reposición 80905 con esto?».</summary>
        public string TituloConfirmacion { get; set; }
        /// <summary>Lo leído coincide con lo enviado.</summary>
        public string AvisoCoincide { get; set; }
        /// <summary>Lo leído no coincide (faltas, sobras o productos que no venían).</summary>
        public string AvisoNoCoincide { get; set; }
        /// <summary>Además, si falta algo. Null: nada que añadir.</summary>
        public string AvisoConFaltas { get; set; }
        /// <summary>Además, si sobra algo. Null: nada que añadir.</summary>
        public string AvisoConSobras { get; set; }
        /// <summary>Además, si algo de lo que sobra es lo dado por no servido (no aplica a reposiciones). Null: nada que añadir.</summary>
        public string AvisoConRecuperadas { get; set; }
        /// <summary>Además, si se ha leído algo que no venía. Null: nada que añadir.</summary>
        public string AvisoConAjenos { get; set; }
    }

    public class LecturaRecepcionReposicion
    {
        public string Producto { get; set; }
        public int Cantidad { get; set; }
    }

    /// <summary>POST api/Almacen/Recepciones/REPO/{traspaso}/Terminar.</summary>
    public class TerminarRecepcionReposicion
    {
        /// <summary>Una vez por recepción: si la respuesta se pierde y se reenvía, no se recibe dos veces.</summary>
        public Guid IdRecepcion { get; set; }
        public List<LecturaRecepcionReposicion> Lecturas { get; set; } = new List<LecturaRecepcionReposicion>();
        public string Dispositivo { get; set; }
    }

    public class DiferenciaRecepcionReposicion
    {
        public string Producto { get; set; }
        public string Descripcion { get; set; }
        public int Esperado { get; set; }
        public int Leido { get; set; }
        public int Diferencia => Leido - Esperado;
        /// <summary>No venía en la reposición.</summary>
        public bool Ajeno { get; set; }
    }

    public class ResultadoRecepcionReposicion
    {
        public string Documento { get; set; }
        public bool YaEstabaTerminada { get; set; }
        /// <summary>Lo que no coincide con lo enviado: ha entrado lo leído.</summary>
        public List<DiferenciaRecepcionReposicion> Diferencias { get; set; } = new List<DiferenciaRecepcionReposicion>();
        /// <summary>A quién se ha informado de las diferencias (quien creó el traspaso).</summary>
        public string AvisadoA { get; set; }
        public List<string> Avisos { get; set; } = new List<string>();
        /// <summary>Nesto#515 (NestoAPI#600): «Lo recibido ya aparece en Ubicar». Ya va dentro de <see cref="Mensaje"/>.</summary>
        public string AvisoUbicar { get; set; }
        /// <summary>
        /// Nesto#515 (NestoAPI#600): todo lo que hay que enseñar al terminar, ya montado por el servidor (diferencias, a quién
        /// se ha avisado, Ubicar…). Null con una API anterior: se monta aquí.
        /// </summary>
        public string Mensaje { get; set; }
    }

    /// <summary>La API no ha aceptado la petición: el motivo que da (sin permiso, ya terminada…).</summary>
    public class RecepcionReposicionException : Exception
    {
        public RecepcionReposicionException(string motivo) : base(motivo) { }
    }
}
