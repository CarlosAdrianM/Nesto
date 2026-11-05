using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nesto.Modules.Producto
{
    /// <summary>Lo que ha contestado el usuario antes de aplicar el cambio de estado.</summary>
    public enum DecisionCambioEstado
    {
        Cancelar,
        /// <summary>Aplicar sin quitar controles de stock (los que los tengan se omiten).</summary>
        Aplicar,
        /// <summary>«Quitarlos y cambiar»: QuitarControlesStock = true.</summary>
        AplicarQuitandoControles
    }

    /// <summary>
    /// Nesto#624 (NestoAPI#738, sugerencia 793 de Manuel): lo que comparten la ventana masiva y el desplegable de la ficha. La
    /// pregunta de los controles de stock se hace UNA vez por lote, con tres botones; si no hace falta, una confirmación con
    /// los avisos. Y el texto del resultado.
    /// </summary>
    public static class FlujoCambioEstadoProductos
    {
        public const string TITULO = "Cambiar estado de productos";
        public const string BOTON_QUITAR_Y_CAMBIAR = "Quitarlos y cambiar";
        public const string BOTON_NO_CAMBIAR_ESOS = "No cambiar esos";
        public const string BOTON_CANCELAR = "Cancelar";
        internal const string PREGUNTA_CONTROLES_POR_DEFECTO =
            "Algún producto tiene controles de stock. ¿Los quito y cambio el estado, o no cambio el estado de esos productos?";

        /// <summary>
        /// Pregunta lo que haga falta antes de aplicar. Con <c>ControlesStockAfectados</c> &gt; 0, la pregunta de los controles
        /// (con los avisos debajo); si no, una confirmación con <paramref name="resumen"/> y los avisos, si hay avisos o si
        /// <paramref name="confirmarSiempre"/>; sin nada que avisar, se aplica directamente.
        /// </summary>
        public static DecisionCambioEstado Preguntar(IServicioDialogos dialogos, VistaPreviaEstadoProductosDTO vista,
            IEnumerable<string> avisos, string resumen, bool confirmarSiempre)
        {
            List<string> lista = (avisos ?? Enumerable.Empty<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
            string textoAvisos = lista.Count == 0 ? null : string.Join("\n", lista.Select(a => "• " + a));

            if (vista != null && vista.ControlesStockAfectados > 0)
            {
                string pregunta = string.IsNullOrWhiteSpace(vista.PreguntaControlesStock) ? PREGUNTA_CONTROLES_POR_DEFECTO : vista.PreguntaControlesStock.Trim();
                var mensaje = new StringBuilder(pregunta);
                if (textoAvisos != null)
                {
                    mensaje.Append("\n\n").Append(textoAvisos);
                }
                ResultadoBoton boton = ConfirmacionConBotones.PreguntarTres(dialogos, TITULO, mensaje.ToString(),
                    BOTON_QUITAR_Y_CAMBIAR, BOTON_NO_CAMBIAR_ESOS, BOTON_CANCELAR);
                return boton switch
                {
                    ResultadoBoton.OK => DecisionCambioEstado.AplicarQuitandoControles,
                    ResultadoBoton.No => DecisionCambioEstado.Aplicar,
                    _ => DecisionCambioEstado.Cancelar
                };
            }

            if (textoAvisos == null && !confirmarSiempre)
            {
                return DecisionCambioEstado.Aplicar;
            }
            string confirmacion = textoAvisos == null ? resumen : $"{resumen}\n\n{textoAvisos}";
            return dialogos != null && dialogos.ShowConfirmationAnswer(TITULO, confirmacion)
                ? DecisionCambioEstado.Aplicar
                : DecisionCambioEstado.Cancelar;
        }

        /// <summary>«Se ha cambiado el estado de N productos.» + controles quitados/creados + omitidos con su motivo + avisos.</summary>
        public static string TextoResultado(ResultadoCambioEstadoProductosDTO resultado)
        {
            if (resultado == null)
            {
                return "No se ha cambiado el estado de ningún producto.";
            }
            var texto = new StringBuilder(resultado.Cambiados == 1
                ? "Se ha cambiado el estado de 1 producto."
                : $"Se ha cambiado el estado de {resultado.Cambiados} productos.");
            if (resultado.ControlesStockQuitados > 0)
            {
                texto.Append(resultado.ControlesStockQuitados == 1
                    ? "\nSe ha quitado 1 control de stock."
                    : $"\nSe han quitado {resultado.ControlesStockQuitados} controles de stock.");
            }
            if (resultado.ControlesStockCreados > 0)
            {
                texto.Append(resultado.ControlesStockCreados == 1
                    ? "\nSe ha creado 1 control de stock con los máximos sugeridos."
                    : $"\nSe han creado {resultado.ControlesStockCreados} controles de stock con los máximos sugeridos.");
            }
            List<ProductoOmitidoDTO> omitidos = resultado.Omitidos ?? new List<ProductoOmitidoDTO>();
            if (omitidos.Count > 0)
            {
                texto.Append(omitidos.Count == 1 ? "\n\nNo se ha cambiado 1 producto:" : $"\n\nNo se han cambiado {omitidos.Count} productos:");
                foreach (ProductoOmitidoDTO omitido in omitidos)
                {
                    texto.Append($"\n• {omitido.Producto?.Trim()}: {omitido.Motivo?.Trim()}");
                }
            }
            List<string> avisos = (resultado.Avisos ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
            if (avisos.Count > 0)
            {
                texto.Append("\n");
                foreach (string aviso in avisos)
                {
                    texto.Append("\n• ").Append(aviso.Trim());
                }
            }
            return texto.ToString();
        }
    }
}
