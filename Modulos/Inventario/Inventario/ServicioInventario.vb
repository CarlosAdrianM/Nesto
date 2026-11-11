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
    ''' <summary>
    ''' POST api/Inventarios/Rellenar. Lanza <see cref="InventarioYaRellenadoException"/> si ya estaba rellenado (409
    ''' YaRellenado) y <see cref="InventarioYaContabilizadoException"/> si ese día ya se contabilizó (409 YaContabilizado,
    ''' NestoAPI#784): solo con <paramref name="otraVez"/> («Contar otra vez») se rellena de nuevo.
    ''' </summary>
    Function Rellenar(empresa As String, almacen As String, fecha As Date, otraVez As Boolean) As Task(Of RellenarInventarioRespuestaDTO)
    ''' <summary>
    ''' La línea pendiente del día del producto leído; Nothing SOLO con el 404 de la API (aún no tiene fila, o lo leído no es
    ''' ningún producto: lo dirá el POST). NestoAPI#784: un 409 (FilasDuplicadas, VariasUbicaciones, código de barras ambiguo)
    ''' lanza <see cref="ConflictoInventarioException"/> y cualquier otro error lanza: nunca se crea otra fila a ciegas.
    ''' </summary>
    Function BuscarLinea(empresa As String, almacen As String, fecha As Date, producto As String) As Task(Of InventarioDTO)
    Function LeerLineas(empresa As String, almacen As String, fecha As Date) As Task(Of List(Of InventarioDTO))
    ''' <summary>
    ''' POST api/Inventarios: devuelve la línea grabada (con la referencia y PorCodigoBarras que decide la API). NestoAPI#784:
    ''' si el producto ya tiene fila pendiente ese día (409 YaTieneFila) lanza <see cref="ConflictoInventarioException"/>.
    ''' </summary>
    Function CrearLinea(linea As InventarioDTO) As Task(Of InventarioDTO)
    ''' <summary>PUT api/Inventarios/{NºOrden}?porCodigoBarras=</summary>
    Function ActualizarLinea(linea As InventarioDTO, porCodigoBarras As Boolean?) As Task
    ''' <summary>NestoAPI#790: GET api/Inventarios/Comprobaciones (bloqueantes, avisos y recuento por usuario).</summary>
    Function Comprobaciones(empresa As String, almacen As String, fecha As Date) As Task(Of ComprobacionesInventarioDTO)
    ''' <summary>
    ''' NestoAPI#790: POST api/Inventarios/Contabilizar. Con bloqueantes (409) no lanza: devuelve sus comprobaciones en
    ''' <see cref="ResultadoContabilizarInventario.Bloqueado"/>. Los demás errores (400 sin pendientes, 403…) sí lanzan.
    ''' </summary>
    ''' <param name="confirmarContadoCero">NestoAPI#784: el usuario confirma que las filas RecontadaSinContar de verdad tienen 0.</param>
    Function Contabilizar(empresa As String, almacen As String, fecha As Date, confirmarContadoCero As Boolean) As Task(Of ResultadoContabilizarInventario)
    ''' <summary>
    ''' NestoAPI#790: GET api/Inventarios/Diferencias. Con <paramref name="traspasos"/>, los contabilizados en ellos; sin ellos,
    ''' las filas pendientes de <paramref name="fecha"/>. La valoración (PVP o coste) la decide la API.
    ''' </summary>
    Function Diferencias(empresa As String, almacen As String, fecha As Date?, traspasos As IEnumerable(Of Integer)) As Task(Of DiferenciasInventarioDTO)
    ''' <summary>
    ''' Sugerencia 1185: POST api/Inventarios/DejarACero (filas de hoy del producto a 0 en todas sus ubicaciones; con
    ''' <paramref name="simular"/>, solo lo que haría). Un 409 (YaContabilizado, FilasDuplicadas, FilasSinUbicacion) lanza
    ''' <see cref="ConflictoInventarioException"/>; los demás errores, Exception con el mensaje de la API.
    ''' </summary>
    Function DejarACero(empresa As String, almacen As String, producto As String, otraVez As Boolean, quitarFilasSinUbicacion As Boolean, simular As Boolean) As Task(Of DejarACeroInventarioRespuestaDTO)
End Interface

''' <summary>NestoAPI#784: un 409 de api/Inventarios, con su Codigo (<see cref="CodigosConflictoInventario"/>) y sus filas.</summary>
Public Class ConflictoInventarioException
    Inherits Exception

    Public Sub New(mensaje As String, Optional conflicto As ConflictoInventarioDTO = Nothing)
        MyBase.New(mensaje)
        Codigo = conflicto?.Codigo
        PuedeContarOtraVez = conflicto IsNot Nothing AndAlso conflicto.PuedeContarOtraVez
        Lineas = If(conflicto?.Lineas, New List(Of LineaInventarioContadaDTO))
        Contabilizadas = If(conflicto?.Contabilizadas, New List(Of ContabilizadasInventarioUsuarioDTO))
    End Sub

    ''' <summary>Nothing en el 409 del código de barras ambiguo (Message y Productos).</summary>
    Public ReadOnly Property Codigo As String
    Public ReadOnly Property PuedeContarOtraVez As Boolean
    Public ReadOnly Property Lineas As List(Of LineaInventarioContadaDTO)
    Public ReadOnly Property Contabilizadas As List(Of ContabilizadasInventarioUsuarioDTO)

    Public Function EsCodigo(codigoBuscado As String) As Boolean
        Return String.Equals(Codigo, codigoBuscado, StringComparison.OrdinalIgnoreCase)
    End Function
