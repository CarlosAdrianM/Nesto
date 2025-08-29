using System;
using System.Collections.Generic;

namespace Nesto.Modules.Producto.Models
{
    public class VideoModel
    {
        public int Id { get; set; }
        public string VideoId { get; set; } // Si no es cliente dejamos en blanco
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaPublicacion { get; set; }
        public string Protocolo { get; set; }
        public string UrlVideo { get; set; }
        public string UrlImagen { get; set; }

        public List<ProductoVideoModel> Productos { get; set; }
    }
}
