Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class ClienteComercialService
    Implements IClienteComercialService
    Public ReadOnly Property Configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion
    Private ReadOnly _clienteApiFactory As IClienteApiFactory

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.Configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
        _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
    End Sub

    Public Async Function ModificarExtractoCliente(extracto As ExtractoClienteDTO) As Task Implements IClienteComercialService.ModificarExtractoCliente

        If IsNothing(extracto) Then
            Throw New Exception("No se puede actualizar un extracto nulo")
        End If
        Using client As HttpClient = _clienteApiFactory.Crear()

            ' Carlos 21/11/24: Agregar autenticación
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Dim urlConsulta As String = "ExtractosCliente"
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(extracto), Encoding.UTF8, "application/json")

            response = client.PutAsync(urlConsulta, content).Result


            If response.IsSuccessStatusCode Then
                respuesta = response.Content.ReadAsStringAsync().Result
            Else
                Dim respuestaError = response.Content.ReadAsStringAsync().Result
                Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                ' Carlos 21/11/24: Usar HttpErrorHelper para parsear errores del API
                Dim contenido As String = HttpErrorHelper.ParsearErrorHttp(detallesError)
                Throw New Exception(contenido)
            End If
        End Using

    End Function
End Class
