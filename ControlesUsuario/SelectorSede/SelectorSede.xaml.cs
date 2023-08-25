using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ControlesUsuario
{
    /// <summary>
    /// Lógica de interacción para SelectorFormaPago.xaml
    /// </summary>
    public partial class SelectorSede : UserControl
    {
        public SelectorSede()
        {
            InitializeComponent();
            Loaded += SelectorSede_Loaded;
        }

        public static readonly DependencyProperty SeleccionadoProperty =
    DependencyProperty.Register(
        nameof(Seleccionado),
        typeof(string),
        typeof(SelectorSede),
        new PropertyMetadata(null, OnSeleccionadoChanged));

        private static void OnSeleccionadoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        public string Seleccionado
        {
            get { return (string)GetValue(SeleccionadoProperty); }
            set { SetValue(SeleccionadoProperty, value); }
        }


        // DependencyProperty para el texto
        public static readonly DependencyProperty EtiquetaProperty =
            DependencyProperty.Register(nameof(Etiqueta), typeof(string), typeof(SelectorSede), new PropertyMetadata(string.Empty));

        // Propiedad CLR que envuelve la DependencyProperty del texto
        public string Etiqueta
        {
            get { return (string)GetValue(EtiquetaProperty); }
            set { SetValue(EtiquetaProperty, value); }
        }

        // DependencyProperty para el color de la etiqueta
        public static readonly DependencyProperty ColorEtiquetaProperty =
            DependencyProperty.Register(nameof(ColorEtiqueta), typeof(Brush), typeof(SelectorSede));

        // Propiedad CLR que envuelve la DependencyProperty del color de la etiqueta
        public Brush ColorEtiqueta
        {
            get { return (Brush)GetValue(ColorEtiquetaProperty); }
            set { SetValue(ColorEtiquetaProperty, value); }
        }

        public bool EtiquetaNoVacia => !string.IsNullOrEmpty(Etiqueta);

        public IEnumerable<Sede> Sedes { get; } = Constantes.Sedes.ListaSedes;

        private void SedeButton_Click(object sender, RoutedEventArgs e)
        {
            // Maneja el evento del botón aquí
            if (sender is Button button)
            {
                Seleccionado = button.Tag as string;
                // Realiza alguna acción con el código de la sede seleccionada
            }
        }

        private void SelectorSede_Loaded(object sender, RoutedEventArgs e)
        {
            // Si IsEnabled=False salen los botones el gris, por eso usamos IsHitTestVisible
            if (!IsEnabled)
            {
                IsEnabled = true;
                IsHitTestVisible = false;
            }
        }
    }

    public class SedeMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || values[0] == null || values[1] == null)
                return false;

            string selectedSede = values[0].ToString();
            string sedeCodigo = values[1].ToString();

            return selectedSede == sedeCodigo;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

