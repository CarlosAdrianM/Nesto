using System;

namespace Nesto.Modules.Producto.Models
{
    public class UbicacionProductoDTO
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Producto { get; set; }
        public string Almacen { get; set; }
        public int Cantidad { get; set; }
        public int Estado { get; set; }
        public string Pasillo { get; set; }
        public string Fila { get; set; }
        public string Columna { get; set; }

        // crear campo calculado CantidadPendiente que lea las ubicaciones en estado registro montarkit y las reste de cantidad (hacer primero el test).

        public UbicacionProductoDTO Clone()
        {
            return new UbicacionProductoDTO
            {
                Empresa = Empresa,
                Producto = Producto,
                Almacen = Almacen,
                Cantidad = Cantidad,
                Estado = Estado,
                Pasillo = Pasillo,
                Fila = Fila,
                Columna = Columna
            };
        }
    }
}
