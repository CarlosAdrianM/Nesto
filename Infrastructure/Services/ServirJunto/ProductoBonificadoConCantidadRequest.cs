namespace Nesto.Infrastructure.Services.ServirJunto
{
    public class ProductoBonificadoConCantidadRequest
    {
        public string ProductoId { get; set; }
        public int Cantidad { get; set; }

        // NestoAPI#175: marca candidatos a bonificado Ganavisiones dentro de LineasPedido
        // (líneas a 0 EUR sin oferta). El servidor confirma contra la tabla Ganavision y
        // descarta los que no estén registrados, así que se puede rellenar con criterio
        // local sin miedo a falsos positivos.
        public bool EsBonificadoGanavisiones { get; set; }
    }
}
