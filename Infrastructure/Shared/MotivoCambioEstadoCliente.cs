using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#558 (NestoAPI#716, 2.2): por qué cambia el estado de un cliente. Copia EXACTA del catálogo de la API
    /// (<c>NestoAPI/Infraestructure/Clientes/MotivoCambioEstadoCliente.cs</c>): los códigos viajan en
    /// <c>motivoCambioEstado</c> del PUT Clientes/ClienteComercial y en <c>MotivoCambioEstado</c> del PUT
    /// Clientes/DejarDeVisitar; los nombres son los del desplegable. Solo los manuales: los automáticos
    /// («DejarDeVisitar», «AltaConNif») los pone la API ella sola.
    /// </summary>
    public static class MotivoCambioEstadoCliente
    {
        public const string CIERRE = "Cierre";
        public const string TRASPASO = "Traspaso";
        public const string COMPETENCIA = "Competencia";
        public const string IMPAGO = "Impago";
        public const string SIN_ACTIVIDAD = "SinActividad";
        public const string DUPLICADO = "Duplicado";
        public const string CAMBIO_TITULAR = "CambioTitular";
        public const string PETICION_CLIENTE = "PeticionCliente";
        public const string DISTRIBUIDOR = "Distribuidor";
        public const string PUBLICO_FINAL = "PublicoFinal";
        public const string REACTIVACION = "Reactivacion";
        public const string OTRO = "Otro";

        /// <summary>Longitud máxima del comentario libre (nvarchar(200) en la API; más, 400).</summary>
        public const int LONGITUD_COMENTARIO = 200;

        /// <summary>Código → nombre para mostrar en el desplegable.</summary>
        public static readonly IReadOnlyDictionary<string, string> Nombres = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            { CIERRE, "El centro ha cerrado" },
            { TRASPASO, "Traspaso" },
            { COMPETENCIA, "Compra a la competencia" },
            { IMPAGO, "Impago" },
            { SIN_ACTIVIDAD, "Sin actividad" },
            { DUPLICADO, "Ficha duplicada" },
            { CAMBIO_TITULAR, "Cambio de titular" },
            { PETICION_CLIENTE, "Lo pide el cliente" },
            { DISTRIBUIDOR, "Pasa a distribuidor" },
            { PUBLICO_FINAL, "Pasa a público final" },
            { REACTIVACION, "Reactivación" },
            { OTRO, "Otro" }
        };

        /// <summary>Los códigos que se ofrecen a la persona, en el orden en que se pintan.</summary>
        public static readonly IReadOnlyList<string> CodigosManuales = new ReadOnlyCollection<string>(new[]
        {
            CIERRE, TRASPASO, COMPETENCIA, IMPAGO, SIN_ACTIVIDAD, DUPLICADO, CAMBIO_TITULAR, PETICION_CLIENTE, DISTRIBUIDOR, PUBLICO_FINAL, REACTIVACION, OTRO
        });

        /// <summary>Las opciones (código + nombre) en orden, para enlazar el desplegable.</summary>
        public static readonly IReadOnlyList<OpcionCatalogo> Opciones = new ReadOnlyCollection<OpcionCatalogo>(
            CodigosManuales.Select(c => new OpcionCatalogo(c, Nombres[c])).ToList());

        /// <summary>
        /// Estados de la ficha que son una baja (el cliente deja de estar activo): al pasar a uno de ellos se pregunta
        /// el motivo. 9 = teléfono, -1 = nulo, 93, 11, 22, 95, 99 (los demás códigos de baja de la ficha).
        /// </summary>
        public static readonly IReadOnlyList<int> EstadosBaja = new ReadOnlyCollection<int>(new[] { 9, -1, 93, 11, 22, 95, 99 });

        public static bool EsEstadoBaja(int? estado) => estado.HasValue && EstadosBaja.Contains(estado.Value);

        /// <summary>
        /// Si hay que preguntar el motivo: el estado cambia y el nuevo es una baja, o se sale de una baja (reactivación).
        /// Sin cambio de estado no se pregunta ni se manda nada.
        /// </summary>
        public static bool PideMotivo(int? estadoAnterior, int? estadoNuevo)
        {
            if (!estadoNuevo.HasValue || estadoAnterior == estadoNuevo)
            {
                return false;
            }
            return EsEstadoBaja(estadoNuevo) || EsEstadoBaja(estadoAnterior);
        }

        public static bool EsValido(string motivo)
            => !string.IsNullOrWhiteSpace(motivo) && Nombres.ContainsKey(motivo.Trim());

        /// <summary>El nombre para mostrar de un código; el propio código si no está en el catálogo; vacío si no hay.</summary>
        public static string NombreDe(string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
            {
                return string.Empty;
            }
            return Nombres.TryGetValue(motivo.Trim(), out string nombre) ? nombre : motivo.Trim();
        }
    }

    /// <summary>
    /// Nesto#558: lo que la persona contesta al preguntarle por qué cambia el estado. <see cref="Motivo"/> es un código de
    /// <see cref="MotivoCambioEstadoCliente"/> o null; <see cref="Comentario"/> es libre (recortado a 200) o null.
    /// Las dos cosas a null = no se manda nada y la API sigue como hasta ahora.
    /// </summary>
    public class MotivoCambioEstado
    {
        public MotivoCambioEstado(string motivo, string comentario)
        {
            Motivo = MotivoCambioEstadoCliente.EsValido(motivo) ? motivo.Trim() : null;
            string limpio = comentario?.Trim();
            if (string.IsNullOrEmpty(limpio))
            {
                Comentario = null;
            }
            else
            {
                Comentario = limpio.Length > MotivoCambioEstadoCliente.LONGITUD_COMENTARIO
                    ? limpio.Substring(0, MotivoCambioEstadoCliente.LONGITUD_COMENTARIO)
                    : limpio;
            }
        }

        public string Motivo { get; }
        public string Comentario { get; }

        /// <summary>True si no hay nada que mandar.</summary>
        public bool EstaVacio => Motivo == null && Comentario == null;
    }
}
