using Nesto.Infrastructure.Contracts;
using System;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3): piloto de la ventana de diálogos propia. Abre los diálogos con
    /// <see cref="ServicioDialogosNesto"/> si el usuario tiene el parámetro
    /// <c>VentanaDialogosPropia</c> = "1", y con <see cref="ServicioDialogosPrism"/> en otro caso
    /// (o si no se puede leer). Se decide una vez, en el primer diálogo, y vale hasta cerrar Nesto.
    /// Cuando la ventana propia esté validada se quita esta clase y se registra directamente.
    /// </summary>
    public class ServicioDialogosConmutable : ServicioDialogosBase
    {
        private readonly IServicioDialogos _prism;
        private readonly IServicioDialogos _propio;
        private readonly Lazy<bool> _usarPropio;

        public ServicioDialogosConmutable(IServicioDialogos prism, IServicioDialogos propio, Func<bool> usarPropio)
        {
            _prism = prism ?? throw new ArgumentNullException(nameof(prism));
            _propio = propio ?? throw new ArgumentNullException(nameof(propio));
            if (usarPropio == null)
            {
                throw new ArgumentNullException(nameof(usarPropio));
            }
            _usarPropio = new Lazy<bool>(() =>
            {
                try
                {
                    return usarPropio();
                }
                catch
                {
                    return false; // sin parámetro legible, lo de siempre
                }
            });
        }

        private IServicioDialogos Elegido => _usarPropio.Value ? _propio : _prism;

        public override void ShowDialog(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => Elegido.ShowDialog(name, parameters, callback);

        public override void Show(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => Elegido.Show(name, parameters, callback);
    }
}
