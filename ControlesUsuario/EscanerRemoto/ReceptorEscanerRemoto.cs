using Nesto.Infrastructure.Contracts;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ControlesUsuario.EscanerRemoto
{
    /// <summary>
    /// Nesto#589 (NestoAPI#768, Carlos 30/10/26): recibe lo leído con Ariadna («Escanear para Nesto») y, en el hilo de la UI:
    /// - si el foco está en un campo que acepta códigos de barras (<see cref="LectorCodigoBarras"/>) de una ventana de Nesto
    ///   que está delante, lo escribe y pulsa Intro, como un lector USB;
    /// - si no, no teclea nada y enseña el aviso no modal «Último código leído: X» con «Copiar» (<see cref="AvisoUltimoCodigoLeido"/>).
    /// Una lectura repetida (misma clave) no se escribe dos veces. Uno para toda la aplicación (lo crea Application al arrancar).
    /// </summary>
    public sealed class ReceptorEscanerRemoto : IDisposable
    {
        private readonly IEscanerRemoto _escaner;
        private readonly Dispatcher _dispatcher;
        private readonly Func<IInputElement> _foco;
        private readonly Func<TextBox, bool> _enVentanaActiva;
        private readonly Action<TextBox, string> _teclear;
        private readonly Action<string> _avisar;
        private readonly FiltroLecturasRepetidas _repetidas = new FiltroLecturasRepetidas();

        public ReceptorEscanerRemoto(IEscanerRemoto escaner, Dispatcher dispatcher)
            : this(escaner, dispatcher, () => Keyboard.FocusedElement, EnVentanaActiva, LectorCodigoBarras.Teclear,
                  AvisoUltimoCodigoLeido.Mostrar)
        { }

        internal ReceptorEscanerRemoto(IEscanerRemoto escaner, Dispatcher dispatcher, Func<IInputElement> foco,
            Func<TextBox, bool> enVentanaActiva, Action<TextBox, string> teclear, Action<string> avisar)
        {
            _escaner = escaner ?? throw new ArgumentNullException(nameof(escaner));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _foco = foco ?? throw new ArgumentNullException(nameof(foco));
            _enVentanaActiva = enVentanaActiva ?? throw new ArgumentNullException(nameof(enVentanaActiva));
            _teclear = teclear ?? throw new ArgumentNullException(nameof(teclear));
            _avisar = avisar ?? throw new ArgumentNullException(nameof(avisar));
            _escaner.LecturaRecibida += AlRecibir;
        }

        private void AlRecibir(object sender, LecturaEscanerRemoto lectura)
        {
            string codigo = lectura?.Codigo?.Trim();
            if (string.IsNullOrEmpty(codigo) || !_repetidas.EsNueva(lectura.ClaveIdempotencia))
            {
                return;
            }
            try
            {
                _ = _dispatcher.BeginInvoke(new Action(() => Procesar(codigo)));
            }
            catch (Exception)
            {
                // Nesto se está cerrando.
            }
        }

        /// <summary>En el hilo de la UI: al campo con el foco si lo acepta; si no, el aviso. Nunca lanza.</summary>
        internal void Procesar(string codigo)
        {
            try
            {
                TextBox destino = LectorCodigoBarras.DestinoDe(_foco());
                if (destino != null && _enVentanaActiva(destino))
                {
                    _teclear(destino, codigo);
                    return;
                }
                _avisar(codigo);
            }
            catch (Exception)
            {
                try
                {
                    _avisar(codigo);
                }
                catch (Exception)
                {
                    // El escáner remoto es un extra: nunca debe tumbar Nesto.
                }
            }
        }

        private static bool EnVentanaActiva(TextBox cuadro) => Window.GetWindow(cuadro)?.IsActive == true;

        public void Dispose() => _escaner.LecturaRecibida -= AlRecibir;
    }
}
