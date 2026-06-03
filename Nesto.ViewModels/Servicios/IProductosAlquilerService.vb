Imports Nesto.Infrastructure.Models.Alquileres

Public Interface IProductosAlquilerService
    Function LeerProductosAlquiler() As Task(Of List(Of ProductoAlquilerModel))
    Function LeerMovimientosAlquiler(empresa As String, pedido As Integer) As Task(Of List(Of MovimientoAlquilerModel))
    Function LeerComprasAlquiler(producto As String, numSerie As String) As Task(Of List(Of CompraAlquilerModel))
End Interface
