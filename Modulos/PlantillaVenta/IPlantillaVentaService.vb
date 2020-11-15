Imports Nesto.Models
Imports Nesto.Modulos.Cliente

Public Interface IPlantillaVentaService
    Function CargarCliente(empresa As String, cliente As String, contacto As String) As Task(Of ClienteCrear)
    Function CargarClientesVendedor(filtroCliente As String, vendedor As String, todosLosVendedores As Boolean) As Task(Of ICollection(Of ClienteJson))
    Sub EnviarCobroTarjeta(cobroTarjetaCorreo As String, cobroTarjetaMovil As String, totalPedido As Decimal, pedido As String)
End Interface
