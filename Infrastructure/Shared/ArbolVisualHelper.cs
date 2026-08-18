using System.Windows;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#435: en los MouseDoubleClick de los DataGrid, e.OriginalSource puede ser un
    /// Run (el texto de la celda), que es un ContentElement y NO un Visual:
    /// VisualTreeHelper.GetParent lanza InvalidOperationException y tiraba la aplicación
    /// entera. Este helper sube un nivel por el árbol admitiendo ambos mundos.
    /// </summary>
    public static class ArbolVisualHelper
    {
        /// <summary>
        /// Devuelve el padre del elemento (visual o lógico según el tipo), o null si no es
        /// un DependencyObject o no tiene padre. Nunca lanza.
        /// </summary>
        public static DependencyObject ObtenerPadreSeguro(object elemento)
        {
            if (elemento is System.Windows.Media.Visual || elemento is System.Windows.Media.Media3D.Visual3D)
            {
                return System.Windows.Media.VisualTreeHelper.GetParent((DependencyObject)elemento);
            }
            if (elemento is DependencyObject dependencyObject)
            {
                return LogicalTreeHelper.GetParent(dependencyObject);
            }
            return null;
        }
    }
}
