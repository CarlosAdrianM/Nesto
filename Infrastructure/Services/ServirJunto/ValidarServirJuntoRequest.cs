using System.Collections.Generic;

namespace Nesto.Infrastructure.Services.ServirJunto
{
    public class ValidarServirJuntoRequest
    {
        public string Almacen { get; set; }
        public List<string> ProductosBonificados { get; set; }
        public List<ProductoBonificadoConCantidadRequest> ProductosBonificadosConCantidad { get; set; }
        public List<ProductoBonificadoConCantidadRequest> LineasPedido { get; set; }
    }
}
