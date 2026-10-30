using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControlesUsuario.EscanerRemoto
{
    /// <summary>
    /// Nesto#589 (NestoAPI#768): qué campos aceptan códigos de barras, en un solo sitio. Lo leído con Ariadna en el móvil
    /// («Escanear para Nesto») solo se escribe en el campo que tiene el foco si está marcado:
    /// <code>
    /// xmlns:escaner="clr-namespace:ControlesUsuario.EscanerRemoto;assembly=ControlesUsuario"
    /// &lt;TextBox escaner:LectorCodigoBarras.AceptaCodigoBarras="True" .../&gt;
    /// </code>
    /// Los TextBox con <c>ProductoBehavior</c> (el producto de las líneas) lo llevan solos. Se escribe y se pulsa Intro igual que
    /// un lector USB (<see cref="Teclear"/>), así que el campo hace lo mismo que con el lector: no hace falta tocar su ViewModel.
    /// </summary>
    public static class LectorCodigoBarras
    {
        public static readonly DependencyProperty AceptaCodigoBarrasProperty =
            DependencyProperty.RegisterAttached("AceptaCodigoBarras", typeof(bool), typeof(LectorCodigoBarras),
                new PropertyMetadata(false));

        public static bool GetAceptaCodigoBarras(DependencyObject elemento)
            => elemento != null && (bool)elemento.GetValue(AceptaCodigoBarrasProperty);

        public static void SetAceptaCodigoBarras(DependencyObject elemento, bool valor)
            => elemento?.SetValue(AceptaCodigoBarrasProperty, valor);

        /// <summary>
        /// El cuadro en el que escribir lo leído: el que tiene el foco si acepta códigos de barras (marcado él o, en un combo
        /// editable, el combo), está habilitado, visible y no es de solo lectura. Null en cualquier otro caso.
        /// </summary>
        public static TextBox DestinoDe(IInputElement foco)
        {
            if (!(foco is TextBox cuadro))
            {
                return null;
            }
            bool marcado = GetAceptaCodigoBarras(cuadro) || GetAceptaCodigoBarras(cuadro.TemplatedParent);
            return marcado && cuadro.IsEnabled && !cuadro.IsReadOnly && cuadro.IsVisible ? cuadro : null;
        }

        /// <summary>
        /// Escribe <paramref name="codigo"/> en el cuadro como si se tecleara (donde está el cursor, sustituyendo lo seleccionado)
        /// y pulsa Intro (abajo y arriba): lo mismo que hace un lector USB. Se llama en el hilo de la UI.
        /// </summary>
        public static void Teclear(TextBox destino, string codigo)
        {
            if (destino == null || string.IsNullOrEmpty(codigo))
            {
                return;
            }
            _ = TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, destino, codigo));
            PulsarIntro(destino);
        }

        private static void PulsarIntro(TextBox destino)
        {
            PresentationSource fuente = PresentationSource.FromVisual(destino);
            if (fuente != null && ReferenceEquals(Keyboard.FocusedElement, destino))
            {
                // Por el InputManager, como el teclado de verdad (va al elemento con el foco): él mismo pasa de PreviewKeyDown a
                // KeyDown si nadie lo atiende, y llegan también los KeyBinding y el botón por defecto (IsDefault) de un diálogo
                _ = InputManager.Current.ProcessInput(Tecla(fuente, Keyboard.PreviewKeyDownEvent));
                _ = InputManager.Current.ProcessInput(Tecla(fuente, Keyboard.PreviewKeyUpEvent));
                return;
            }
            // Sin ventana con el foco (no debería pasar: solo se teclea en el campo con el foco): directamente al cuadro
            if (Lanzar(destino, Tecla(fuente, Keyboard.PreviewKeyDownEvent)))
            {
                _ = Lanzar(destino, Tecla(fuente, Keyboard.KeyDownEvent));
            }
            if (Lanzar(destino, Tecla(fuente, Keyboard.PreviewKeyUpEvent)))
            {
                _ = Lanzar(destino, Tecla(fuente, Keyboard.KeyUpEvent));
            }
        }

        private static KeyEventArgs Tecla(PresentationSource fuente, RoutedEvent evento)
            => new KeyEventArgs(Keyboard.PrimaryDevice, fuente, Environment.TickCount, Key.Enter) { RoutedEvent = evento };

        /// <summary>Devuelve true si nadie ha dado la tecla por atendida (entonces sigue el evento que burbujea).</summary>
        private static bool Lanzar(TextBox destino, KeyEventArgs tecla)
        {
            destino.RaiseEvent(tecla);
            return !tecla.Handled;
        }
    }
}
