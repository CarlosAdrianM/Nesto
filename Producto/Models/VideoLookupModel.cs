using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Nesto.Modules.Producto.Models
{
    public class VideoLookupModel : ObservableObject
    {
        public int Id { get; set; }
        public string VideoId { get; set; } // Si no es cliente dejamos en blanco
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaPublicacion { get; set; }
        public bool EsUnProtocolo { get; set; }
        public string UrlVideo { get; set; }
        public string UrlImagen { get; set; }

        // Nesto#497: hay otro vídeo en la lista con el mismo VideoId de YouTube. No viene de la API:
        // lo marca la ventana al cargar, y puede cambiar al cargar más páginas.
        private bool _esDuplicado;
        public bool EsDuplicado
        {
            get => _esDuplicado;
            set => SetProperty(ref _esDuplicado, value);
        }
    }
}
