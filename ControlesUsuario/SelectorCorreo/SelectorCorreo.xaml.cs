using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario
{
    /// <summary>
    /// Nesto (29/10/26): selector de correo reutilizable. Ofrece en un combo editable los correos que le pasen en
    /// <see cref="Correos"/> (p. ej. los de las personas de contacto del cliente), sin repetir, y deja elegir uno o escribir otro
    /// en <see cref="Correo"/> (enlace de doble sentido). Cuando cambian los correos y el usuario no había escrito nada, pone el
    /// primero. <see cref="EsValido"/> dice si lo escrito tiene forma de correo (vacío vale: es opcional).
    /// <para>Null-safe en el teardown: si <see cref="Correos"/> pasa a null al quitar la vista, solo se vacía la lista; no toca
    /// <see cref="Correo"/>.</para>
    /// </summary>
    public partial class SelectorCorreo : UserControl
    {
        private string _puestoPorElSelector;
        private INotifyCollectionChanged _coleccionObservada;

        public SelectorCorreo()
        {
            InitializeComponent();
        }

        /// <summary>Los correos que se ofrecen (strings; pueden venir varios juntos separados por «;» o «,»).</summary>
        public IEnumerable Correos
        {
            get => (IEnumerable)GetValue(CorreosProperty);
            set => SetValue(CorreosProperty, value);
        }

        public static readonly DependencyProperty CorreosProperty =
            DependencyProperty.Register(nameof(Correos), typeof(IEnumerable), typeof(SelectorCorreo),
                new FrameworkPropertyMetadata(null, OnCorreosChanged));

        private static void OnCorreosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorCorreo)d;
            if (selector._coleccionObservada != null)
            {
                selector._coleccionObservada.CollectionChanged -= selector.AlCambiarColeccion;
                selector._coleccionObservada = null;
            }
            if (e.NewValue is INotifyCollectionChanged observable)
            {
                selector._coleccionObservada = observable;
                observable.CollectionChanged += selector.AlCambiarColeccion;
            }
            selector.Recalcular(e.NewValue != null);
        }

        private void AlCambiarColeccion(object sender, NotifyCollectionChangedEventArgs e) => Recalcular(true);

        private void Recalcular(bool ajustarCorreo)
        {
            List<string> nuevos = CorreosElegibles.Unicos(Correos?.OfType<object>().Select(c => c?.ToString()));
            SetValue(CorreosOfrecidosPropertyKey, nuevos);
            if (!ajustarCorreo)
            {
                return;
            }
            string correo = CorreosElegibles.CorreoTrasCambiarLista(Correo, _puestoPorElSelector, nuevos);
            if (correo != Correo)
            {
                _puestoPorElSelector = correo;
                Correo = correo;
            }
        }

        /// <summary>Lo que se ve en el desplegable (sin repetir y solo los que tienen forma de correo).</summary>
        public IReadOnlyList<string> CorreosOfrecidos => (IReadOnlyList<string>)GetValue(CorreosOfrecidosProperty);

        private static readonly DependencyPropertyKey CorreosOfrecidosPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(CorreosOfrecidos), typeof(IReadOnlyList<string>), typeof(SelectorCorreo),
                new PropertyMetadata(new List<string>()));

        public static readonly DependencyProperty CorreosOfrecidosProperty = CorreosOfrecidosPropertyKey.DependencyProperty;

        /// <summary>El correo elegido o escrito (doble sentido por defecto).</summary>
        public string Correo
        {
            get => (string)GetValue(CorreoProperty);
            set => SetValue(CorreoProperty, value);
        }

        public static readonly DependencyProperty CorreoProperty =
            DependencyProperty.Register(nameof(Correo), typeof(string), typeof(SelectorCorreo),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCorreoChanged));

        private static void OnCorreoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (SelectorCorreo)d;
            selector.SetValue(EsValidoPropertyKey, CorreosElegibles.EsValido(e.NewValue as string));
        }

        /// <summary>False si lo escrito no tiene forma de correo (vacío es válido).</summary>
        public bool EsValido => (bool)GetValue(EsValidoProperty);

        private static readonly DependencyPropertyKey EsValidoPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(EsValido), typeof(bool), typeof(SelectorCorreo), new PropertyMetadata(true));

        public static readonly DependencyProperty EsValidoProperty = EsValidoPropertyKey.DependencyProperty;

        /// <summary>El texto de ayuda del combo.</summary>
        public string AyudaCorreo
        {
            get => (string)GetValue(AyudaCorreoProperty);
            set => SetValue(AyudaCorreoProperty, value);
        }

        public static readonly DependencyProperty AyudaCorreoProperty =
            DependencyProperty.Register(nameof(AyudaCorreo), typeof(string), typeof(SelectorCorreo),
                new PropertyMetadata("Elija uno de los correos del cliente o escriba otro"));
    }
}
