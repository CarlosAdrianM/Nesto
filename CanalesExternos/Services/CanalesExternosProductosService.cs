using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Services
{
    public class CanalesExternosProductosService : ICanalesExternosProductosService
    {
        private readonly IConfiguracion _configuracion;

        public CanalesExternosProductosService(IConfiguracion configuracion)
        {
            _configuracion = configuracion;
        }
        public async Task<ProductoCanalExterno> AddProductoAsync(string productoId)
        {
            using (var db = new NestoEntities())
            {
                var producto = new PrestashopProductos
                {
                    Número = productoId,
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    VistoBueno = _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)
                };

                db.PrestashopProductos.Add(producto);
                await db.SaveChangesAsync();
                return new ProductoCanalExterno
                {
                    ProductoId = producto.Número,
                    VistoBueno = producto.VistoBueno ?? false
                };
            }
        }

        public async Task<ProductoCanalExterno> GetProductoAsync(string productoId)
        {
            using (var db = new NestoEntities())
            {
                var producto = await db.PrestashopProductos.FirstOrDefaultAsync(p => p.Empresa == Constantes.Empresas.EMPRESA_DEFECTO && p.Número == productoId);

                if (producto == null) return null;

                return new ProductoCanalExterno
                {
                    ProductoId = producto.Número,
                    Nombre = producto.Nombre,
                    DescripcionBreve = producto.DescripciónBreve,
                    DescripcionCompleta = producto.Descripción,
                    PvpIvaIncluido = producto.PVP_IVA_Incluido,
                    VistoBueno = producto.VistoBueno ?? false,
                    IsDirty = false
                };
            }
        }

        public async Task<IEnumerable<ProductoCanalExterno>> GetProductosSinVistoBuenoNumeroAsync()
        {
            using (var db = new NestoEntities())
            {
                return await db.PrestashopProductos
                    .Where(p => p.VistoBueno == null || p.VistoBueno == false)
                    .Select(producto => new ProductoCanalExterno
                    {
                        ProductoId = producto.Número,
                        Nombre = producto.Nombre,
                        DescripcionBreve = producto.DescripciónBreve,
                        DescripcionCompleta = producto.Descripción,
                        PvpIvaIncluido = producto.PVP_IVA_Incluido,
                        VistoBueno = producto.VistoBueno ?? false,
                        IsDirty = false
                    })
                    .ToListAsync();
            }
        }

        public async Task SaveProductoAsync(ProductoCanalExterno producto)
        {
            using (var db = new NestoEntities())
            {
                var productoPrestashopDb = await db.PrestashopProductos.FirstOrDefaultAsync(p => p.Empresa == Constantes.Empresas.EMPRESA_DEFECTO && p.Número == producto.ProductoId);

                if (productoPrestashopDb != null)
                {
                    productoPrestashopDb.Nombre = producto.Nombre;
                    productoPrestashopDb.DescripciónBreve = producto.DescripcionBreve;
                    productoPrestashopDb.Descripción = producto.DescripcionCompleta;
                    productoPrestashopDb.PVP_IVA_Incluido = producto.PvpIvaIncluido;
                    productoPrestashopDb.VistoBueno = producto.VistoBueno;
                    await db.SaveChangesAsync();
                    producto.IsDirty = false;
                }
            }
        }
    }
}
