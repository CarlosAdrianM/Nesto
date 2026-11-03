Imports System.Globalization
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Inventario.InventarioModel
Imports Newtonsoft.Json

''' <summary>
''' Nesto#596 (gemela de NestoAPI#789): las llamadas de la pantalla de inventario a api/Inventarios, fuera del ViewModel para
''' poder probarlo con el servicio falseado.
''' </summary>
Public Interface IServicioInventario
    ''' <summary>Lo pendiente de contabilizar del almacén ese día, por usuario (GET api/Inventarios/Pendientes).</summary>
    Function LeerPendientes(empresa As String, almacen As String, fecha As Date) As Task(Of List(Of InventarioPendienteDTO))
    ''' <summary>POST api/Inventarios/Rellenar. Lanza <see cref="InventarioYaRellenadoException"/> si ya estaba rellenado (409).</summary>
    Function Rellenar(empresa As String, almacen As String, fecha As Date) As Task(Of RellenarInventarioRespuestaDTO)
    ''' <summary>La línea del día del producto leído; Nothing si aún no tiene (o si lo leído no es ningún producto).</summary>
    Function BuscarLinea(empresa As String, almacen As String, fecha As Date, producto As String) As Task(Of InventarioDTO)
    Function LeerLineas(empresa As String, almacen As String, fecha As Date) As Task(Of List(Of InventarioDTO))
    ''' <summary>POST api/Inventarios: devuelve la línea grabada (con la referencia y PorCodigoBarras que decide la API).</summary>
    Function CrearLinea(linea As InventarioDTO) As Task(Of InventarioDTO)
    ''' <summary>PUT api/Inventarios/{NºOrden}?porCodigoBarras=</summary>
    Function ActualizarLinea(linea As InventarioDTO, porCodigoBarras As Boolean?) As Task
    ''' <summary>NestoAPI#790: GET api/Inventarios/Comprobaciones (bloqueantes, avisos y recuento por usuario).</summary>
    Function Comprobaciones(empresa As String, almacen As String, fecha As Date) As Task(Of ComprobacionesInventarioDTO)
    ''' <summary>
    ''' NestoAPI#790: POST api/Inventarios/Contabilizar. Con bloqueantes (409) no lanza: devuelve sus comprobaciones en
    ''' <see cref="ResultadoContabilizarInventario.Bloqueado"/>. Los demás errores (400 sin pendientes, 403…) sí lanzan.
    ''' </summary>
    Function Contabilizar(empresa As String, almacen As String, fecha As Date) As Task(Of ResultadoContabilizarInventario)
    ''' <summary>
    ''' NestoAPI#790: GET api/Inventarios/Diferencias. Con <paramref name="traspasos"/>, los contabilizados en ellos; sin ellos,
    ''' las filas pendientes de <paramref name="fecha"/>. La valoración (PVP o coste) la decide la API.
    ''' </summary>
    Function Diferencias(empresa As String, almacen As String, fecha As Date?, traspasos As IEnumerable(Of Integer)) As Task(Of DiferenciasInventarioDTO)
End Interface

