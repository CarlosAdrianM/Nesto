using ControlesUsuario.Models;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ControlesUsuario.Dialogs
{
    /// <summary>NestoAPI#609: «¿Quisiste decir…?» (ver <see cref="RevisionConceptoDialogViewModel"/>).</summary>
    public partial class RevisionConceptoDialog : UserControl
    {
        public RevisionConceptoDialog()
        {
            InitializeComponent();
            Loaded += (_, __) => BotonUsarCorreccion.Focus();
        }
    }

    /// <summary>
    /// NestoAPI#609: pinta el concepto propuesto con los cambios resaltados (negrita y colores de selección del
    /// sistema, legibles en claro y en oscuro). En XAML: <c>local:TextoConCambios.Tramos="{Binding Tramos}"</c>.
    /// </summary>
    public static class TextoConCambios
    {
        public static readonly DependencyProperty TramosProperty = DependencyProperty.RegisterAttached(
            "Tramos", typeof(IEnumerable<TramoConcepto>), typeof(TextoConCambios), new PropertyMetadata(null, OnTramosChanged));

        public static IEnumerable<TramoConcepto> GetTramos(DependencyObject d) => (IEnumerable<TramoConcepto>)d.GetValue(TramosProperty);
        public static void SetTramos(DependencyObject d, IEnumerable<TramoConcepto> value) => d.SetValue(TramosProperty, value);

        private static void OnTramosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBlock bloque))
            {
                return;
            }
            bloque.Inlines.Clear();
            foreach (TramoConcepto tramo in e.NewValue as IEnumerable<TramoConcepto> ?? new List<TramoConcepto>())
            {
                var run = new Run(tramo.Texto);
                if (tramo.EsCambio)
                {
                    run.FontWeight = FontWeights.SemiBold;
                    run.SetResourceReference(TextElement.BackgroundProperty, SystemColors.HighlightBrushKey);
                    run.SetResourceReference(TextElement.ForegroundProperty, SystemColors.HighlightTextBrushKey);
                }
                bloque.Inlines.Add(run);
            }
        }
    }
}
