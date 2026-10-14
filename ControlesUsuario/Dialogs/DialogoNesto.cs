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
    }
}