''' <summary>NestoAPI#789: el inventario ya tenía filas pendientes ese día (lo ha rellenado otro mientras tanto).</summary>
Public Class InventarioYaRellenadoException
    Inherits Exception

    Public Sub New(mensaje As String)
        MyBase.New(mensaje)
    End Sub
End Class

Public Class ServicioInventario
    Implements IServicioInventario

    Private ReadOnly _clienteApiFactory As IClienteApiFactory
    Private Shared ReadOnly CulturaApi As New CultureInfo("en-US")

    Public Sub New(clienteApiFactory As IClienteApiFactory)
        _clienteApiFactory = clienteApiFactory
    End Sub

    Public Async Function LeerPendientes(empresa As String, almacen As String, fecha As Date) As Task(Of List(Of InventarioPendienteDTO)) Implements IServicioInventario.LeerPendientes
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim url = $"Inventarios/Pendientes?empresa={Uri.EscapeDataString(empresa)}&almacen={Uri.EscapeDataString(almacen)}&fecha={TextoFecha(fecha)}"
            Dim response = Await client.GetAsync(url)
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            Return If(JsonConvert.DeserializeObject(Of List(Of InventarioPendienteDTO))(cuerpo), New List(Of InventarioPendienteDTO))
        End Using
    End Function

    Public Async Function Rellenar(empresa As String, almacen As String, fecha As Date) As Task(Of RellenarInventarioRespuestaDTO) Implements IServicioInventario.Rellenar
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim peticion = New With {.Empresa = empresa, .Almacen = almacen, .Fecha = fecha.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json")
            Dim response = Await client.PostAsync("Inventarios/Rellenar", content)
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            If response.StatusCode = HttpStatusCode.Conflict Then
                Throw New InventarioYaRellenadoException(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            Return JsonConvert.DeserializeObject(Of RellenarInventarioRespuestaDTO)(cuerpo)
        End Using
    End Function

    Public Async Function BuscarLinea(empresa As String, almacen As String, fecha As Date, producto As String) As Task(Of InventarioDTO) Implements IServicioInventario.BuscarLinea
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim url = $"Inventarios?empresa={Uri.EscapeDataString(empresa)}&almacen={Uri.EscapeDataString(almacen)}&fecha={TextoFecha(fecha)}&producto={Uri.EscapeDataString(If(producto, String.Empty))}"
            Dim response = Await client.GetAsync(url)
            If Not response.IsSuccessStatusCode Then
                ' Sin línea (404) o lo leído no es nada / lo comparten varios: lo dirá el POST con su mensaje.
                Return Nothing
            End If
            Return JsonConvert.DeserializeObject(Of InventarioDTO)(Await response.Content.ReadAsStringAsync())
        End Using
    End Function

    Public Async Function LeerLineas(empresa As String, almacen As String, fecha As Date) As Task(Of List(Of InventarioDTO)) Implements IServicioInventario.LeerLineas
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim url = $"Inventarios?empresa={Uri.EscapeDataString(empresa)}&almacen={Uri.EscapeDataString(almacen)}&fecha={TextoFecha(fecha)}"
            Dim response = Await client.GetAsync(url)
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            Return If(JsonConvert.DeserializeObject(Of List(Of InventarioDTO))(cuerpo), New List(Of InventarioDTO))
        End Using
    End Function

    Public Async Function CrearLinea(linea As InventarioDTO) As Task(Of InventarioDTO) Implements IServicioInventario.CrearLinea
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(linea), Encoding.UTF8, "application/json")
            Dim response = Await client.PostAsync("Inventarios", content)
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            Return JsonConvert.DeserializeObject(Of InventarioDTO)(cuerpo)
        End Using
    End Function

    Public Async Function ActualizarLinea(linea As InventarioDTO, porCodigoBarras As Boolean?) As Task Implements IServicioInventario.ActualizarLinea
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(linea), Encoding.UTF8, "application/json")
            Dim url = "Inventarios/" + linea.NumOrden.ToString(CultureInfo.InvariantCulture)
            If porCodigoBarras.HasValue Then
                url += "?porCodigoBarras=" + If(porCodigoBarras.Value, "true", "false")
            End If
            Dim response = Await client.PutAsync(url, content)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(Await response.Content.ReadAsStringAsync()))
            End If
        End Using
    End Function

    Public Async Function Comprobaciones(empresa As String, almacen As String, fecha As Date) As Task(Of ComprobacionesInventarioDTO) Implements IServicioInventario.Comprobaciones
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim url = $"Inventarios/Comprobaciones?empresa={Uri.EscapeDataString(empresa)}&almacen={Uri.EscapeDataString(almacen)}&fecha={TextoFecha(fecha)}"
            Dim response = Await client.GetAsync(url)
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            Return If(JsonConvert.DeserializeObject(Of ComprobacionesInventarioDTO)(cuerpo), New ComprobacionesInventarioDTO With {.Almacen = almacen, .Fecha = fecha})
        End Using
    End Function

    Public Async Function Contabilizar(empresa As String, almacen As String, fecha As Date) As Task(Of ResultadoContabilizarInventario) Implements IServicioInventario.Contabilizar
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim peticion = New With {.Empresa = empresa, .Almacen = almacen, .Fecha = fecha.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json")
            Dim response = Await client.PostAsync("Inventarios/Contabilizar", content)
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            If response.StatusCode = HttpStatusCode.Conflict Then
                Dim bloqueado = JsonConvert.DeserializeObject(Of ComprobacionesInventarioDTO)(cuerpo)
                If bloqueado Is Nothing Then
                    Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
                End If
                Return New ResultadoContabilizarInventario With {.Bloqueado = bloqueado}
            End If
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            Return New ResultadoContabilizarInventario With {
                .Contabilizacion = If(JsonConvert.DeserializeObject(Of ContabilizacionInventarioDTO)(cuerpo), New ContabilizacionInventarioDTO With {.Almacen = almacen, .Fecha = fecha})
            }
        End Using
    End Function

    Public Async Function Diferencias(empresa As String, almacen As String, fecha As Date?, traspasos As IEnumerable(Of Integer)) As Task(Of DiferenciasInventarioDTO) Implements IServicioInventario.Diferencias
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim url = $"Inventarios/Diferencias?empresa={Uri.EscapeDataString(empresa)}&almacen={Uri.EscapeDataString(almacen)}"
            If fecha.HasValue Then
                url += "&fecha=" + TextoFecha(fecha.Value)
            End If
            For Each traspaso In If(traspasos, Enumerable.Empty(Of Integer)())
                url += "&traspaso=" + traspaso.ToString(CultureInfo.InvariantCulture)
            Next
            Dim response = Await client.GetAsync(url)
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            Return If(JsonConvert.DeserializeObject(Of DiferenciasInventarioDTO)(cuerpo), New DiferenciasInventarioDTO With {.Almacen = almacen, .Fecha = fecha})
        End Using
    End Function

    Private Shared Function TextoFecha(dia As Date) As String
        Return Uri.EscapeDataString(dia.ToString("d", CulturaApi))
    End Function
End Class
