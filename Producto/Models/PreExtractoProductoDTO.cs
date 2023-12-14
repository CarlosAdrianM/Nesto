//using Nesto.Infrastructure.Shared;
//using System.Collections.Generic;
//using System.Linq;

//namespace Nesto.Modules.Producto.Models
//{
//    public class PreExtractoProductoDTO
//    {
//        public PreExtractoProductoDTO() {
//            Ubicaciones = new();
//        }
//        public string Empresa { get; set; }
//        public string Almacen { get;set; }
//        public string Diario { get; set; }
//        public string Producto { get; set; }
//        public int Cantidad { get; set; }
//        public int CantidadPendiente => Cantidad - Ubicaciones.Where(u => u.Estado == Constantes.Ubicaciones.ESTADO_REGISTRO_MONTAR_KITS).Sum(u => u.Cantidad);
//        public string Pasillo { get; set; }
//        public string Fila { get; set; }
//        public string Columna { get; set; }
//        public List<UbicacionProductoDTO> Ubicaciones { get; set; }
//    }
//}
