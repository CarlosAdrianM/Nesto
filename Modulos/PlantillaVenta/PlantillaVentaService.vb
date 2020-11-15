Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports System.Text
Imports ControlesUsuario.Models
Imports Nesto.Contratos
Imports Nesto.Models
Imports Nesto.Modulos.Cliente
Imports Newtonsoft.Json

Public Class PlantillaVentaService
    Implements IPlantillaVentaService

    Private ReadOnly configuracion As IConfiguracion
    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
    End Sub

    Public Async Sub EnviarCobroTarjeta(correo As String, movil As String, totalPedido As Decimal, pedido As String) Implements IPlantillaVentaService.EnviarCobroTarjeta
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = String.Empty

            Dim reclamacion As New ReclamacionDeuda With {
                .Asunto = String.Format("Pago pedido {0} de Nueva Visión", pedido),
                .Correo = correo,
                .Importe = totalPedido,
                .Movil = movil,
                .TextoSMS = "Este es un mensaje de @COMERCIO@. Puede pagar el pedido " + pedido + " de @IMPORTE@ @MONEDA@ aquí: @URL@"
            }

            Try
                Dim urlConsulta As String = "ReclamacionDeuda"
                Dim reclamacionJson As String = JsonConvert.SerializeObject(reclamacion)
                Dim content As StringContent = New StringContent(reclamacionJson, Encoding.UTF8, "application/json")
                response = Await client.PostAsync(urlConsulta, content)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                    'reclamacion = JsonConvert.DeserializeObject(Of ReclamacionDeuda)(respuesta)
                    'If reclamacion.TramitadoOK Then
                    '   EnlaceReclamarDeuda = reclamacion.Enlace
                    'End If
                Else
                    respuesta = String.Empty
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido procesar la reclamación de deuda")
            Finally

            End Try
        End Using

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

    Public Async Function CargarCliente(empresa As String, cliente As String, contacto As String) As Task(Of ClienteCrear) Implements IPlantillaVentaService.CargarCliente
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage


            Try

                response = Await client.GetAsync(String.Format("Clientes/GetClienteCrear?empresa={0}&cliente={1}&contacto={2}", empresa, cliente, contacto))

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Return JsonConvert.DeserializeObject(Of ClienteCrear)(cadenaJson)
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
