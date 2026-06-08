Imports Nesto.Infrastructure.Models.Alquileres

Public Interface IProductosAlquilerService
    Function LeerProductosAlquiler() As Task(Of List(Of ProductoAlquilerModel))
    Function LeerMovimientosAlquiler(empresa As String, pedido As Integer) As Task(Of List(Of MovimientoAlquilerModel))
    Function LeerComprasAlquiler(producto As String, numSerie As String) As Task(Of List(Of CompraAlquilerModel))
    Function LeerInmovilizadosAlquiler(empresa As String, numero As String) As Task(Of List(Of ExtractoInmovilizadoModel))
    ' Nesto#340 Fase 1C.3: grid principal (cabeceras editables del producto).
    Function LeerCabecerasAlquiler(empresa As String, producto As String) As Task(Of List(Of AlquilerModel))
    Function GuardarCabecerasAlquiler(empresa As String, producto As String, cabeceras As List(Of AlquilerModel)) As Task(Of List(Of AlquilerModel))
End Interface
