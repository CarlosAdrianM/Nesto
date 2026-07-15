Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta
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

    ''' <summary>
    ''' Nesto#340 (1C.8, slice 4): ficha completa del cliente desde la API (GET Clientes), con
    ''' VendedoresGrupoProducto y PersonasContacto. Sustituye a DbContext.Clientes en el VM.
    ''' </summary>
    Public Async Function LeerCliente(empresa As String, cliente As String, contacto As String) As Task(Of ClienteJson) Implements IClienteComercialService.LeerCliente
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim urlConsulta As String = "Clientes" +
                "?empresa=" + Uri.EscapeDataString(If(empresa?.Trim(), String.Empty)) +
                "&cliente=" + Uri.EscapeDataString(If(cliente?.Trim(), String.Empty)) +
                "&contacto=" + Uri.EscapeDataString(If(contacto?.Trim(), String.Empty))
            Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
            If response.IsSuccessStatusCode Then
                Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                Return JsonConvert.DeserializeObject(Of ClienteJson)(respuesta)
            Else
                Return Nothing
            End If
        End Using
    End Function
End Class
