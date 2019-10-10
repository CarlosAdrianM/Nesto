Imports Nesto.Models

Public Interface IPlantillaVentaService
    Function CargarClientesVendedor(filtroCliente As String, vendedor As String, todosLosVendedores As Boolean) As Task(Of ICollection(Of ClienteJson))
End Interface
