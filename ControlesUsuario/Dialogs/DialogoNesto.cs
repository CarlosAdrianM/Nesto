using System.Windows;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 3): propiedades adjuntas de las vistas de diálogo, en vez de las de Prism.
    ///
    /// <see cref="EstiloVentanaProperty"/> sustituye a <c>prism:Dialog.WindowStyle</c>: el estilo de la ventana
    /// que aloja el diálogo (tamaño, si se puede redimensionar...). Como con Prism, si la vista lo trae,
    /// <see cref="ServicioDialogosNesto"/> lo pone en la ventana EN VEZ del suyo (lo sustituye entero). En XAML:
    /// <code>
    /// xmlns:dialogos="clr-namespace:ControlesUsuario.Dialogs;assembly=ControlesUsuario"
    /// &lt;dialogos:DialogoNesto.EstiloVentana&gt;
    ///     &lt;Style TargetType="Window"&gt;...&lt;/Style&gt;
    /// &lt;/dialogos:DialogoNesto.EstiloVentana&gt;
    /// </code>
    /// </summary>
    public static class DialogoNesto
    {
        public static readonly DependencyProperty EstiloVentanaProperty = DependencyProperty.RegisterAttached(
            "EstiloVentana", typeof(Style), typeof(DialogoNesto), new PropertyMetadata(null));

        public static Style GetEstiloVentana(DependencyObject obj) => (Style)obj.GetValue(EstiloVentanaProperty);

        public static void SetEstiloVentana(DependencyObject obj, Style value) => obj.SetValue(EstiloVentanaProperty, value);

        /// <summary>
        /// Nesto#575 (sugerencia 900): el diálogo se abre SIN bloquear Nesto aunque se pida con <c>ShowDialog</c>,
        /// para poder minimizarlo, ir a otra pantalla (por ejemplo, a hacer una captura) y volver a él desde la
        /// barra de tareas. Solo hay uno abierto por nombre: si se pide otra vez, se trae al frente el que ya
        /// estaba (restaurado si estaba minimizado). Quien lo abre recibe el resultado al cerrarse, como siempre;
        /// el que pide uno que ya estaba abierto lo recibe enseguida (<see cref="Nesto.Infrastructure.Contracts.ResultadoBoton.None"/>).
        /// En XAML: <c>dialogos:DialogoNesto.SinBloquear="True"</c>.
        /// </summary>
        public static readonly DependencyProperty SinBloquearProperty = DependencyProperty.RegisterAttached(
            "SinBloquear", typeof(bool), typeof(DialogoNesto), new PropertyMetadata(false));

        public static bool GetSinBloquear(DependencyObject obj) => (bool)obj.GetValue(SinBloquearProperty);

        public static void SetSinBloquear(DependencyObject obj, bool value) => obj.SetValue(SinBloquearProperty, value);
    }
}
