using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class PedidoCompraModel
    {
        public int Id { get; set; }
        public string Proveedor { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string Telefono { get; set; }
        public string Cif { get; set; }
        public DateTime Fecha { get; set; }
        public bool PedidoValorado { get; set; }
        public List<LineaPedidoCompraModel> Lineas { get; set; }



        public static async Task<PedidoCompraModel> CargarDatos(string empresa, int pedido)
        {
            List<PedidoCompraModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                string consulta = "select c.Número Id, rtrim(NºProveedor) Proveedor, rtrim(p.Nombre) Nombre, rtrim(p.Dirección) Direccion, rtrim(p.CodPostal) CodigoPostal, rtrim(p.Población) Poblacion, rtrim(p.Provincia) Provincia, rtrim(p.Teléfono) Telefono, rtrim(p.[CIF/NIF]) Cif, c.Fecha, p.PedidoValorado ";
                consulta += "from CabPedidoCmp c inner ";
                consulta += "join Proveedores p ";
                consulta += "on c.Empresa = p.Empresa and c.NºProveedor = p.Número and c.Contacto = p.Contacto ";
                consulta += $"where c.Empresa = '{empresa}' and c.Número = {pedido}";
                lista = await db.Database.SqlQuery<PedidoCompraModel>(consulta).ToListAsync();

                PedidoCompraModel pedidoCompra = lista.First();
                string proveedor = pedidoCompra.Proveedor;

                string consultaLineas = "select rtrim(p.ReferenciaProv) SuReferencia, rtrim(l.Producto) NuestraReferencia, rtrim(l.Texto) Descripcion, d.Tamaño Tamanno, rtrim(d.UnidadMedida) UnidadMedida, l.Cantidad, l.Precio PrecioUnitario, l.SumaDescuentos, l.BaseImponible ";
                consultaLineas += "from LinPedidoCmp l left join Productos d ";
                consultaLineas += "on l.Empresa = d.Empresa and l.Producto = d.Número ";
                consultaLineas += "left join ProveedoresProducto p ";
                consultaLineas += $"on l.Empresa = p.Empresa and l.Producto = p.[Nº Producto] and p.[Nº Proveedor] = '{proveedor}' ";
                consultaLineas += $"where l.Empresa = '{empresa}' and l.Número = {pedido} ";
                consultaLineas += "order by l.NºOrden";
                try
                {
                    pedidoCompra.Lineas = await db.Database.SqlQuery<LineaPedidoCompraModel>(consultaLineas).ToListAsync();
                    if (!pedidoCompra.PedidoValorado)
                    {
                        foreach (var linea in pedidoCompra.Lineas) {
                            linea.PrecioUnitario = 0;
                            linea.SumaDescuentos = 0;
                            linea.BaseImponible = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                

                return pedidoCompra;
            };
        }
    }

    public class LineaPedidoCompraModel
    {
        public string SuReferencia { get; set; }
        public string NuestraReferencia { get; set; }
        public string Descripcion { get; set; }
        public short? Tamanno { get; set; }
        public string UnidadMedida { get; set; }
        public short? Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal SumaDescuentos { get; set; }
        public decimal BaseImponible { get; set; }
    }
}
