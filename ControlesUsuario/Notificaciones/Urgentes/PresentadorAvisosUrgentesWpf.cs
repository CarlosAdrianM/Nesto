using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace ControlesUsuario.Notificaciones.Urgentes
{
    /// <summary>
    /// Nesto#593 (NestoAPI#775): las ventanas rojas de las urgentes. Cada una, abajo a la derecha del área de trabajo (si hay varias, una
    /// encima de otra), siempre encima y sin dueño (así se sigue viendo con Nesto minimizado). El pitido es el de exclamación de Windows y,
    /// si Nesto no está delante, parpadea su icono en la barra de tareas hasta que se ponga delante (FlashWindowEx).
    /// </summary>
    public sealed class PresentadorAvisosUrgentesWpf : IPresentadorAvisosUrgentes
    {
        private const double MARGEN = 12;
        private readonly Dictionary<AvisoUrgenteViewModel, AvisoUrgenteWindow> _ventanas = new Dictionary<AvisoUrgenteViewModel, AvisoUrgenteWindow>();

        public void Abrir(AvisoUrgenteViewModel aviso, Action<AvisoUrgenteViewModel> alCerrarse)
        {
            if (aviso == null || _ventanas.ContainsKey(aviso))
            {
                return;
            }
            var ventana = new AvisoUrgenteWindow { DataContext = aviso, WindowStartupLocation = WindowStartupLocation.Manual };
            _ventanas[aviso] = ventana;
            ventana.Closed += (s, e) =>
            {
                _ = _ventanas.Remove(aviso);
                Recolocar();
                alCerrarse?.Invoke(aviso);
            };
            ventana.SizeChanged += (s, e) => Recolocar();
            ventana.Left = -10000; // fuera de la vista hasta saber su alto
            ventana.Show();
            Recolocar();
        }

        public async void Cerrar(AvisoUrgenteViewModel aviso, TimeSpan retraso)
        {
            if (aviso == null || !_ventanas.ContainsKey(aviso))
            {
                return;
            }
            if (retraso > TimeSpan.Zero)
            {
                await Task.Delay(retraso);
            }
            if (_ventanas.TryGetValue(aviso, out AvisoUrgenteWindow ventana))
            {
                ventana.Close();
            }
        }

        public void Pitar() => SystemSounds.Exclamation.Play();

        public void LlamarLaAtencion()
        {
            Window principal = Application.Current?.MainWindow;
            if (principal == null || (principal.IsActive && principal.WindowState != WindowState.Minimized))
            {
                return;
            }
            IntPtr hwnd = new WindowInteropHelper(principal).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                hwnd = hwnd,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = uint.MaxValue,
                dwTimeout = 0
            };
            _ = FlashWindowEx(ref info);
        }

        /// <summary>De abajo arriba en la esquina inferior derecha, en el orden en que se abrieron.</summary>
        private void Recolocar()
        {
            Rect area = SystemParameters.WorkArea;
            double abajo = area.Bottom - MARGEN;
            foreach (AvisoUrgenteWindow ventana in _ventanas.Values.ToList())
            {
                double alto = ventana.ActualHeight > 0 ? ventana.ActualHeight : 240;
                ventana.Left = area.Right - ventana.Width - MARGEN;
                ventana.Top = Math.Max(area.Top, abajo - alto);
                abajo = ventana.Top - MARGEN;
            }
        }

        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
    }
}
