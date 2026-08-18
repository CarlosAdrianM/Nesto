using Nesto.Infrastructure.Shared;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControlesUsuario
{
    /// <summary>
    /// Nesto#444: muestra los teléfonos de un cliente (la cadena de la ficha puede llevar
    /// varios) troceados de uno en uno, cada uno en un TextBox de solo lectura: un clic
    /// selecciona ese número completo y con botón derecho se copia (menú nativo), listo
    /// para pegarlo en la centralita. Si no hay teléfonos, el control se oculta.
    /// </summary>
    public partial class ListaTelefonos : UserControl
    {
        public ListaTelefonos()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty TelefonosProperty = DependencyProperty.Register(
            nameof(Telefonos), typeof(string), typeof(ListaTelefonos),
            new PropertyMetadata(null, OnTelefonosChanged));

        /// <summary>Cadena de teléfonos tal y como viene de la ficha (p. ej. "911234567/680396700").</summary>
        public string Telefonos
        {
            get => (string)GetValue(TelefonosProperty);
            set => SetValue(TelefonosProperty, value);
        }

        public static readonly DependencyProperty EtiquetaProperty = DependencyProperty.Register(
            nameof(Etiqueta), typeof(string), typeof(ListaTelefonos), new PropertyMetadata(null));

        public string Etiqueta
        {
            get => (string)GetValue(EtiquetaProperty);
            set => SetValue(EtiquetaProperty, value);
        }

        private static void OnTelefonosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ListaTelefonos)d;
            IReadOnlyList<string> telefonos = TelefonoHelper.TrocearTelefonos(e.NewValue as string);
            control.itemsTelefonos.ItemsSource = telefonos;
            control.Visibility = telefonos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Telefono_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox caja)
            {
                caja.Focus();
                caja.SelectAll();
            }
        }

    }
}
