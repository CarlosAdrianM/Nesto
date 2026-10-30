using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControlesUsuario.EscanerRemoto
{
    /// <summary>
    /// Nesto#589 (NestoAPI#768): lo leído con Ariadna cuando el foco NO está en un campo que acepte códigos de barras. Aviso NO
    /// modal y sin quitar el foco (una ventanita encima de todo, abajo a la derecha): «Último código leído: X» con «Copiar» y
    /// «Cerrar». Siempre la misma ventana: la lectura siguiente cambia el código. Se llama en el hilo de la UI.
    /// </summary>
    public sealed class AvisoUltimoCodigoLeido : Window
    {
        public const string TITULO = "Escáner remoto";
        public const string TEXTO_ULTIMO = "Último código leído:";
        public const string TEXTO_EXPLICACION = "No se ha escrito en ningún sitio: el cursor no está en un campo que acepte códigos de barras " +
            "(el producto de una línea, la referencia de la ficha de producto, la lectura de una reposición…). Póngalo en uno y vuelva a leer, o cópielo.";
        public const string TEXTO_COPIAR = "Copiar";
        public const string TEXTO_COPIADO = "Copiado";

        private static AvisoUltimoCodigoLeido _actual;

        private readonly TextBox _codigo;
        private readonly Button _copiar;

        /// <summary>El código que se enseña ahora mismo (para los tests).</summary>
        internal string Codigo => _codigo.Text;

        internal AvisoUltimoCodigoLeido()
        {
            Title = TITULO;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Background = Brushes.White;
            WindowStartupLocation = WindowStartupLocation.Manual;

            var panel = new StackPanel { Margin = new Thickness(14), MaxWidth = 380 };
            panel.Children.Add(new TextBlock { Text = TEXTO_ULTIMO, Foreground = Brushes.DimGray });
            _codigo = new TextBox
            {
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 2, 0, 8)
            };
            panel.Children.Add(_codigo);
            panel.Children.Add(new TextBlock { Text = TEXTO_EXPLICACION, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray });

            var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            _copiar = new Button { Content = TEXTO_COPIAR, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 2, 8, 2) };
            _copiar.Click += (s, e) => Copiar();
            var cerrar = new Button { Content = "Cerrar", MinWidth = 80, Padding = new Thickness(8, 2, 8, 2) };
            cerrar.Click += (s, e) => Close();
            botones.Children.Add(_copiar);
            botones.Children.Add(cerrar);
            panel.Children.Add(botones);
            Content = panel;

            // Revisión 30/10/26: ShowActivated solo vale para el Show; al pulsar «Copiar» la ventanita se activaba y la
            // siguiente lectura volvía a caer aquí hasta que el usuario pinchaba en Nesto. Con WS_EX_NOACTIVATE nunca se activa.
            SourceInitialized += (s, e) => NoActivar();
            Loaded += (s, e) => Colocar();
            Closed += (s, e) =>
            {
                if (ReferenceEquals(_actual, this))
                {
                    _actual = null;
                }
            };
        }

        /// <summary>Enseña (o actualiza) el aviso con el código. Nunca lanza.</summary>
        public static void Mostrar(string codigo)
        {
            try
            {
                if (_actual == null)
                {
                    _actual = new AvisoUltimoCodigoLeido();
                }
                _actual.Poner(codigo);
                if (!_actual.IsVisible)
                {
                    _actual.Show();
                }
            }
            catch (Exception)
            {
                // El aviso es un extra: nunca debe tumbar Nesto.
            }
        }

        internal void Poner(string codigo)
        {
            _codigo.Text = codigo ?? string.Empty;
            _copiar.Content = TEXTO_COPIAR;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private void NoActivar()
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    _ = SetWindowLong(handle, GWL_EXSTYLE, GetWindowLong(handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
                }
            }
            catch (Exception)
            {
                // Sin esto la ventanita funciona igual, solo que se activa al pulsar en ella.
            }
        }

        private void Copiar()
        {
            try
            {
                Clipboard.SetText(_codigo.Text ?? string.Empty);
                _copiar.Content = TEXTO_COPIADO;
            }
            catch (Exception)
            {
                // El portapapeles puede estar ocupado (escritorio remoto): se puede seleccionar el código y copiarlo a mano.
                _codigo.Focus();
                _codigo.SelectAll();
            }
        }

        private void Colocar()
        {
            Rect zona = SystemParameters.WorkArea;
            Left = Math.Max(zona.Left, zona.Right - ActualWidth - 16);
            Top = Math.Max(zona.Top, zona.Bottom - ActualHeight - 16);
        }
    }
}
