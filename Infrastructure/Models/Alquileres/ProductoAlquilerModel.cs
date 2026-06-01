namespace Nesto.Infrastructure.Models.Alquileres
{
    /// <summary>
    /// Una fila de la lista de productos en alquiler. Equivale al antiguo complejo EDMX
    /// prdProductosAlquiler, con nombres limpios (Nesto#340, Fase 1C.1). La lista es de solo
    /// lectura en la UI, así que no necesita INotifyPropertyChanged.
    /// </summary>
    public class ProductoAlquilerModel
    {
        public string Empresa { get; set; }
        public string Numero { get; set; }
        public string Nombre { get; set; }
        public int Stock { get; set; }
        public int StockAlquileres { get; set; }
        public int Diferencia { get; set; }
    }
}
