using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#372: diálogo "Qué hay de nuevo" con el changelog de usuario.
    /// </summary>
    public partial class NovedadesDialog : UserControl
    {
        public NovedadesDialog()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        // NestoAPI#520: Ctrl+V en el cuadro de comentario pega la captura si lo copiado es una imagen;
        // si es texto, la tecla sigue su curso normal y pega el texto.
        private void CuadroComentario_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (EsCtrlV(e)
                && (sender as FrameworkElement)?.DataContext is NovedadItem novedad
                && novedad.PegarImagen())
            {
                e.Handled = true;
            }
        }

        // Nesto#487: lo mismo en el cuadro de sugerir una característica.
        private void CuadroSugerencia_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (EsCtrlV(e) && DataContext is NovedadesDialogViewModel vm && vm.PegarImagenSugerencia())
            {
                e.Handled = true;
            }
        }

        private static bool EsCtrlV(KeyEventArgs e) => e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control;

        // Nesto#487: la novedad elegida en el buscador se hace visible en la lista (cuando ya está pintada).
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged anterior)
            {
                anterior.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (e.NewValue is INotifyPropertyChanged nuevo)
            {
                nuevo.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(NovedadesDialogViewModel.NovedadDestacada)
                || !(sender is NovedadesDialogViewModel vm) || vm.NovedadDestacada == null)
            {
                return;
            }
            NovedadItem destacada = vm.NovedadDestacada;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new System.Action(() =>
            {
                (ListaNovedades.ItemContainerGenerator.ContainerFromItem(destacada) as FrameworkElement)?.BringIntoView();
            }));
        }
    }
}
