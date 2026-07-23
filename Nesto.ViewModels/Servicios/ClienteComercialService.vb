Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
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
    ''' Nesto#340 (1C.8, último resto EF del VM): empresas para el combo de la cabecera.
    ''' Mismo GET Empresas que usa RemesasService.
    ''' </summary>
    Public Async Function LeerEmpresas() As Task(Of List(Of EmpresaModel)) Implements IClienteComercialService.LeerEmpresas
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response = Await client.GetAsync("Empresas")
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener la lista de empresas: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of EmpresaModel))(body)
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

    ''' <summary>
    ''' Nesto#340 (1C.8, slice 5): CCCs del cliente/contacto desde la API. Los setters de
    ''' CCCModel marcan EsModificado al deserializar, así que se resetea antes de devolver.
    ''' </summary>
    Public Async Function LeerCCCs(empresa As String, cliente As String, contacto As String) As Task(Of List(Of CCCModel)) Implements IClienteComercialService.LeerCCCs
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim urlConsulta As String = "Clientes/CCCs" +
                "?empresa=" + Uri.EscapeDataString(If(empresa?.Trim(), String.Empty)) +
                "&cliente=" + Uri.EscapeDataString(If(cliente?.Trim(), String.Empty)) +
                "&contacto=" + Uri.EscapeDataString(If(contacto?.Trim(), String.Empty))
            Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
            If Not response.IsSuccessStatusCode Then
                Return New List(Of CCCModel)
            End If
            Dim respuesta As String = Await response.Content.ReadAsStringAsync()
            Dim cccs As List(Of CCCModel) = JsonConvert.DeserializeObject(Of List(Of CCCModel))(respuesta)
            If IsNothing(cccs) Then
                Return New List(Of CCCModel)
            End If
            For Each ccc In cccs
                ccc.EsModificado = False
            Next
            Return cccs
        End Using
    End Function

    Public Async Function LeerEstadosCCC(empresa As String) As Task(Of List(Of EstadoCCCModel)) Implements IClienteComercialService.LeerEstadosCCC
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim urlConsulta As String = "Clientes/EstadosCCC?empresa=" + Uri.EscapeDataString(If(empresa?.Trim(), String.Empty))
            Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
            If Not response.IsSuccessStatusCode Then
                Return New List(Of EstadoCCCModel)
            End If
            Dim respuesta As String = Await response.Content.ReadAsStringAsync()
            Return If(JsonConvert.DeserializeObject(Of List(Of EstadoCCCModel))(respuesta), New List(Of EstadoCCCModel))
        End Using
    End Function

    Public Async Function GuardarCCCs(peticion As GuardarCCCsRequest) As Task(Of GuardarCCCsRespuesta) Implements IClienteComercialService.GuardarCCCs
        If IsNothing(peticion) Then
            Throw New Exception("No se puede guardar una petición de CCCs nula")
        End If
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PutAsync("Clientes/CCCs", content)
            Dim respuesta As String = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuesta)
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(detallesError))
            End If
            Return JsonConvert.DeserializeObject(Of GuardarCCCsRespuesta)(respuesta)
        End Using
    End Function
End Class
