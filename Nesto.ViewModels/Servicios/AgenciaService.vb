Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports System.Text
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Newtonsoft.Json
Imports Prism.Ioc
Imports Prism.Services.Dialogs

Public Class AgenciaService
    Implements IAgenciaService

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _dialogService As IDialogService
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion
    Private ReadOnly _clienteApiFactory As IClienteApiFactory
    Private ReadOnly _servicioAgencias As IServicioAgenciasMantenimiento

    Public Sub New(configuracion As IConfiguracion, dialogService As IDialogService, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _dialogService = dialogService
        _servicioAutenticacion = servicioAutenticacion
        _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
        ' Se reutiliza el cliente que ya existía de api/Agencias en vez de escribir otro:
        ' un único sitio que sepa hablar con ese endpoint.
        _servicioAgencias = New AgenciasMantenimientoService(_clienteApiFactory)
    End Sub

    ''' <summary>Solo para tests (InternalsVisibleTo "ViewModels.Tests"): permite falsear la
    ''' lectura de agencias sin levantar la API ni la configuración.</summary>
    Friend Sub New(servicioAgencias As IServicioAgenciasMantenimiento)
        _servicioAgencias = servicioAgencias
    End Sub

    ' ===== Nesto#340 (Agencias, slice A2): el CRUD de envíos va por la API =====
    ' La entidad viaja SIN navegaciones (ContractResolverSinNavegaciones): una navegación
    ' estampada (p. ej. la AgenciasTransporte de los listados A1.b) haría que el servidor
    ' intentara insertarla como entidad nueva. El PUT devuelve la RowVersion refrescada para
    ' encadenar modificaciones sobre el mismo objeto sin recargar; el DELETE lleva
    ' permitirEnCurso=true (paridad con el borrado EF antiguo, que no tenía guarda de estado)
    ' y borra también la historia de seguimiento en el servidor.

    Private Shared ReadOnly _jsonSinNavegaciones As New JsonSerializerSettings With {
        .ContractResolver = New ContractResolverSinNavegaciones()
    }

    Public Sub Modificar(envio As EnviosAgencia) Implements IAgenciaService.Modificar
        Dim rowVersionNueva As Byte() = Task.Run(
            Async Function() As Task(Of Byte())
                Using client As HttpClient = _clienteApiFactory.Crear()
                    If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                        Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                    End If
                    Dim json As String = JsonConvert.SerializeObject(envio, _jsonSinNavegaciones)
                    Dim content As HttpContent = New StringContent(json, Encoding.UTF8, "application/json")
                    Dim response As HttpResponseMessage = Await client.PutAsync($"EnviosAgencias/{envio.Numero}", content)
                    Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                    If Not response.IsSuccessStatusCode Then
                        Throw New Exception($"No se pudo modificar el envío {envio.Numero} ({CInt(response.StatusCode)}): {cuerpo}")
                    End If
                    Dim refrescado = JsonConvert.DeserializeAnonymousType(cuerpo, New With {Key .RowVersion = CType(Nothing, Byte())})
                    Return refrescado?.RowVersion
                End Using
            End Function).GetAwaiter().GetResult()
        If rowVersionNueva IsNot Nothing Then
            envio.RowVersion = rowVersionNueva
        End If
    End Sub

    ' Consolidación A2 (20/08/26): esto TRAGABA la excepción (ShowError y devolver como si nada)
    ' y los llamadores quitaban el envío de las listas aunque el DELETE hubiera fallado en el
    ' servidor — el grid mentía (mismo patrón que el falso "Etiqueta creada"). Ahora LANZA y
    ' cada llamador decide: mostrar el error sin tocar las listas, o tolerarlo si procede.
    Public Sub Borrar(Id As Integer) Implements IAgenciaService.Borrar
        Task.Run(
            Async Function() As Task
                Using client As HttpClient = _clienteApiFactory.Crear()
                    If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                        Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                    End If
                    Dim response As HttpResponseMessage = Await client.DeleteAsync($"EnviosAgencias/{Id}?permitirEnCurso=true")
                    If Not response.IsSuccessStatusCode Then
                        Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                        Throw New Exception($"No se pudo borrar el envío {Id} ({CInt(response.StatusCode)}): {cuerpo}")
                    End If
                End Using
            End Function).GetAwaiter().GetResult()
    End Sub

    ' ===== Nesto#340 (Agencias, slice A1.b): los listados vienen de la API =====
    ' GET api/EnviosAgencias/... replica los filtros EXACTOS que tenían estas consultas EF
    ' (con tests server-side). Se deserializa sobre la ENTIDAD EnviosAgencia (POCO del EDMX)
    ' con la agencia estampada como navegación mínima: el VM y el XAML (Binding
    ' AgenciasTransporte.Nombre) no cambian, y el DbContext desaparece de los listados.
    ' Las entidades caerán con el EDMX al final de #340.

    Public Function CargarListaPendientes() As IEnumerable(Of EnvioAgenciaWrapper) Implements IAgenciaService.CargarListaPendientes
        Return LeerListadoEnvios("EnviosAgencias/Pendientes").
            Select(Function(envio) EnvioAgenciaWrapper.EnvioAgenciaAWrapper(envio)).ToList()
    End Function

    Public Function Insertar(envio As EnviosAgencia) As EnviosAgencia Implements IAgenciaService.Insertar
        Dim creado As EnviosAgencia = Task.Run(
            Async Function() As Task(Of EnviosAgencia)
                Using client As HttpClient = _clienteApiFactory.Crear()
                    If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                        Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                    End If
                    Dim json As String = JsonConvert.SerializeObject(envio, _jsonSinNavegaciones)
                    Dim content As HttpContent = New StringContent(json, Encoding.UTF8, "application/json")
                    Dim response As HttpResponseMessage = Await client.PostAsync("EnviosAgencias", content)
                    Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                    If Not response.IsSuccessStatusCode Then
                        Throw New Exception($"No se pudo insertar el envío ({CInt(response.StatusCode)}): {cuerpo}")
                    End If
                    Return JsonConvert.DeserializeObject(Of EnviosAgencia)(cuerpo)
                End Using
            End Function).GetAwaiter().GetResult()

        ' El Insertar EF mutaba el MISMO objeto (identity + referencias cargadas): se replica
        ' copiando lo generado por la BD y estampando las navegaciones que usan los consumidores.
        envio.Numero = creado.Numero
        envio.RowVersion = creado.RowVersion
        envio.FechaModificacion = creado.FechaModificacion
        envio.AgenciasTransporte = CargarAgencia(envio.Agencia)
        envio.Empresas = CargarEmpresa(envio.Empresa)
        Return envio
    End Function

    Public Function CargarListaReembolsos(empresa As String, agencia As Integer) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaReembolsos
        Return New ObservableCollection(Of EnviosAgencia)(LeerListadoEnvios(
            $"EnviosAgencias/Reembolsos?empresa={Uri.EscapeDataString(empresa?.Trim())}&agencia={agencia}"))
    End Function

    Public Function CargarListaRetornos(empresa As String, agencia As Integer, tipoDeRetornoExcluido As Integer) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaRetornos
        Return New ObservableCollection(Of EnviosAgencia)(LeerListadoEnvios(
            $"EnviosAgencias/Retornos?empresa={Uri.EscapeDataString(empresa?.Trim())}&agencia={agencia}&tipoRetornoExcluido={tipoDeRetornoExcluido}"))
    End Function

    ' #387: envíos INCIDENTADOS (estado temporal), sin filtro de fecha. Es un estado de paso: deben
    ' avanzar a Entregado o a Devuelto, y en ambos casos salen de esta lista. Los Devueltos (terminales)
    ' NO se incluyen aquí a propósito: se quedarían para siempre y la lista crecería sin fin.
    Public Function CargarListaIncidentados(empresa As String) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaIncidentados
        Return New ObservableCollection(Of EnviosAgencia)(LeerListadoEnvios(
            $"EnviosAgencias/Incidentados?empresa={Uri.EscapeDataString(empresa?.Trim())}"))
    End Function

    ''' <summary>
    ''' Nesto#468: la pestaña Retrasados. El servidor decide qué es "retrasado" (tramitado, sin
    ''' estado terminal, más de diasUmbral días y menos del techo histórico): aquí solo se pinta.
    ''' </summary>
    Public Function CargarListaRetrasados(diasUmbral As Integer, agencia As Integer?, vendedor As String) As List(Of EnvioRetrasadoModel) Implements IAgenciaService.CargarListaRetrasados
        Dim ruta As String = RutaEnviosRetrasados(diasUmbral, agencia, vendedor)
        Return Task.Run(Async Function() As Task(Of List(Of EnvioRetrasadoModel))
                            Using client As HttpClient = _clienteApiFactory.Crear()
                                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                                End If
                                Dim response As HttpResponseMessage = Await client.GetAsync(ruta)
                                Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                                If Not response.IsSuccessStatusCode Then
                                    Throw New Exception($"No se pudieron cargar los envíos retrasados ({CInt(response.StatusCode)}): {cuerpo}")
                                End If
                                Return JsonConvert.DeserializeObject(Of List(Of EnvioRetrasadoModel))(cuerpo)
                            End Using
                        End Function).GetAwaiter().GetResult()
    End Function

    ''' <summary>Aparte para fijar el contrato de nombres en un test. Los filtros vacíos no viajan:
    ''' el servidor los trata como "sin filtro".</summary>
    Friend Shared Function RutaEnviosRetrasados(diasUmbral As Integer, agencia As Integer?, vendedor As String) As String
        Dim ruta As String = $"EnviosAgencias/Retrasados?diasUmbral={diasUmbral}"
        If agencia.HasValue Then
            ruta &= $"&agencia={agencia.Value}"
        End If
        If Not String.IsNullOrWhiteSpace(vendedor) Then
            ruta &= $"&vendedor={Uri.EscapeDataString(vendedor.Trim())}"
        End If
        Return ruta
    End Function

    ' >= TRAMITADO server-side para incluir también Entregado (2) e Incidentado (3) en la pestaña
    ' de tramitados (#387): se distinguen por la columna Estado coloreada.
    Public Function CargarListaEnviosTramitados(empresa As String, agencia As Integer, fechaFiltro As Date) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosTramitados
        Return New ObservableCollection(Of EnviosAgencia)(LeerListadoEnvios(
            $"EnviosAgencias/Tramitados?empresa={Uri.EscapeDataString(empresa?.Trim())}&agencia={agencia}&fecha={fechaFiltro:yyyy-MM-dd}"))
    End Function

    Public Function CargarListaEnviosTramitadosPorFecha(empresa As String, fechaFiltro As Date) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosTramitadosPorFecha
        Return New ObservableCollection(Of EnviosAgencia)(LeerListadoEnvios(
            $"EnviosAgencias/Tramitados?empresa={Uri.EscapeDataString(empresa?.Trim())}&fecha={fechaFiltro:yyyy-MM-dd}"))
    End Function

    Public Function CargarListaEnvios(agencia As Integer) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnvios
        Return New ObservableCollection(Of EnviosAgencia)(LeerListadoEnvios(
            $"EnviosAgencias/EnCurso?agencia={agencia}"))
    End Function

    Public Function CargarListaEnviosTramitadosPorCliente(empresa As String, clienteFiltro As String) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosTramitadosPorCliente
        Return New ObservableCollection(Of EnviosAgencia)(LeerListadoEnvios(
            $"EnviosAgencias/Tramitados?empresa={Uri.EscapeDataString(empresa?.Trim())}&cliente={Uri.EscapeDataString(clienteFiltro?.Trim())}"))
    End Function

    Public Function CargarListaEnviosTramitadosPorNombre(empresa As String, nombreFiltro As String) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosTramitadosPorNombre
        Return New ObservableCollection(Of EnviosAgencia)(LeerListadoEnvios(
            $"EnviosAgencias/Tramitados?empresa={Uri.EscapeDataString(empresa?.Trim())}&texto={Uri.EscapeDataString(nombreFiltro)}"))
    End Function

    ' Descarga y mapeo común de los listados: DTO del API → entidad POCO con la navegación
    ' mínima que usan los grids (AgenciasTransporte.Nombre). Síncrono a propósito: los setters
    ' del VM que consumen estos métodos son síncronos (mismo patrón que las reglas de Cajas).
    ' ===== Nesto#340 (Agencias, slice A3): el PEDIDO va por la API =====
    ' Sustituye a los 4 CargarPedido* que devolvian la entidad CabPedidoVta con Include de
    ' Clientes y de sus personas de contacto. El endpoint devuelve exactamente esos datos y SIN
    ' RECORTAR: Agencias compara Empresa, Nº_Cliente y Contacto sin Trim contra listas que aun
    ' vienen de EF con el padding de la BD.
    '
    ' 404 se traduce a Nothing, que es lo que devolvia EF cuando no encontraba el pedido: los
    ' caminos de "no encontrado" del ViewModel siguen funcionando igual.

    Public Function LeerPedidoParaAgencia(empresa As String, numeroPedido As Integer?) As PedidoAgenciaModel Implements IAgenciaService.LeerPedidoParaAgencia
        If numeroPedido Is Nothing Then
            Return Nothing
        End If
        Return LeerPedido($"PedidosVenta/ParaAgencia?empresa={Uri.EscapeDataString(If(empresa, String.Empty))}&numero={numeroPedido.Value}")
    End Function

    Public Function LeerPedidoParaAgenciaPorNumero(numeroPedido As Integer, incluirEspejo As Boolean) As PedidoAgenciaModel Implements IAgenciaService.LeerPedidoParaAgenciaPorNumero
        Return LeerPedido($"PedidosVenta/ParaAgencia?numero={numeroPedido}&incluirEspejo={incluirEspejo.ToString().ToLowerInvariant()}")
    End Function

    Public Function LeerPedidoParaAgenciaPorFactura(numeroFactura As String) As PedidoAgenciaModel Implements IAgenciaService.LeerPedidoParaAgenciaPorFactura
        If String.IsNullOrWhiteSpace(numeroFactura) Then
            Return Nothing
        End If
        Return LeerPedido($"PedidosVenta/ParaAgencia?factura={Uri.EscapeDataString(numeroFactura.Trim())}")
    End Function

    ' Sustituye a CargarClientePorUnDato + navegar cliente.CabPedidoVta. Esos dos pasos hacian
    ' lazy loading sobre un DbContext ya cerrado por su Using, asi que lanzaban
    ' ObjectDisposedException en cuanto la busqueda SI encontraba cliente. Ahora es una consulta
    ' server-side: busca el cliente por nombre/direccion/telefono y devuelve su pedido mas
    ' reciente, con los mismos criterios que tenia el original.
    Public Function LeerPedidoParaAgenciaPorTextoCliente(empresa As String, texto As String) As PedidoAgenciaModel Implements IAgenciaService.LeerPedidoParaAgenciaPorTextoCliente
        If String.IsNullOrWhiteSpace(texto) Then
            Return Nothing
        End If
        Return LeerPedido($"PedidosVenta/ParaAgencia?empresa={Uri.EscapeDataString(If(empresa, String.Empty))}&textoCliente={Uri.EscapeDataString(texto)}")
    End Function

    ''' <summary>
    ''' Nesto#340 (Agencias A3): nº de factura de la primera línea facturada del pedido, o Nothing.
    ''' Lee el pedido entero de GET api/PedidosVenta (el mismo DTO que DetallePedido) y mira
    ''' LineaPedidoVentaDTO.Factura; en pedidos parciales puede haber líneas sin facturar todavía.
    ''' </summary>
    Friend Shared Function NumeroFacturaDelPedido(pedido As PedidoVentaDTO) As String
        Return pedido?.Lineas?.
            Select(Function(l) l.Factura?.Trim()).
            FirstOrDefault(Function(f) Not String.IsNullOrWhiteSpace(f))
    End Function

    Friend Shared Function RutaPedidoVenta(empresa As String, numeroPedido As Integer) As String
        Return $"PedidosVenta?empresa={Uri.EscapeDataString(If(empresa, "").Trim())}&numero={numeroPedido}"
    End Function

    Private Async Function LeerNumeroFacturaDelPedido(empresa As String, numeroPedido As Integer) As Task(Of String)
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
            End If
            Dim response As HttpResponseMessage = Await client.GetAsync(RutaPedidoVenta(empresa, numeroPedido))
            If response.StatusCode = Net.HttpStatusCode.NotFound Then
                Return Nothing
            End If
            Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"No se pudo cargar el pedido {numeroPedido} ({CInt(response.StatusCode)}): {cuerpo}")
            End If
            Return NumeroFacturaDelPedido(JsonConvert.DeserializeObject(Of PedidoVentaDTO)(cuerpo))
        End Using
    End Function

    Private Function LeerPedido(ruta As String) As PedidoAgenciaModel
        Return Task.Run(Async Function() As Task(Of PedidoAgenciaModel)
                            Using client As HttpClient = _clienteApiFactory.Crear()
                                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                                End If
                                Dim response As HttpResponseMessage = Await client.GetAsync(ruta)
                                If response.StatusCode = Net.HttpStatusCode.NotFound Then
                                    Return Nothing
                                End If
                                Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                                If Not response.IsSuccessStatusCode Then
                                    Throw New Exception($"No se pudo cargar el pedido ({CInt(response.StatusCode)}): {cuerpo}")
                                End If
                                Return JsonConvert.DeserializeObject(Of PedidoAgenciaModel)(cuerpo)
                            End Using
                        End Function).GetAwaiter().GetResult()
    End Function

    ''' <summary>
    ''' Nesto#340 (slice A3): las dos preguntas que Agencias le hacia a las lineas del pedido
    ''' (HayAlgunaLineaConPicking y EsTodoElPedidoOnline) las contesta ahora el servidor. Eran
    ''' las dos ultimas consultas de este servicio que abrian un NestoEntities sobre LinPedidoVta.
    '''
    ''' No se cachea a proposito: el picking de un pedido cambia mientras el almacen trabaja, y
    ''' justo por eso se pregunta. Son dos llamadas por tramitacion, las mismas dos consultas que
    ''' hacia antes.
    '''
    ''' La empresa se manda TAL CUAL llega, con su relleno de char(3) si lo trae: la comparacion
    ''' la hace SQL Server, que lo ignora. Es la ventaja de que el filtro se quede en el servidor
    ''' y no aqui, donde &quot;1&quot; y &quot;1  &quot; no casan (Nesto#254).
    ''' </summary>
    Private Function LeerSituacionLineas(empresa As String, pedido As Integer) As SituacionLineasPedidoModel
        Dim ruta As String = $"PedidosVenta/ParaAgencia/SituacionLineas?empresa={Uri.EscapeDataString(If(empresa, String.Empty))}&numero={pedido}"
        Return Task.Run(Async Function() As Task(Of SituacionLineasPedidoModel)
                            Using client As HttpClient = _clienteApiFactory.Crear()
                                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                                End If
                                Dim response As HttpResponseMessage = Await client.GetAsync(ruta)
                                Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                                If Not response.IsSuccessStatusCode Then
                                    Throw New Exception($"No se pudo consultar la situación del pedido {pedido} ({CInt(response.StatusCode)}): {cuerpo}")
                                End If
                                Return JsonConvert.DeserializeObject(Of SituacionLineasPedidoModel)(cuerpo)
                            End Using
                        End Function).GetAwaiter().GetResult()
    End Function

    Private Function LeerListadoEnvios(ruta As String) As List(Of EnviosAgencia)
        Dim dtos As List(Of EnvioAgenciaListadoDTO) =
            Task.Run(Async Function() As Task(Of List(Of EnvioAgenciaListadoDTO))
                         Using client As HttpClient = _clienteApiFactory.Crear()
                             If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                                 Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                             End If
                             Dim response As HttpResponseMessage = Await client.GetAsync(ruta)
                             Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                             If Not response.IsSuccessStatusCode Then
                                 Throw New Exception($"No se pudieron cargar los envíos ({CInt(response.StatusCode)}): {cuerpo}")
                             End If
                             Return JsonConvert.DeserializeObject(Of List(Of EnvioAgenciaListadoDTO))(cuerpo)
                         End Using
                     End Function).GetAwaiter().GetResult()

        ' Nesto#448: la navegación AgenciasTransporte se estampa COMPLETA. La mínima de A1.b
        ' solo llevaba el Nombre, y los flujos de tramitación y contabilización consumen más
        ' campos (Identificador para el uidcliente de ASM/Correos Express, CuentaReembolsos...).
        Dim agencias As Dictionary(Of String, AgenciasTransporte) = CargarDiccionarioAgencias()
        Return dtos.Select(Function(dto) AEnvioAgencia(dto, agencias)).ToList()
    End Function

    ' ===== Nesto#340 (Agencias, slice A1): las agencias se leen de la API, no de EF =====
    ' Son datos maestros que apenas cambian (13 filas) y antes se releían de la base de datos en
    ' CADA listado y en CADA envío insertado. Contra la API eso serían N llamadas HTTP, así que se
    ' cachean unos minutos: acota la ventana de desfase si alguien da de alta una agencia desde la
    ' ventana de mantenimiento, sin pagar una llamada por envío.
    Private Shared ReadOnly _duracionCacheAgencias As TimeSpan = TimeSpan.FromMinutes(5)
    Private _agenciasCacheadas As List(Of AgenciasTransporte)
    Private _agenciasCacheadasHasta As Date

    Private Function CargarTodasLasAgencias() As List(Of AgenciasTransporte)
        If _agenciasCacheadas IsNot Nothing AndAlso Date.Now < _agenciasCacheadasHasta Then
            Return _agenciasCacheadas
        End If

        Dim agencias As List(Of AgenciaMantenimiento) =
            Task.Run(Function() _servicioAgencias.LeerAgencias()).GetAwaiter().GetResult()

        _agenciasCacheadas = agencias.Select(AddressOf AAgenciaTransporte).ToList()
        _agenciasCacheadasHasta = Date.Now.Add(_duracionCacheAgencias)
        Return _agenciasCacheadas
    End Function

    ''' <summary>
    ''' Compara como lo hacía SQL Server, que es de donde venían antes estas agencias: ignorando el
    ''' relleno de los char y sin distinguir mayúsculas. La API devuelve los campos ya recortados y
    ''' los llamantes siguen pasando valores con padding (Empresa es char(3)), así que un "=" pelado
    ''' dejaría de encontrar la agencia sin dar ningún error. Es el mismo tropiezo de Nesto#254.
    ''' </summary>
    Private Shared Function CampoIgual(valorAgencia As String, valorBuscado As String) As Boolean
        Return String.Equals(If(valorAgencia, "").Trim(), If(valorBuscado, "").Trim(),
                             StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>
    ''' Empresa es char(3) en la base de datos y la API la devuelve RECORTADA ("1"), pero el resto
    ''' de Nesto la compara con "=" pelado contra otros char(3) que siguen viniendo de Entity
    ''' Framework con su relleno ("1  "): CabPedidoVta.Empresa, Empresas.Número... Si la agencia
    ''' llegara recortada, esas comparaciones dejarían de casar.
    '''
    ''' Pasó el 28/08/2026, al día siguiente de que las agencias empezaran a leerse de la API: al
    ''' imprimir CUALQUIER etiqueta reventaba ConfigurarAgenciaPedido con "Sequence contains no
    ''' matching element". Y lo peor no era eso, sino los dos FirstOrDefault que fallaban CALLADOS
    ''' (entre ellos el ajuste del comparador del servidor: Innovatrans no se habría elegido nunca).
    '''
    ''' Por eso la entidad se devuelve con el mismo relleno que tenía cuando la leía EF: así todos
    ''' los consumidores se comportan igual que antes del cambio, incluidos los que no avisan.
    '''
    ''' Se aplica igual a EnviosAgencia.Empresa, que también es char(3) y también llega recortado
    ''' desde el slice A2: AgenciasViewModel hace listaEmpresas.Single(e.Número = envio.Empresa)
    ''' al seleccionar un envío, y ahí pasaba exactamente lo mismo.
    ''' </summary>
    Private Const LONGITUD_EMPRESA As Integer = 3

    Private Shared Function ComoCharDeLaBD(valor As String, longitud As Integer) As String
        Return If(valor Is Nothing, Nothing, valor.Trim().PadRight(longitud))
    End Function

    Private Shared Function AAgenciaTransporte(dto As AgenciaMantenimiento) As AgenciasTransporte
        ' Usuario y FechaModificacion no viajan en el DTO: son de auditoría y no los mira nadie
        ' de los que consumen estas agencias. Las navegaciones tampoco, a propósito.
        Return New AgenciasTransporte With {
            .Numero = dto.Numero,
            .Empresa = ComoCharDeLaBD(dto.Empresa, LONGITUD_EMPRESA),
            .Nombre = dto.Nombre,
            .Ruta = dto.Ruta,
            .Identificador = dto.Identificador,
            .PrefijoCodigoBarras = dto.PrefijoCodigoBarras,
            .CuentaReembolsos = dto.CuentaReembolsos,
            .EsSombra = dto.EsSombra
        }
    End Function

    Private Function CargarDiccionarioAgencias() As Dictionary(Of String, AgenciasTransporte)
        Return CargarTodasLasAgencias().
            ToDictionary(Function(a) ClaveAgencia(a.Empresa, a.Numero))
    End Function

    Private Shared Function ClaveAgencia(empresa As String, numero As Integer) As String
        Return $"{empresa?.Trim()}|{numero}"
    End Function

    Friend Shared Function AEnvioAgencia(dto As EnvioAgenciaListadoDTO, agencias As Dictionary(Of String, AgenciasTransporte)) As EnviosAgencia
        Dim agencia As AgenciasTransporte = Nothing
        If agencias IsNot Nothing Then
            Dim unused = agencias.TryGetValue(ClaveAgencia(dto.Empresa, dto.Agencia), agencia)
        End If
        Return New EnviosAgencia With {
            .Numero = dto.Numero,
            .Empresa = ComoCharDeLaBD(dto.Empresa, LONGITUD_EMPRESA),
            .Agencia = dto.Agencia,
            .Cliente = dto.Cliente,
            .Contacto = dto.Contacto,
            .Pedido = dto.Pedido,
            .Estado = dto.Estado,
            .Fecha = dto.Fecha,
            .Servicio = CByte(dto.Servicio),
            .Horario = CByte(dto.Horario),
            .Bultos = CByte(dto.Bultos),
            .Retorno = CByte(dto.Retorno),
            .Nombre = dto.Nombre,
            .Direccion = dto.Direccion,
            .CodPostal = dto.CodPostal,
            .Poblacion = dto.Poblacion,
            .Provincia = dto.Provincia,
            .Telefono = dto.Telefono,
            .Movil = dto.Movil,
            .Email = dto.Email,
            .Observaciones = dto.Observaciones,
            .Atencion = dto.Atencion,
            .Reembolso = dto.Reembolso,
            .FechaPagoReembolso = dto.FechaPagoReembolso,
            .ImporteGasto = dto.ImporteGasto,
            .CodigoBarras = dto.CodigoBarras,
            .Pais = dto.Pais,
            .FechaEntrega = dto.FechaEntrega,
            .ImporteAsegurado = dto.ImporteAsegurado,
            .Peso = dto.Peso,
            .Vendedor = dto.Vendedor,
            .FechaFactura = dto.FechaFactura,
            .Usuario = dto.Usuario,
            .FechaModificacion = dto.FechaModificacion,
            .FechaRetornoRecibido = dto.FechaRetornoRecibido,
            .NombrePlaza = dto.NombrePlaza,
            .Nemonico = dto.Nemonico,
            .TelefonoPlaza = dto.TelefonoPlaza,
            .EmailPlaza = dto.EmailPlaza,
            .RowVersion = dto.RowVersion,
            .DetalleEstado = dto.DetalleEstado,
            .EnReparto = dto.EnReparto,
            .AgenciasTransporte = If(agencia, New AgenciasTransporte With {
                .Empresa = ComoCharDeLaBD(dto.Empresa, LONGITUD_EMPRESA), .Numero = dto.Agencia, .Nombre = dto.NombreAgencia})
        }
    End Function

    Public Function CargarListaAgencias(empresa As String) As ObservableCollection(Of AgenciasTransporte) Implements IAgenciaService.CargarListaAgencias
        Return New ObservableCollection(Of AgenciasTransporte)(
            CargarTodasLasAgencias().Where(Function(c) CampoIgual(c.Empresa, empresa)))
    End Function

    ''' <summary>
    ''' Nesto#340 (Agencias, slice A3): los envíos del pedido (todos los estados, por número) los
    ''' sirve GET api/EnviosAgencias/PorPedido. El filtro se queda EN EL SERVIDOR, que compara
    ''' ignorando el relleno de los char (Nesto#254). La consulta de EF solo cargaba la navegación
    ''' AgenciasTransporte, que AEnvioAgencia estampa completa desde la caché de agencias.
    ''' </summary>
    Public Function CargarListaEnviosPedido(empresa As String, pedido As Integer) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosPedido
        Return New ObservableCollection(Of EnviosAgencia)(LeerListadoEnvios(RutaEnviosPorPedido(empresa, pedido)))
    End Function

    ''' <summary>Aparte para fijarla en un test, como RutaEnvioPendientePorPedido.</summary>
    Friend Shared Function RutaEnviosPorPedido(empresa As String, pedido As Integer) As String
        Return $"EnviosAgencias/PorPedido?empresa={Uri.EscapeDataString(If(empresa, "").Trim())}&pedido={pedido}"
    End Function

    Public Function CargarAgencia(agencia As Integer) As AgenciasTransporte Implements IAgenciaService.CargarAgencia
        ' Se mantiene SingleOrDefault (y no First): hoy el número de agencia es único entre
        ' empresas, y si algún día dejara de serlo conviene que salte en vez de elegir una a dedo.
        Return CargarTodasLasAgencias().SingleOrDefault(Function(a) a.Numero = agencia)
    End Function

    ''' <summary>
    ''' Nesto#340 (slice A3): el historial de cambios del envio lo sirve
    ''' GET api/EnviosAgencias/{id}/Historia. Era la unica consulta de Nesto sobre
    ''' EnviosHistoria, y la tabla ni siquiera estaba en el EDMX del servidor hasta el
    ''' 31/08/2026.
    '''
    ''' SOLO LECTURA. Las ESCRITURAS del historial siguen en AgenciasViewModel, dentro de las
    ''' transacciones de contabilizacion de reembolsos, y se migraran con ellas.
    '''
    ''' El servidor ordena por numero (identity), o sea por orden de los hechos. Esta consulta
    ''' no ordenaba y el orden lo decidia el plan de SQL Server.
    ''' </summary>
    Public Function CargarListaHistoriaEnvio(envio As Integer) As ObservableCollection(Of EnviosHistoria) Implements IAgenciaService.CargarListaHistoriaEnvio
        Dim filas As List(Of EnvioHistoriaModel) =
            Task.Run(Async Function() As Task(Of List(Of EnvioHistoriaModel))
                         Using client As HttpClient = _clienteApiFactory.Crear()
                             If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                                 Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                             End If
                             Dim response As HttpResponseMessage = Await client.GetAsync($"EnviosAgencias/{envio}/Historia")
                             Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                             If Not response.IsSuccessStatusCode Then
                                 Throw New Exception($"No se pudo cargar el historial del envío {envio} ({CInt(response.StatusCode)}): {cuerpo}")
                             End If
                             Return JsonConvert.DeserializeObject(Of List(Of EnvioHistoriaModel))(cuerpo)
                         End Using
                     End Function).GetAwaiter().GetResult()

        Return New ObservableCollection(Of EnviosHistoria)(filas.Select(Function(f) New EnviosHistoria With {
            .Numero = f.Numero,
            .NumeroEnvio = f.NumeroEnvio,
            .Campo = f.Campo,
            .ValorAnterior = f.ValorAnterior,
            .Observaciones = f.Observaciones,
            .Usuario = f.Usuario,
            .FechaModificacion = f.FechaModificacion
        }))
    End Function

    ' ===== Nesto#340 (Agencias 1D): el saldo de la cuenta de reembolsos sale de la API, no de EF =====
    ' GET api/Contabilidades/Saldo (Debe - Haber desde el 01/01/2019, que es lo que sumaba aquí el
    ' Aggregate sobre Contabilidad). Lo consume el getter sumaContabilidad de AgenciasViewModel, que ya
    ' se traga cualquier excepción y devuelve 0. Inyectable para los tests, como LectorEmpresas.
    Friend Property LectorSaldo As Func(Of String, String, Double?) = AddressOf LeerSaldoDeLaApi

    Public Function CalcularSumaContabilidad(empresa As String, cuentaReembolsos As String) As Double? Implements IAgenciaService.CalcularSumaContabilidad
        If String.IsNullOrWhiteSpace(empresa) OrElse String.IsNullOrWhiteSpace(cuentaReembolsos) Then
            Return Nothing
        End If
        Return LectorSaldo.Invoke(empresa.Trim(), cuentaReembolsos.Trim())
    End Function

    Private Function LeerSaldoDeLaApi(empresa As String, cuenta As String) As Double?
        Return Task.Run(Async Function() As Task(Of Double?)
                            Using client As HttpClient = _clienteApiFactory.Crear()
                                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                                End If
                                Dim ruta As String = $"Contabilidades/Saldo?empresa={Uri.EscapeDataString(empresa)}&cuenta={Uri.EscapeDataString(cuenta)}"
                                Dim response As HttpResponseMessage = Await client.GetAsync(ruta)
                                Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                                If Not response.IsSuccessStatusCode Then
                                    Throw New Exception($"No se pudo leer el saldo de la cuenta {cuenta} ({CInt(response.StatusCode)}): {cuerpo}")
                                End If
                                Return JsonConvert.DeserializeObject(Of Double?)(cuerpo)
                            End Using
                        End Function).GetAwaiter().GetResult()
    End Function

    ' ===== Nesto#340 (Agencias): las empresas se leen de la API, no de EF =====
    ' Mismo GET Empresas que ya usan RemesasService y ClienteComercialService. Aquí se deserializa
    ' sobre la entidad Empresas porque es el tipo de la navegación envio.Empresas y de los cinco
    ' llamantes (etiquetas de ASM y Correos Express, combo de la ventana), que no cambian.
    ' Son 2-3 filas que no cambian en toda la sesión, pero CargarEmpresa se llama por cada etiqueta
    ' que se imprime: contra la API sería una llamada HTTP por etiqueta, así que se cachean igual
    ' que las agencias.
    ' OJO: el servidor devuelve la entidad tal cual, CON el relleno de los char (Número "1  "),
    ' igual que hacía EF. CargarEmpresa compara con CampoIgual y los llamantes ya hacían Trim.
    Private Shared ReadOnly _duracionCacheEmpresas As TimeSpan = TimeSpan.FromMinutes(5)
    Private _empresasCacheadas As List(Of Empresas)
    Private _empresasCacheadasHasta As Date

    ''' <summary>Solo para tests (InternalsVisibleTo "ViewModels.Tests"): sustituye la lectura
    ''' HTTP de GET Empresas sin levantar la API.</summary>
    Friend Property LectorEmpresas As Func(Of List(Of Empresas)) = AddressOf LeerEmpresasDeLaApi

    Public Function CargarListaEmpresas() As ObservableCollection(Of Empresas) Implements IAgenciaService.CargarListaEmpresas
        If _empresasCacheadas Is Nothing OrElse Date.Now >= _empresasCacheadasHasta Then
            _empresasCacheadas = LectorEmpresas.Invoke()
            _empresasCacheadasHasta = Date.Now.Add(_duracionCacheEmpresas)
        End If
        Return New ObservableCollection(Of Empresas)(_empresasCacheadas)
    End Function

    Private Function LeerEmpresasDeLaApi() As List(Of Empresas)
        Return Task.Run(Async Function() As Task(Of List(Of Empresas))
                            Using client As HttpClient = _clienteApiFactory.Crear()
                                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                                End If
                                Dim response As HttpResponseMessage = Await client.GetAsync("Empresas")
                                Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                                If Not response.IsSuccessStatusCode Then
                                    Throw New Exception($"No se pudieron cargar las empresas ({CInt(response.StatusCode)}): {cuerpo}")
                                End If
                                Return JsonConvert.DeserializeObject(Of List(Of Empresas))(cuerpo)
                            End Using
                        End Function).GetAwaiter().GetResult()
    End Function

    ''' <summary>
    ''' Ver IAgenciaService.CargarEmpresa. Cuando CargarListaEmpresas pase a la API (Nesto#340), este
    ''' es el ÚNICO punto donde hay que mirar si la comparación aguanta, en vez de los cinco de antes.
    ''' </summary>
    Public Function CargarEmpresa(numeroEmpresa As String) As Empresas Implements IAgenciaService.CargarEmpresa
        Dim empresa As Empresas = CargarListaEmpresas().FirstOrDefault(Function(e) CampoIgual(e.Número, numeroEmpresa))
        If empresa Is Nothing Then
            ' Falla aquí y con el número delante, no tres pantallas más adelante. Los sitios que
            ' imprimen etiqueta usaban Single y reventaban con "Sequence contains no elements", que
            ' no dice de qué empresa habla; los otros dos dejaban la navegación a Nothing y el error
            ' salía luego como un NullReference en mitad de la etiqueta.
            Throw New Exception($"No se encuentra la empresa '{numeroEmpresa}' para armar el envío.")
        End If
        Return empresa
    End Function

    ''' <summary>
    ''' Nesto#340 (slice A3): lo contesta GET api/Clientes/ExistePrincipalActivo.
    '''
    ''' Antes se traia la ficha entera del cliente para mirar unicamente si era Nothing. El
    ''' endpoint lleva el MISMO filtro de estado que tenia esta consulta (cliente de alta), que
    ''' no es un detalle: el GET api/Clientes de toda la vida NO filtra por estado, y
    ''' reutilizarlo habria dejado contabilizar reembolsos contra clientes dados de baja.
    ''' </summary>
    Public Function ExisteClientePrincipalActivo(empresa As String, cliente As String) As Boolean Implements IAgenciaService.ExisteClientePrincipalActivo
        Dim ruta As String = $"Clientes/ExistePrincipalActivo?empresa={Uri.EscapeDataString(If(empresa, String.Empty))}&cliente={Uri.EscapeDataString(If(cliente, String.Empty))}"
        Return Task.Run(Async Function() As Task(Of Boolean)
                            Using client As HttpClient = _clienteApiFactory.Crear()
                                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                                End If
                                Dim response As HttpResponseMessage = Await client.GetAsync(ruta)
                                Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                                If Not response.IsSuccessStatusCode Then
                                    Throw New Exception($"No se pudo comprobar el cliente {cliente} ({CInt(response.StatusCode)}): {cuerpo}")
                                End If
                                Return JsonConvert.DeserializeObject(Of Boolean)(cuerpo)
                            End Using
                        End Function).GetAwaiter().GetResult()
    End Function



    ''' <summary>
    ''' Nesto#340 (slice A3): lo contesta el servidor. Ver LeerSituacionLineas.
    ''' </summary>
    Public Function HayAlgunaLineaConPicking(empresa As String, pedido As Integer) As Boolean Implements IAgenciaService.HayAlgunaLineaConPicking
        Return LeerSituacionLineas(empresa, pedido).TieneAlgunaLineaConPicking
    End Function

    Public Function CargarAgenciaPorNombreYCuentaReembolsos(empresa As String, cuentaReembolsos As String, nombreAgencia As String) As AgenciasTransporte Implements IAgenciaService.CargarAgenciaPorNombreYCuentaReembolsos
        Return CargarTodasLasAgencias().SingleOrDefault(
            Function(a) CampoIgual(a.Empresa, empresa) AndAlso
                        CampoIgual(a.CuentaReembolsos, cuentaReembolsos) AndAlso
                        CampoIgual(a.Nombre, nombreAgencia))
    End Function

    ''' <summary>
    ''' Nesto#340 (Agencias, slice A3): el envío pendiente del pedido lo sirve
    ''' GET api/EnviosAgencias/PendientePorPedido. El filtro (Estado &lt; 0, empresa y pedido) se
    ''' queda EN EL SERVIDOR, que es quien compara ignorando el relleno de los char; filtrarlo aquí
    ''' en memoria reabriría el fallo mudo de Nesto#254.
    '''
    ''' De este envío salen el destino real de la etiqueta cuando la tienda online ya la había
    ''' creado (Nesto#395) y, en InsertarRegistro, la fila que se va a modificar. Por eso se estampa
    ''' también la empresa: envio.Empresas alimenta el remitente de la etiqueta de Correos Express y
    ''' el asiento del reembolso (hoy en el servidor, ConfirmarTramitacion), y Entity Framework la
    ''' traía con un Reference(...).Load().
    ''' Mismo criterio (y mismo código) que Insertar.
    ''' </summary>
    Public Function CargarEnvio(empresa As String, pedido As Integer) As EnviosAgencia Implements IAgenciaService.CargarEnvio
        Dim envio As EnviosAgencia = LeerListadoEnvios(RutaEnvioPendientePorPedido(empresa, pedido)).FirstOrDefault()
        If envio Is Nothing Then
            Return Nothing
        End If
        envio.Empresas = CargarEmpresa(envio.Empresa)
        Return envio
    End Function

    ''' <summary>La ruta del envío pendiente, aparte para poder fijarla en un test: si los nombres de
    ''' los parámetros dejaran de casar con los del endpoint, Web API no ataría el valor y la llamada
    ''' devolvería otra cosa (o un 400) sin que nada aquí lo notara.</summary>
    Friend Shared Function RutaEnvioPendientePorPedido(empresa As String, pedido As Integer) As String
        Return $"EnviosAgencias/PendientePorPedido?empresa={Uri.EscapeDataString(If(empresa, "").Trim())}&pedido={pedido}"
    End Function


    Public Function CargarAgenciaPorRuta(empresa As String, ruta As String) As AgenciasTransporte Implements IAgenciaService.CargarAgenciaPorRuta
        Dim agencias As List(Of AgenciasTransporte) = CargarTodasLasAgencias()
        Return If(empresa.Trim = Constantes.Empresas.EMPRESA_DEFECTO,
            agencias.FirstOrDefault(Function(a) CampoIgual(a.Empresa, empresa) AndAlso CampoIgual(a.Ruta, ruta)),
            agencias.FirstOrDefault(Function(a) CampoIgual(a.Empresa, empresa) AndAlso CampoIgual(a.Nombre, Constantes.Agencias.AGENCIA_REEMBOLSOS)))
    End Function

    ''' <summary>
    ''' Nesto#340 (Agencias, slice A3): la "ampliación" (el envío EN CURSO de hoy del mismo cliente,
    ''' contacto y dirección, para meter el pedido nuevo en el mismo bulto) dejó Entity Framework y
    ''' la contesta GET api/EnviosAgencias/EnCursoPorClienteYDireccion, que replica el filtro exacto
    ''' (Estado = 0, sin empresa). Como CargarEnvio, estampa la empresa: el EF la traía con un
    ''' Reference(...).Load() y de ahí salen el remitente de Correos Express y el asiento del reembolso.
    ''' </summary>
    Public Function CargarEnvioPorClienteYDireccion(cliente As String, contacto As String, direccion As String) As EnviosAgencia Implements IAgenciaService.CargarEnvioPorClienteYDireccion
        Dim envio As EnviosAgencia = LeerListadoEnvios(RutaEnvioEnCursoPorClienteYDireccion(cliente, contacto, direccion)).FirstOrDefault()
        If envio Is Nothing Then
            Return Nothing
        End If
        envio.Empresas = CargarEmpresa(envio.Empresa)
        Return envio
    End Function

    ''' <summary>Ruta aparte para fijar en un test el contrato de nombres con el endpoint. Cliente y
    ''' contacto son char en la BD (llegan con relleno); la dirección se manda tal cual, que SQL
    ''' Server ya ignora el relleno al comparar y en memoria no hay que comparar nada.</summary>
    Friend Shared Function RutaEnvioEnCursoPorClienteYDireccion(cliente As String, contacto As String, direccion As String) As String
        Return $"EnviosAgencias/EnCursoPorClienteYDireccion?cliente={Uri.EscapeDataString(If(cliente, "").Trim())}&contacto={Uri.EscapeDataString(If(contacto, "").Trim())}&direccion={Uri.EscapeDataString(If(direccion, ""))}"
    End Function

    ' ===== Nesto#340 (Agencias, slice A4.1): cerrar el envío y contabilizar su reembolso =====
    ' El servidor hace las dos cosas en una transacción (POST .../ConfirmarTramitacion) y estampa el
    ' usuario del asiento desde el JWT.
    '
    ' Se rodó detrás del parámetro de usuario TramitarEnvioPorApi (protocolo de pies de plomo del
    ' 20/08/26). Estuvo en "API" para (defecto) desde el 25/08 y el piloto se cerró el 31/08 con los
    ' tres envíos con reembolso verificados en la base de datos, así que el camino de Entity
    ' Framework llevaba diez días sin ejecutarse: se retira junto con la bandera.

    Public Function TramitarEnvio(envio As EnviosAgencia) As String Implements IAgenciaService.TramitarEnvio
        Return TramitarEnvioPorApi(envio)
    End Function

    ''' <summary>
    ''' Cierra el envío en el servidor. Devuelve el mismo tipo de mensaje que el camino antiguo
    ''' porque el ViewModel decide por su contenido (busca la palabra "Error").
    ''' </summary>
    Private Function TramitarEnvioPorApi(envio As EnviosAgencia) As String
        Try
            Return Task.Run(Async Function() As Task(Of String)
                                Using client As HttpClient = _clienteApiFactory.Crear()
                                    If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                                        Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
                                    End If
                                    Dim response As HttpResponseMessage = Await client.PostAsync(
                                        $"EnviosAgencias/{envio.Numero}/ConfirmarTramitacion", New StringContent(String.Empty, Encoding.UTF8, "application/json"))
                                    Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                                    If Not response.IsSuccessStatusCode Then
                                        Throw New Exception(cuerpo)
                                    End If
                                    Dim resultado = JsonConvert.DeserializeObject(Of ResultadoTramitacionEnvioModel)(cuerpo)
                                    ' El servidor ya ha cambiado estado y fechas: se reflejan en la
                                    ' entidad que el ViewModel tiene en la mano para no recargar.
                                    envio.Estado = Constantes.Agencias.ESTADO_TRAMITADO_ENVIO
                                    envio.Fecha = Today
                                    envio.FechaEntrega = Today.AddDays(1)
                                    Return resultado.Mensaje
                                End Using
                            End Function).GetAwaiter().GetResult()
        Catch ex As Exception
            ' Nesto#448: aqui la excepcion se convierte en TEXTO de retorno, asi que el Catch del
            ' ViewModel no llega a verla y su registro en ELMAH nunca se ejecuta. Si no se registra
            ' aqui, el fallo se queda SOLO en el dialogo que ve el usuario.
            ' Paso el 28/08/2026: el error del Limit1 solo constaba por el lado del servidor.
            Dim unused = RegistrarErrorEnElmah(ex, "AgenciaService.TramitarEnvioPorApi")
            Return $"Error al tramitar pedido {envio.Pedido}: {ex.Message}"
        End Try
    End Function

    ' Best-effort: registrar el error nunca puede romper el flujo ni tapar el error de verdad.
    Private Shared Async Function RegistrarErrorEnElmah(ex As Exception, contexto As String) As Task
        Try
            Dim servicioErrores = ContainerLocator.Container?.Resolve(Of IServicioRegistroErrores)()
            If servicioErrores IsNot Nothing Then
                Await servicioErrores.RegistrarErrorAsync(ex, contexto)
            End If
        Catch
            ' Si falla el registro, se ignora.
        End Try
    End Function


    Public Async Function EnviarCorreoEntregaAgencia(envioActual As EnvioAgenciaWrapper) As Task Implements IAgenciaService.EnviarCorreoEntregaAgencia
        Using client As HttpClient = _clienteApiFactory.Crear()
            Try

                ' Carlos 21/11/24: Agregar autenticación
                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                Dim response As HttpResponseMessage
                Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(envioActual), Encoding.UTF8, "application/json")
                response = Await client.PostAsync("EnviosAgencias/EnviarCorreoEntregaAgencia", content)
            Catch ex As Exception
                Throw ex
            End Try
        End Using
    End Function

    ''' <summary>
    ''' Nesto#340 (slice A3): lo contesta el servidor. Ver LeerSituacionLineas.
    ''' </summary>
    Public Function EsTodoElPedidoOnline(empresa As String, pedido As Integer) As Boolean Implements IAgenciaService.EsTodoElPedidoOnline
        Return LeerSituacionLineas(empresa, pedido).EsTodoOnline
    End Function

    Public Async Function GuardarLlamadaAgencia(respuesta As RespuestaAgencia) As Task Implements IAgenciaService.GuardarLlamadaAgencia
        respuesta.Usuario = configuracion.usuario
        Using client As HttpClient = _clienteApiFactory.Crear()
            Try

                ' Carlos 21/11/24: Agregar autenticación
                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                Dim response As HttpResponseMessage
                Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(respuesta), Encoding.UTF8, "application/json")
                response = Await client.PostAsync("AgenciasLlamadasWeb", content)
            Catch ex As Exception
                Throw ex
            End Try
        End Using
    End Function


    Public Async Function TramitarEnvioRemoto(numeroEnvio As Integer) As Task(Of TramitarEnvioResultadoDto) Implements IAgenciaService.TramitarEnvioRemoto
        Using client As HttpClient = _clienteApiFactory.Crear()

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
            End If

            ' El cuerpo va vacío: el envío ya existe (lo identifica la ruta); el servidor lo tramita.
            Dim content As HttpContent = New StringContent(String.Empty, Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PostAsync($"EnviosAgencias/{numeroEnvio}/Tramitar", content)
            Dim cuerpo As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"NestoAPI rechazó la tramitación ({CInt(response.StatusCode)}): {cuerpo}")
            End If

            Return JsonConvert.DeserializeObject(Of TramitarEnvioResultadoDto)(cuerpo)
        End Using
    End Function

    Public Async Function AnularEnvioRemoto(numeroEnvio As Integer) As Task Implements IAgenciaService.AnularEnvioRemoto
        Using client As HttpClient = _clienteApiFactory.Crear()

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
            End If

            ' Cuerpo vacío: el envío lo identifica la ruta. API primero, BD después: si la agencia
            ' rechaza, el servidor no toca nada y aquí lanzamos con SU motivo tal cual.
            Dim content As HttpContent = New StringContent(String.Empty, Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PostAsync($"EnviosAgencias/{numeroEnvio}/Anular", content)
            Dim cuerpo As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"NestoAPI rechazó la anulación ({CInt(response.StatusCode)}): {cuerpo}")
            End If
        End Using
    End Function

    ''' <summary>
    ''' Nesto#340 (Agencias, slice A4.2): la recepción del retorno la estampa el servidor
    ''' (POST EnviosAgencias/{n}/RecibirRetorno); antes era un UPDATE por Entity Framework en
    ''' AgenciasViewModel.OnRecibirRetorno. Devuelve la fecha que ha quedado grabada. Si el servidor
    ''' rechaza (retorno ya recibido por otra sesión, envío inexistente) lanza con SU motivo tal cual.
    ''' </summary>
    Public Async Function RecibirRetorno(numeroEnvio As Integer) As Task(Of Date) Implements IAgenciaService.RecibirRetorno
        Using client As HttpClient = _clienteApiFactory.Crear()

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
            End If

            Dim content As HttpContent = New StringContent(String.Empty, Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PostAsync(RutaRecibirRetorno(numeroEnvio), content)
            Dim cuerpo As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"NestoAPI rechazó la recepción del retorno ({CInt(response.StatusCode)}): {cuerpo}")
            End If

            Return JsonConvert.DeserializeObject(Of RetornoRecibidoDto)(cuerpo).FechaRetornoRecibido
        End Using
    End Function

    ''' <summary>Ruta aparte para fijar en un test el contrato con el endpoint.</summary>
    Friend Shared Function RutaRecibirRetorno(numeroEnvio As Integer) As String
        Return $"EnviosAgencias/{numeroEnvio}/RecibirRetorno"
    End Function

    ''' <summary>
    ''' Nesto#415 / Nesto#340 (Agencias, slice A4.3): el pago de reembolsos lo contabiliza el
    ''' servidor (POST EnviosAgencias/PagarReembolsos) con el usuario del JWT; antes lo hacía
    ''' AgenciasViewModel.OnContabilizarReembolso por Entity Framework. Si el servidor rechaza
    ''' (400 con el motivo: envío ya pagado, cliente inexistente...) lanza con SU texto tal cual.
    ''' </summary>
    Public Async Function PagarReembolsos(datos As PagoReembolsosDto) As Task(Of ResultadoPagoReembolsosDto) Implements IAgenciaService.PagarReembolsos
        Using client As HttpClient = _clienteApiFactory.Crear()

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
            End If

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(datos), Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PostAsync(RUTA_PAGAR_REEMBOLSOS, content)
            Dim cuerpo As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"NestoAPI rechazó el pago de reembolsos ({CInt(response.StatusCode)}): {cuerpo}")
            End If

            Return JsonConvert.DeserializeObject(Of ResultadoPagoReembolsosDto)(cuerpo)
        End Using
    End Function

    Friend Const RUTA_PAGAR_REEMBOLSOS As String = "EnviosAgencias/PagarReembolsos"

    ''' <summary>
    ''' Nesto#340 (Agencias, slice A4.4): la modificación de un envío tramitado la hace el servidor
    ''' (POST EnviosAgencias/{n}/ModificarDatos); antes eran modificarEnvio +
    ''' contabilizarModificacionReembolso del ViewModel por Entity Framework.
    ''' </summary>
    Public Async Function ModificarDatosEnvio(numeroEnvio As Integer, datos As ModificarDatosEnvioDto) As Task(Of ResultadoModificacionEnvioDto) Implements IAgenciaService.ModificarDatosEnvio
        Using client As HttpClient = _clienteApiFactory.Crear()

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
            End If

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(datos), Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PostAsync(RutaModificarDatosEnvio(numeroEnvio), content)
            Dim cuerpo As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"NestoAPI rechazó la modificación del envío ({CInt(response.StatusCode)}): {cuerpo}")
            End If

            Return JsonConvert.DeserializeObject(Of ResultadoModificacionEnvioDto)(cuerpo)
        End Using
    End Function

    Friend Shared Function RutaModificarDatosEnvio(numeroEnvio As Integer) As String
        Return $"EnviosAgencias/{numeroEnvio}/ModificarDatos"
    End Function

    Public Async Function ModificarEnvioRemoto(numeroEnvio As Integer, datos As ModificarEnvioAgenciaDto) As Task(Of TramitarEnvioResultadoDto) Implements IAgenciaService.ModificarEnvioRemoto
        Using client As HttpClient = _clienteApiFactory.Crear()

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
            End If

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(datos), Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PostAsync($"EnviosAgencias/{numeroEnvio}/Modificar", content)
            Dim cuerpo As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"NestoAPI rechazó la modificación ({CInt(response.StatusCode)}): {cuerpo}")
            End If

            Return JsonConvert.DeserializeObject(Of TramitarEnvioResultadoDto)(cuerpo)
        End Using
    End Function

    Public Async Function ActualizarSeguimientoEnvio(numeroEnvio As Integer) As Task(Of SeguimientoActualizadoDto) Implements IAgenciaService.ActualizarSeguimientoEnvio
        Using client As HttpClient = _clienteApiFactory.Crear()

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización contra NestoAPI.")
            End If

            ' Cuerpo vacío: el envío lo identifica la ruta; el servidor consulta su seguimiento y persiste.
            Dim content As HttpContent = New StringContent(String.Empty, Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PostAsync($"EnviosAgencias/{numeroEnvio}/ActualizarSeguimiento", content)
            Dim cuerpo As String = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"No se pudo actualizar el seguimiento ({CInt(response.StatusCode)}): {cuerpo}")
            End If

            Return JsonConvert.DeserializeObject(Of SeguimientoActualizadoDto)(cuerpo)
        End Using
    End Function

    Public Async Function ImporteReembolso(empresa As String, pedido As Integer) As Task(Of Decimal) Implements IAgenciaService.ImporteReembolso
        Using client As HttpClient = _clienteApiFactory.Crear()

            ' Carlos 21/11/24: Agregar autenticación
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = $"PedidosVenta/ImporteReembolso?empresa={empresa.Trim}&pedido={pedido}"

                response = Await client.GetAsync(urlConsulta)

                respuesta = If(response.IsSuccessStatusCode, Await response.Content.ReadAsStringAsync(), "")

            Catch ex As Exception
                Throw New Exception("No se ha podido calcular el reembolso del pedido", ex)
            Finally

            End Try

            Dim importe As Decimal = JsonConvert.DeserializeObject(Of Decimal)(respuesta)

            Return importe

        End Using
    End Function

    Public Async Function EnviarCorreoConFacturaDelPedido(empresa As String, numeroPedido As Integer, destinatario As String, asunto As String, cuerpo As String) As Task(Of (Exito As Boolean, Mensaje As String)) Implements IAgenciaService.EnviarCorreoConFacturaDelPedido
        ' Nesto#359: Canteras necesita la factura adjunta para el DUA. Buscamos la primera
        ' línea del pedido con Nº_Factura informado (en pedidos parciales puede haber líneas
        ' sin facturar todavía); si no hay ninguna, abortamos sin enviar.
        ' Nesto#340 (Agencias A3, 16/09/26): la factura sale del pedido de la API (GET api/PedidosVenta),
        ' primera línea con Factura informada, en vez de una consulta EF sobre LinPedidoVta.
        Dim numeroFactura As String = Await LeerNumeroFacturaDelPedido(empresa, numeroPedido)

        If String.IsNullOrWhiteSpace(numeroFactura) Then
            Return (False, $"El pedido {numeroPedido} no tiene factura asociada todavía. Factura primero el pedido y vuelve a tramitar el envío.")
        End If

        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Return (False, "No se pudo configurar la autorización contra NestoAPI.")
            End If

            Dim urlFactura As String = $"Facturas?empresa={empresa.Trim()}&numeroFactura={numeroFactura.Trim()}"
            Dim respuestaPdf As HttpResponseMessage = Await client.GetAsync(urlFactura)
            If Not respuestaPdf.IsSuccessStatusCode Then
                Return (False, $"No se pudo descargar la factura {numeroFactura.Trim()}: {CInt(respuestaPdf.StatusCode)} {respuestaPdf.ReasonPhrase}")
            End If
            Dim bytesPdf As Byte() = Await respuestaPdf.Content.ReadAsByteArrayAsync()

            ' Nesto#367: los correos a agencias salen de Logística, con copia oculta a la propia
            ' Logística para que el equipo tenga constancia en su buzón de qué se ha enviado.
            Dim payload = New With {
                Key .Remitente = "logistica@nuevavision.es",
                Key .NombreRemitente = "Logística Nueva Visión",
                Key .Destinatarios = New String() {destinatario},
                Key .CopiaOculta = New String() {"logistica@nuevavision.es"},
                Key .Asunto = asunto,
                Key .Cuerpo = cuerpo,
                Key .EsHtml = False,
                Key .Adjuntos = {
                    New With {
                        Key .Nombre = $"Factura_{numeroFactura.Trim()}.pdf",
                        Key .ContenidoBase64 = Convert.ToBase64String(bytesPdf),
                        Key .TipoMime = "application/pdf"
                    }
                }
            }

            Dim json As String = JsonConvert.SerializeObject(payload)
            Dim content As HttpContent = New StringContent(json, Encoding.UTF8, "application/json")
            Dim respuesta As HttpResponseMessage = Await client.PostAsync("Correos/Enviar", content)
            If respuesta.IsSuccessStatusCode Then
                Return (True, $"Correo enviado correctamente a {destinatario}.")
            End If

            Dim cuerpoError As String = Await respuesta.Content.ReadAsStringAsync()
            Return (False, $"NestoAPI rechazó el correo: {CInt(respuesta.StatusCode)} {respuesta.ReasonPhrase}. {cuerpoError}")
        End Using
    End Function

