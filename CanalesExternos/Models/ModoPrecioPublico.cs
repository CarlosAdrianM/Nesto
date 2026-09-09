namespace Nesto.Modulos.CanalesExternos.Models
{
    /// <summary>
    /// Nesto#453: los tres modos que admite <c>PrestashopProductos.PVP_IVA_Incluido</c> desde el
    /// cutover de precios de NestoSync 1.4.0. En la BD viajan como un número: positivo (precio fijo),
    /// NULL (el módulo deriva el público con el descuento por defecto) o -1 (público = profesional,
    /// sentinel <c>Constantes.Productos.PVP_IVA_MISMO_QUE_PROFESIONAL</c>).
    /// </summary>
    public enum ModoPrecioPublico
    {
        PrecioFijo,
        DescuentoPorDefecto,
        MismoQueProfesional
    }
}
