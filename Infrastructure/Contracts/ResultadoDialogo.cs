using System;
namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.2): botón con el que se cerró un diálogo. Mismos valores que el
    /// <c>ButtonResult</c> de Prism (que a su vez son los de Win32), para poder convertir con un cast.
    /// </summary>
    public enum ResultadoBoton
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Abort = 3,
        Retry = 4,
        Ignore = 5,
        Yes = 6,
        No = 7
    }

    /// <summary>
    /// Nesto#490 (4C.2): resultado de un diálogo de <see cref="IServicioDialogos"/>, sin tipos de Prism.
    /// </summary>
    public class ResultadoDialogo
    {
        public ResultadoDialogo(ResultadoBoton result, ParametrosDialogo parameters = null)
        {
            Result = result;
            Parameters = parameters ?? new ParametrosDialogo();
        }

        public ResultadoBoton Result { get; }
        public ParametrosDialogo Parameters { get; }
    }

    /// <summary>
    /// Nesto#490 (4C.2): parámetros de ida y vuelta de un diálogo, sin tipos de Prism. Admite el
    /// inicializador de colección igual que <c>DialogParameters</c>:
    /// <c>new ParametrosDialogo { { "title", "..." }, { "message", "..." } }</c>.
    /// El comportamiento está en <see cref="ParametrosNesto"/> (compartido con la navegación).
    /// </summary>
    public class ParametrosDialogo : ParametrosNesto
    {
    }
}