End Class

' Nesto#340 (Agencias, slice A2): excluye las propiedades de navegación al serializar entidades
' del EDMX hacia la API. Las navegaciones son las únicas propiedades Overridable de las
' entidades generadas, así que el criterio es genérico y aguanta columnas nuevas sin tocar nada.
Friend Class ContractResolverSinNavegaciones
    Inherits Newtonsoft.Json.Serialization.DefaultContractResolver

    Protected Overrides Function CreateProperty(member As Reflection.MemberInfo, memberSerialization As MemberSerialization) As Newtonsoft.Json.Serialization.JsonProperty
        Dim propiedad = MyBase.CreateProperty(member, memberSerialization)
        Dim info = TryCast(member, Reflection.PropertyInfo)
        Dim getter = info?.GetGetMethod()
        If getter IsNot Nothing AndAlso getter.IsVirtual AndAlso Not getter.IsFinal Then
            propiedad.ShouldSerialize = Function(o) False
        End If
        Return propiedad
    End Function
End Class

' Nesto#340 (Agencias, slice A1.b): contrato de los GET api/EnviosAgencias/* de listados
' (EnvioAgenciaListadoDTO del API). Se mapea de inmediato a la entidad EnviosAgencia, así que
' no sale de este fichero.
Friend Class EnvioAgenciaListadoDTO
    Public Property Numero As Integer
    Public Property Empresa As String
    Public Property Agencia As Integer
    Public Property NombreAgencia As String
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Pedido As Integer?
    Public Property Estado As Short
    Public Property Fecha As Date
    Public Property Servicio As Short
    Public Property Horario As Short
    Public Property Bultos As Short
    Public Property Retorno As Short
    Public Property Nombre As String
    Public Property Direccion As String
    Public Property CodPostal As String
    Public Property Poblacion As String
    Public Property Provincia As String
    Public Property Telefono As String
    Public Property Movil As String
    Public Property Email As String
    Public Property Observaciones As String
    Public Property Atencion As String
    Public Property Reembolso As Decimal
    Public Property FechaPagoReembolso As Date?
    Public Property ImporteGasto As Decimal
    Public Property CodigoBarras As String
    Public Property Pais As Integer
    Public Property FechaEntrega As Date?
    Public Property ImporteAsegurado As Decimal
    Public Property Peso As Decimal
    ' Nesto#448: columnas que faltaban — sin ellas, Modificar machacaba estos campos a NULL.
    Public Property Vendedor As String
    Public Property FechaFactura As Date?
    Public Property Usuario As String
    Public Property FechaModificacion As Date
    Public Property FechaRetornoRecibido As Date?
    Public Property NombrePlaza As String
    Public Property Nemonico As String
    Public Property TelefonoPlaza As String
    Public Property EmailPlaza As String
    Public Property RowVersion As Byte()
    ' NestoAPI#259: motivo del estado tal y como lo da la agencia. Lo escribe el poll de
    ' seguimiento del servidor; aqui es de solo lectura y alimenta la columna "Incidencia".
    Public Property DetalleEstado As String
    ' NestoAPI#516: tramitado que la agencia ya ha sacado a reparto (derivado de DetalleEstado en la API).
    ' Con una API anterior no viene y se queda en False.
    Public Property EnReparto As Boolean
End Class
