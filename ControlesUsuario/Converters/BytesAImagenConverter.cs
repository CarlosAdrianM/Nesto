using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ControlesUsuario.Converters
{
    /// <summary>
    /// Nesto#522: los bytes de una foto (JPG) a una imagen para la vista. El parámetro es el ancho en píxeles al que se
    /// decodifica (una miniatura no necesita la foto entera en memoria); sin parámetro, a su tamaño. Bytes vacíos o que no
    /// son una imagen: nada (null), nunca una excepción.
    /// </summary>
    public class BytesAImagenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not byte[] datos || datos.Length == 0)
            {
                return null;
            }
            try
            {
                var imagen = new BitmapImage();
                using (var flujo = new MemoryStream(datos))
                {
                    imagen.BeginInit();
                    imagen.CacheOption = BitmapCacheOption.OnLoad; // se lee entero ahora: el flujo se puede cerrar
                    if (parameter != null && int.TryParse(parameter.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ancho) && ancho > 0)
                    {
                        imagen.DecodePixelWidth = ancho;
                    }
                    imagen.StreamSource = flujo;
                    imagen.EndInit();
                }
                imagen.Freeze();
                return imagen;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
