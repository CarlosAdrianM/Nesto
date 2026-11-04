Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Newtonsoft.Json

Namespace Services
    ''' <summary>Nesto#615 (NestoAPI#810): PUT api/EnviosAgencias/{id}/Reembolso.</summary>
    Public Class ServicioReembolsoEnvios
        Implements IServicioReembolsoEnvios

        Private ReadOnly _clienteApiFactory As IClienteApiFactory

        Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
            _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
        End Sub

        Friend Sub New(clienteApiFactory As IClienteApiFactory)
            _clienteApiFactory = clienteApiFactory
        End Sub

        Public Async Function CambiarReembolso(envio As Integer, datos As CambiarReembolsoEnvioDTO) As Task(Of ReembolsoEnvioCambiadoDTO) Implements IServicioReembolsoEnvios.CambiarReembolso
            Using client As HttpClient = _clienteApiFactory.Crear()
                Dim content As New StringContent(JsonConvert.SerializeObject(datos), Encoding.UTF8, "application/json")
                Dim response As HttpResponseMessage = Await client.PutAsync($"EnviosAgencias/{envio}/Reembolso", content)
                Dim body As String = Await response.Content.ReadAsStringAsync()
                If Not response.IsSuccessStatusCode Then
                    Throw New Exception(ConsultaReembolso.ExtraerMensaje(body, $"Error {CInt(response.StatusCode)} de la API."))
                End If
                Return JsonConvert.DeserializeObject(Of ReembolsoEnvioCambiadoDTO)(body)
            End Using
        End Function
    End Class
End Namespace
