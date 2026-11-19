using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ControlesUsuario.Notificaciones
{
    /// <summary>
    /// Nesto#477: campana con el número de notificaciones sin leer y su panel. El DataContext es un
    /// <see cref="CampanaNotificacionesViewModel"/> (lo pone la ventana principal); al cargarse arranca
    /// su refresco periódico.
    /// </summary>
    public partial class CampanaNotificaciones : UserControl
    {
        // Sugerencia 1367: el tamaño del panel se recuerda mientras Nesto esté abierto (de partida, como la ventana de Novedades)
        internal const double ANCHO_INICIAL = 760;
        internal const double ALTO_LISTA_INICIAL = 560;
        internal const double ANCHO_MINIMO = 400;
        internal const double ALTO_LISTA_MINIMO = 200;
        private static double ancho = ANCHO_INICIAL;
        private static double altoLista = ALTO_LISTA_INICIAL;

        public CampanaNotificaciones()
        {
            InitializeComponent();
            Marco.Width = ancho;
            Lista.MaxHeight = altoLista;
            Loaded += (s, e) => (DataContext as CampanaNotificacionesViewModel)?.Iniciar();
            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>Sugerencia 1367: el nuevo tamaño con la esquina (sin bajar del mínimo).</summary>
        internal static (double Ancho, double Alto) Redimensionar(double ancho, double alto, double cambioAncho, double cambioAlto)
            => (Math.Max(ANCHO_MINIMO, ancho + cambioAncho), Math.Max(ALTO_LISTA_MINIMO, alto + cambioAlto));

        private void Esquina_DragDelta(object sender, DragDeltaEventArgs e)
        {
            (double nuevoAncho, double nuevoAlto) = Redimensionar(Marco.ActualWidth > 0 ? Marco.ActualWidth : ancho,
                double.IsInfinity(Lista.MaxHeight) ? altoLista : Lista.MaxHeight, e.HorizontalChange, e.VerticalChange);
            Marco.Width = ancho = nuevoAncho;
            Lista.MaxHeight = altoLista = nuevoAlto;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            (e.OldValue as CampanaNotificacionesViewModel)?.Detener();
            if (IsLoaded)
            {
                (e.NewValue as CampanaNotificacionesViewModel)?.Iniciar();
            }
        }
    }
}
