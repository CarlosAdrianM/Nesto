using System;

namespace Nesto.Modules.Producto.Models
{
    public class VideoLookupModel
    {
        public int Id { get; set; }
        public string VideoId { get; set; } // Si no es cliente dejamos en blanco
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaPublicacion { get; set; }
        public bool EsUnProtocolo { get; set; }
        public string UrlVideo { get; set; }
        public string UrlImagen { get; set; }
    }
}
