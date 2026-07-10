namespace Nesto.Modules.Producto.Models
{
    /// <summary>
    /// NestoAPI#249: grupo alternativo por el que puede comisionar un producto marcado (además del
    /// de su ficha). Se mantiene desde la pestaña Comisiones de la ficha del producto.
    /// </summary>
    public class GrupoComisionableModel
    {
        public string Grupo { get; set; }
        public bool Seleccionado { get; set; }
    }
}