End Class

''' <summary>NestoAPI#789: el inventario ya tenía filas pendientes ese día (lo ha rellenado otro mientras tanto).</summary>
Public Class InventarioYaRellenadoException
    Inherits ConflictoInventarioException

    Public Sub New(mensaje As String, Optional conflicto As ConflictoInventarioDTO = Nothing)
        MyBase.New(mensaje, conflicto)
    End Sub
End Class

''' <summary>
''' NestoAPI#784: ese día ya se contó y contabilizó el almacén. Rellenar otra vez pondría como teórico lo ya contado y, si
''' nadie lo cuenta, contabilizarlo lo deja a cero (el 42341 de Paloma): solo si el usuario dice «Contar otra vez».
''' </summary>
Public Class InventarioYaContabilizadoException
    Inherits ConflictoInventarioException

    Public Sub New(mensaje As String, Optional conflicto As ConflictoInventarioDTO = Nothing)
        MyBase.New(mensaje, conflicto)
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

    Public Async Function Rellenar(empresa As String, almacen As String, fecha As Date, otraVez As Boolean) As Task(Of RellenarInventarioRespuestaDTO) Implements IServicioInventario.Rellenar
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim peticion = New With {.Empresa = empresa, .Almacen = almacen, .Fecha = fecha.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), .OtraVez = otraVez}
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json")
            Dim response = Await client.PostAsync("Inventarios/Rellenar", content)
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            If response.StatusCode = HttpStatusCode.Conflict Then
                Dim conflicto = LeerConflicto(cuerpo)
                Dim mensaje = MensajeConflicto(conflicto, cuerpo)
                If conflicto IsNot Nothing AndAlso String.Equals(conflicto.Codigo, CodigosConflictoInventario.YA_CONTABILIZADO, StringComparison.OrdinalIgnoreCase) Then
                    Throw New InventarioYaContabilizadoException(mensaje, conflicto)
                End If
                ' YaRellenado (o la API de antes de NestoAPI#784, sin Codigo): ya hay filas pendientes, se cuenta igual.
                Throw New InventarioYaRellenadoException(mensaje, conflicto)
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
            If response.StatusCode = HttpStatusCode.NotFound Then
                ' Sin línea (o lo leído no es ningún producto: lo dirá el POST con su mensaje).
                Return Nothing
            End If
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            ' NestoAPI#784 (09/11/26): antes CUALQUIER error era «sin línea» y se creaba otra; así se multiplicaban las filas.
            If response.StatusCode = HttpStatusCode.Conflict Then
                Dim conflicto = LeerConflicto(cuerpo)
                Throw New ConflictoInventarioException(MensajeConflicto(conflicto, cuerpo), conflicto)
            End If
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            Return JsonConvert.DeserializeObject(Of InventarioDTO)(cuerpo)
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
            If response.StatusCode = HttpStatusCode.Conflict Then
                Dim conflicto = LeerConflicto(cuerpo)
                Throw New ConflictoInventarioException(MensajeConflicto(conflicto, cuerpo), conflicto)
            End If
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

    Public Async Function Contabilizar(empresa As String, almacen As String, fecha As Date, confirmarContadoCero As Boolean) As Task(Of ResultadoContabilizarInventario) Implements IServicioInventario.Contabilizar
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim peticion = New With {.Empresa = empresa, .Almacen = almacen, .Fecha = fecha.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), .ConfirmarContadoCero = confirmarContadoCero}
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

    Public Async Function DejarACero(empresa As String, almacen As String, producto As String, otraVez As Boolean, quitarFilasSinUbicacion As Boolean, simular As Boolean) As Task(Of DejarACeroInventarioRespuestaDTO) Implements IServicioInventario.DejarACero
        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim peticion = New With {.Empresa = empresa, .Almacen = almacen, .Producto = producto, .OtraVez = otraVez,
                .QuitarFilasSinUbicacion = quitarFilasSinUbicacion, .Simular = simular}
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json")
            Dim response = Await client.PostAsync("Inventarios/DejarACero", content)
            Dim cuerpo = Await response.Content.ReadAsStringAsync()
            If response.StatusCode = HttpStatusCode.Conflict Then
                Dim conflicto = LeerConflicto(cuerpo)
                Throw New ConflictoInventarioException(MensajeConflicto(conflicto, cuerpo), conflicto)
            End If
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
            End If
            Return JsonConvert.DeserializeObject(Of DejarACeroInventarioRespuestaDTO)(cuerpo)
        End Using
    End Function

    ''' <summary>NestoAPI#784: el cuerpo de un 409; Nothing si no es JSON.</summary>
    Private Shared Function LeerConflicto(cuerpo As String) As ConflictoInventarioDTO
        If String.IsNullOrWhiteSpace(cuerpo) Then
            Return Nothing
        End If
        Try
            Return JsonConvert.DeserializeObject(Of ConflictoInventarioDTO)(cuerpo)
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function MensajeConflicto(conflicto As ConflictoInventarioDTO, cuerpo As String) As String
        If conflicto IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(conflicto.Message) Then
            Return conflicto.Message
        End If
        Return HttpErrorHelper.ParsearErrorHttp(cuerpo)
    End Function

    Private Shared Function TextoFecha(dia As Date) As String
        Return Uri.EscapeDataString(dia.ToString("d", CulturaApi))
    End Function
End Class
