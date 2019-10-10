Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports Nesto.Contratos
Imports Nesto.Models
Imports Nesto.Modulos.PlantillaVenta
Imports Newtonsoft.Json

Public Class PlantillaVentaService
    Implements IPlantillaVentaService

    Private ReadOnly configuracion As IConfiguracion
    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
    End Sub
    Public Async Function CargarClientesVendedor(filtroCliente As String, vendedor As String, todosLosVendedores As Boolean) As Task(Of ICollection(Of ClienteJson)) Implements IPlantillaVentaService.CargarClientesVendedor
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage


            Try
                If todosLosVendedores Then
                    response = Await client.GetAsync("Clientes?empresa=1&filtro=" + filtroCliente)
                Else
                    response = Await client.GetAsync("Clientes?empresa=1&vendedor=" + vendedor + "&filtro=" + filtroCliente)
                End If


                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Return JsonConvert.DeserializeObject(Of ObservableCollection(Of ClienteJson))(cadenaJson)
                Else
                    Throw New Exception("Se ha producido un error al cargar los clientes")
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            Finally

            End Try
        End Using
    End Function
End Class
