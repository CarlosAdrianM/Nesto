using System.Windows;

namespace ControlesUsuario
{
    /// <summary>
    /// Proxy para permitir bindings a DataContext desde elementos que no están en el árbol visual,
    /// como DataGridColumn.
    ///
    /// Uso en XAML:
    /// 1. Añadir como recurso: <controles:BindingProxy x:Key="proxy" Data="{Binding}"/>
    /// 2. Usar en binding: Visibility="{Binding Data.MiPropiedad, Source={StaticResource proxy}, Converter=...}"
    /// </summary>
    public class BindingProxy : Freezable
    {
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        public object Data
        {
            get { return GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
    }
}
