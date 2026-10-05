using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#504: «Pegar» (botón derecho y Ctrl+V) en un cuadro de texto adjunta la imagen del portapapeles cuando lo
    /// copiado es una imagen sin texto. El menú de botón derecho de serie del TextBox solo sabe pegar texto y, con una
    /// imagen, dejaba «Pegar» en gris. Se engancha a la orden Paste antes que el TextBox (Preview): con imagen dice que
    /// se puede y ejecuta <see cref="ComandoProperty"/>; con texto no interviene y el TextBox pega como siempre.
    /// Uso: local:PegarImagenTextBox.Comando="{Binding PegarImagenCommand}".
    /// </summary>
    public static class PegarImagenTextBox
    {
        public static readonly DependencyProperty ComandoProperty = DependencyProperty.RegisterAttached(
            "Comando", typeof(ICommand), typeof(PegarImagenTextBox), new PropertyMetadata(null, OnComandoChanged));

        public static ICommand GetComando(DependencyObject elemento) => (ICommand)elemento.GetValue(ComandoProperty);

        public static void SetComando(DependencyObject elemento, ICommand valor) => elemento.SetValue(ComandoProperty, valor);

        /// <summary>Para las pruebas: qué hay en el portapapeles. Null = el portapapeles de Windows.</summary>
        internal static Func<bool> HayImagenSinTexto { get; set; }

        private static bool ImagenSinTexto()
        {
            if (HayImagenSinTexto != null)
            {
                return HayImagenSinTexto();
            }
            try
            {
                return Clipboard.ContainsImage() && !Clipboard.ContainsText();
            }
            catch (Exception)
            {
                return false; // portapapeles bloqueado por otra aplicación: como si no hubiera imagen
            }
        }

        private static void OnComandoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBox cuadro))
            {
                return;
            }
            CommandManager.RemovePreviewCanExecuteHandler(cuadro, AlPreguntarSiSePuede);
            CommandManager.RemovePreviewExecutedHandler(cuadro, AlEjecutar);
            if (e.NewValue != null)
            {
                CommandManager.AddPreviewCanExecuteHandler(cuadro, AlPreguntarSiSePuede);
                CommandManager.AddPreviewExecutedHandler(cuadro, AlEjecutar);
            }
        }

        private static void AlPreguntarSiSePuede(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Paste && sender is DependencyObject d && GetComando(d) != null && ImagenSinTexto())
            {
                e.CanExecute = true;
                e.Handled = true;
            }
        }

        private static void AlEjecutar(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command != ApplicationCommands.Paste || !(sender is DependencyObject d) || !ImagenSinTexto())
            {
                return;
            }
            ICommand comando = GetComando(d);
            if (comando != null && comando.CanExecute(null))
            {
                comando.Execute(null);
                e.Handled = true;
            }
        }
    }
}
