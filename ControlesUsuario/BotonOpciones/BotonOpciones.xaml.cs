using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ControlesUsuario
{
    /// <summary>
    /// Nesto#388: "ruedita" de opciones reutilizable para selectores. Un icono de engranaje que
    /// abre un panel flotante con las opciones que el consumidor declare como contenido:
    /// <code>
    /// &lt;controles:BotonOpciones Titulo="Opciones del selector"&gt;
    ///     &lt;CheckBox Content="..." IsChecked="{Binding ...}"/&gt;
    /// &lt;/controles:BotonOpciones&gt;
    /// </code>
    /// El consumidor pone dentro cualquier contenido (checkboxes, combos...) enlazado a su propio
    /// ViewModel, que es quien persiste cada parámetro por usuario (IConfiguracion.GuardarParametro).
    /// </summary>
    [ContentProperty(nameof(Opciones))]
    public partial class BotonOpciones : UserControl
    {
        public BotonOpciones()
        {
            InitializeComponent();
        }

        public string Titulo
        {
            get => (string)GetValue(TituloProperty);
            set => SetValue(TituloProperty, value);
        }

        public static readonly DependencyProperty TituloProperty =
            DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(BotonOpciones),
                new PropertyMetadata("Opciones"));

        public object Opciones
        {
            get => GetValue(OpcionesProperty);
            set => SetValue(OpcionesProperty, value);
        }

        public static readonly DependencyProperty OpcionesProperty =
            DependencyProperty.Register(nameof(Opciones), typeof(object), typeof(BotonOpciones),
                new PropertyMetadata(null));
    }
}
