using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Una dirección del alta (19/11/26): la misma vista para la fiscal y la de entrega, con el teclado del buscador de Google (Nesto#409)
    /// y el foco al borrar la dirección (Nesto#480). DataContext = <see cref="BloqueDireccionAlta"/>.
    /// </summary>
    public partial class BloqueDireccionAltaView : UserControl
    {
        private BloqueDireccionAlta bloque;

        public BloqueDireccionAltaView()
        {
            InitializeComponent();
            DataContextChanged += AlCambiarDataContext;
        }

        private void AlCambiarDataContext(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (bloque != null)
            {
                bloque.FocoEnDireccionSolicitado -= PonerFocoEnDireccion;
            }
            bloque = DataContext as BloqueDireccionAlta;
            if (bloque != null)
            {
                bloque.FocoEnDireccionSolicitado += PonerFocoEnDireccion;
            }
        }

        /// <summary>Lleva el foco al cuadro de la dirección (al entrar en «Datos generales» o al borrar la dirección).</summary>
        public void EnfocarDireccion()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate ()
            {
                if (txtDireccion.IsVisible)
                {
                    txtDireccion.Focus();
                    Keyboard.Focus(txtDireccion);
                }
            }));
        }

        private void PonerFocoEnDireccion(object sender, EventArgs e) => EnfocarDireccion();

        // Nesto#409: navegación por teclado del combo de sugerencias SIN sacar el foco del cuadro.
        private void TxtDireccion_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (bloque == null || !bloque.HaySugerenciasDireccion)
            {
                return;
            }
            switch (e.Key)
            {
                case Key.Down:
                    bloque.MoverSeleccionSugerencias(1);
                    lstSugerenciasDireccion.ScrollIntoView(bloque.SugerenciaDireccionSeleccionada);
                    e.Handled = true;
                    break;
                case Key.Up:
                    bloque.MoverSeleccionSugerencias(-1);
                    lstSugerenciasDireccion.ScrollIntoView(bloque.SugerenciaDireccionSeleccionada);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    bloque.CerrarSugerenciasDireccion();
                    e.Handled = true;
                    break;
                case Key.Enter:
                    // Aplica la resaltada; el KeyUp moverá el foco al siguiente campo
                    _ = bloque.AplicarSugerenciaSeleccionadaAsync();
                    break;
            }
        }

        // Nesto#409: el clic en una sugerencia la aplica (la selección por sí sola es solo resaltado)
        private async void LstSugerenciasDireccion_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (bloque != null)
            {
                _ = await bloque.AplicarSugerenciaSeleccionadaAsync();
                Keyboard.Focus(txtDireccion); // devolver el teclado al cuadro
            }
        }

        // NestoAPI#596: lo tecleado («4480 670») se sustituye por el canónico («4480-670») al salir del cuadro
        private void TxtCodigoPostal_LostFocus(object sender, RoutedEventArgs e) => bloque?.NormalizarCodigoPostal();

        private void TxtSiguienteConIntro_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.OriginalSource is UIElement uiElement)
            {
                e.Handled = true;
                uiElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }
    }
}
