Imports System.Collections.ObjectModel
Imports System.Data.Entity
Imports System.Net.Http
Imports System.Text
Imports System.Transactions
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Newtonsoft.Json
Imports Prism.Services.Dialogs

Public Class AgenciaService
    Implements IAgenciaService

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _dialogService As IDialogService
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion
    Private ReadOnly _clienteApiFactory As IClienteApiFactory

    Public Sub New(configuracion As IConfiguracion, dialogService As IDialogService, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _dialogService = dialogService
        _servicioAutenticacion = servicioAutenticacion
        _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
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
        envio.Empresas = CargarListaEmpresas().FirstOrDefault(Function(e) e.Número?.Trim() = envio.Empresa?.Trim())
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

    Private Function CargarDiccionarioAgencias() As Dictionary(Of String, AgenciasTransporte)
        Using contexto = New NestoEntities
            Return contexto.AgenciasTransporte.ToList().
                ToDictionary(Function(a) ClaveAgencia(a.Empresa, a.Numero))
        End Using
    End Function

    Private Shared Function ClaveAgencia(empresa As String, numero As Integer) As String
        Return $"{empresa?.Trim()}|{numero}"
    End Function

    Private Shared Function AEnvioAgencia(dto As EnvioAgenciaListadoDTO, agencias As Dictionary(Of String, AgenciasTransporte)) As EnviosAgencia
        Dim agencia As AgenciasTransporte = Nothing
        If agencias IsNot Nothing Then
            Dim unused = agencias.TryGetValue(ClaveAgencia(dto.Empresa, dto.Agencia), agencia)
        End If
        Return New EnviosAgencia With {
            .Numero = dto.Numero,
            .Empresa = dto.Empresa,
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
            .AgenciasTransporte = If(agencia, New AgenciasTransporte With {
                .Empresa = dto.Empresa, .Numero = dto.Agencia, .Nombre = dto.NombreAgencia})
        }
    End Function

    Public Function CargarListaAgencias(empresa As String) As ObservableCollection(Of AgenciasTransporte) Implements IAgenciaService.CargarListaAgencias
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of AgenciasTransporte)(From c In contexto.AgenciasTransporte Where c.Empresa = empresa)
        End Using
    End Function

    Public Function CargarListaEnviosPedido(empresa As String, pedido As Integer) As ObservableCollection(Of EnviosAgencia) Implements IAgenciaService.CargarListaEnviosPedido
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of EnviosAgencia)(From e In contexto.EnviosAgencia.Include("AgenciasTransporte") Where e.Empresa = empresa AndAlso e.Pedido = pedido Order By e.Numero)
        End Using
    End Function

    Public Function CargarAgencia(agencia As Integer) As AgenciasTransporte Implements IAgenciaService.CargarAgencia
        Using contexto = New NestoEntities
            Return contexto.AgenciasTransporte.Where(Function(a) a.Numero = agencia).SingleOrDefault
        End Using
    End Function

    Public Function CargarListaHistoriaEnvio(envio As Integer) As ObservableCollection(Of EnviosHistoria) Implements IAgenciaService.CargarListaHistoriaEnvio
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of EnviosHistoria)(From h In contexto.EnviosHistoria Where h.NumeroEnvio = envio)
        End Using
    End Function

    Public Function CargarMultiusuario(empresa As String, multiusuario As Integer) As MultiUsuarios Implements IAgenciaService.CargarMultiusuario
        Using contexto = New NestoEntities
            Return (From m In contexto.MultiUsuarios Where m.Empresa = empresa And m.Número = multiusuario).FirstOrDefault
        End Using
    End Function

    Public Function CalcularSumaContabilidad(empresa As String, cuentaReembolsos As String) As Double? Implements IAgenciaService.CalcularSumaContabilidad
        Using contexto = New NestoEntities
            Dim fechaInicial As New Date(2019, 1, 1)
            Return Aggregate c In contexto.Contabilidad Where c.Empresa = empresa AndAlso c.Fecha >= fechaInicial AndAlso c.Nº_Cuenta = cuentaReembolsos Into Sum(c.Debe - CType(c.Haber, Double?))
        End Using
    End Function

    Public Function CargarListaEmpresas() As ObservableCollection(Of Empresas) Implements IAgenciaService.CargarListaEmpresas
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of Empresas)(From c In contexto.Empresas)
        End Using
    End Function

    Public Function CargarClientePrincipal(empresa As String, cliente As String) As Clientes Implements IAgenciaService.CargarClientePrincipal
        Using contexto = New NestoEntities
            Return (From c In contexto.Clientes Where c.Empresa = empresa And c.Nº_Cliente = cliente And c.ClientePrincipal = True And c.Estado >= Constantes.Clientes.ESTADO_NORMAL).FirstOrDefault
        End Using
    End Function

    Public Function CargarLineasPedidoPendientes(pedido As Integer) As List(Of LinPedidoVta) Implements IAgenciaService.CargarLineasPedidoPendientes
        Using contexto = New NestoEntities
            Return (From l In contexto.LinPedidoVta Where l.Número = pedido And l.Estado = Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE).ToList
        End Using
    End Function

    Public Function CargarLineasPedidoSinPicking(pedido As Integer) As List(Of LinPedidoVta) Implements IAgenciaService.CargarLineasPedidoSinPicking
        Using contexto = New NestoEntities
            Return (From l In contexto.LinPedidoVta Where l.Número = pedido And l.Picking <> 0 And l.Estado = Constantes.LineasPedido.ESTADO_SIN_FACTURAR).ToList
        End Using
    End Function

    Public Function HayAlgunaLineaConPicking(empresa As String, pedido As Integer) As Boolean Implements IAgenciaService.HayAlgunaLineaConPicking
        Using contexto = New NestoEntities
            Dim lineaConPicking = contexto.LinPedidoVta.FirstOrDefault(Function(l) l.Empresa = empresa AndAlso l.Número = pedido AndAlso l.Estado <= Constantes.LineasPedido.ESTADO_SIN_FACTURAR AndAlso l.Estado >= Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE AndAlso l.Picking <> 0)
            Return Not IsNothing(lineaConPicking)
        End Using
    End Function

    Public Function CargarAgenciaPorNombreYCuentaReembolsos(empresa As String, cuentaReembolsos As String, nombreAgencia As String) As AgenciasTransporte Implements IAgenciaService.CargarAgenciaPorNombreYCuentaReembolsos
        Using contexto = New NestoEntities
            Return contexto.AgenciasTransporte.SingleOrDefault(Function(a) a.Empresa = empresa AndAlso a.CuentaReembolsos = cuentaReembolsos AndAlso a.Nombre = nombreAgencia)
        End Using
    End Function

    Public Function CargarEnvio(empresa As String, pedido As Integer) As EnviosAgencia Implements IAgenciaService.CargarEnvio
        Using contexto = New NestoEntities
            Dim respuesta As EnviosAgencia = contexto.EnviosAgencia.Include("AgenciasTransporte").FirstOrDefault(Function(e) e.Estado < Constantes.Agencias.ESTADO_INICIAL_ENVIO AndAlso e.Empresa = empresa AndAlso e.Pedido = pedido)
            If Not IsNothing(respuesta) Then
                contexto.Entry(respuesta).Reference(Function(e) e.Empresas).Load()
            End If
            Return respuesta
        End Using
    End Function

    Public Function CargarExtractoCliente(empresa As String, cliente As String, positivos As Boolean) As ObservableCollection(Of ExtractoCliente) Implements IAgenciaService.CargarExtractoCliente
        Using contexto = New NestoEntities
            Return If(positivos,
                New ObservableCollection(Of ExtractoCliente)(From e In contexto.ExtractoCliente Where e.Empresa = empresa AndAlso e.Número = cliente AndAlso e.ImportePdte > 0 AndAlso (e.Estado = "NRM" OrElse e.Estado Is Nothing) AndAlso Not e.Nº_Documento.StartsWith(Constantes.Series.SERIE_CURSOS)),
                New ObservableCollection(Of ExtractoCliente)(From e In contexto.ExtractoCliente Where e.Empresa = empresa AndAlso e.Número = cliente AndAlso e.ImportePdte < 0 AndAlso (e.Estado = "NRM" OrElse e.Estado Is Nothing) AndAlso Not e.Nº_Documento.StartsWith(Constantes.Series.SERIE_CURSOS)))
        End Using
    End Function

    Public Function CargarPagoExtractoClientePorEnvio(envio As EnviosAgencia, concepto As String, importeAnterior As Double) As ObservableCollection(Of ExtractoCliente) Implements IAgenciaService.CargarPagoExtractoClientePorEnvio
        Using contexto = New NestoEntities
            Return New ObservableCollection(Of ExtractoCliente)(From e In contexto.ExtractoCliente Where e.Empresa = envio.Empresa And
                                                                    e.Número = envio.Cliente And e.Contacto = envio.Contacto And e.Fecha = envio.Fecha And e.TipoApunte = 3 And e.Concepto = concepto And
                                                                    e.Importe = -importeAnterior)
        End Using
    End Function

    Public Function CargarAgenciaPorRuta(empresa As String, ruta As String) As AgenciasTransporte Implements IAgenciaService.CargarAgenciaPorRuta
        Using contexto = New NestoEntities
            Return If(empresa.Trim = Constantes.Empresas.EMPRESA_DEFECTO,
                contexto.AgenciasTransporte.FirstOrDefault(Function(a) a.Empresa = empresa AndAlso a.Ruta = ruta),
                contexto.AgenciasTransporte.FirstOrDefault(Function(a) a.Empresa = empresa AndAlso a.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS))
        End Using
    End Function

    Public Function CargarCliente(empresa As String, cliente As String, contacto As String) As Clientes Implements IAgenciaService.CargarCliente
        Using contexto = New NestoEntities
            Return contexto.Clientes.Single(Function(c) c.Empresa = empresa AndAlso c.Nº_Cliente = cliente AndAlso c.Contacto = contacto)
        End Using
    End Function

    Public Function CargarEnvioPorClienteYDireccion(cliente As String, contacto As String, direccion As String) As EnviosAgencia Implements IAgenciaService.CargarEnvioPorClienteYDireccion
        Using contexto = New NestoEntities
            Dim respuesta = (From e In contexto.EnviosAgencia.Include("AgenciasTransporte") Where e.Cliente = cliente And e.Contacto = contacto And e.Direccion = direccion And e.Estado = Constantes.Agencias.ESTADO_INICIAL_ENVIO).FirstOrDefault
            If Not IsNothing(respuesta) Then
                contexto.Entry(respuesta).Reference(Function(e) e.Empresas).Load()
            End If
            Return respuesta
        End Using
    End Function

    Public Function CargarDeudasCliente(cliente As String, fechaReclamar As Date) As List(Of ExtractoCliente) Implements IAgenciaService.CargarDeudasCliente
        Using contexto = New NestoEntities
            Return (From e In contexto.ExtractoCliente Where e.Número = cliente AndAlso
                e.ImportePdte <> 0 AndAlso
                (e.Estado Is Nothing Or (e.Estado <> "RTN" And e.Estado <> "RHS")) AndAlso
                (e.FormaPago <> "TRN") AndAlso
                (e.Ruta Is Nothing Or e.Ruta <> "RG") AndAlso
                e.FechaVto < fechaReclamar AndAlso
                e.TipoApunte <> "4").ToList
        End Using

    End Function

    ' ===== Nesto#340 (Agencias, slice A4.1): cerrar el envío y contabilizar su reembolso =====
    ' El servidor hace las dos cosas en una transacción (POST .../ConfirmarTramitacion) y estampa el
    ' usuario del asiento desde el JWT. Mientras se rueda, un parámetro de usuario decide el camino:
    ' sin fila (o con otro valor) se sigue usando el Entity Framework de siempre, que se queda intacto
    ' debajo. Protocolo de pies de plomo acordado el 20/08/26 tras los 3 sustos de A2.
    Friend Const CLAVE_TRAMITAR_POR_API As String = "TramitarEnvioPorApi"
    Private Const VALOR_TRAMITAR_POR_API As String = "API"

    Private Function TramitarPorApi() As Boolean
        Try
            Dim valor As String = Task.Run(Function() configuracion.leerParametro(
                Constantes.Empresas.EMPRESA_DEFECTO, CLAVE_TRAMITAR_POR_API)).GetAwaiter().GetResult()
            Return String.Equals(valor?.Trim(), VALOR_TRAMITAR_POR_API, StringComparison.OrdinalIgnoreCase)
        Catch
            ' Si no se puede leer el parámetro, el camino seguro es el de siempre.
            Return False
        End Try
    End Function

    Public Function TramitarEnvio(envio As EnviosAgencia) As String Implements IAgenciaService.TramitarEnvio
        If TramitarPorApi() Then
            Return TramitarEnvioPorApi(envio)
        End If
        Return TramitarEnvioConEntityFramework(envio)
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
            Return $"Error al tramitar pedido {envio.Pedido}: {ex.Message}"
        End Try
    End Function

    Private Function TramitarEnvioConEntityFramework(envio As EnviosAgencia) As String
        Dim success As Boolean = False

        Using transaction As New TransactionScope()
            Using DbContext As New NestoEntities
                Dim asiento As Integer = 0

                Dim envioEncontrado = DbContext.EnviosAgencia.Where(Function(e) e.Numero = envio.Numero).Single

                ' Issue #135: Convertir sentinel de reembolso antes de tramitar
                If envioEncontrado.Reembolso < 0 Then
                    envioEncontrado.Reembolso = 0
                End If

                envioEncontrado.Estado = Constantes.Agencias.ESTADO_TRAMITADO_ENVIO 'Enviado
                envioEncontrado.Fecha = Today
                envioEncontrado.FechaEntrega = Today.AddDays(1) 'Se entrega al día siguiente
                success = DbContext.SaveChanges()

                If success AndAlso envioEncontrado.Reembolso <> 0 Then
                    asiento = ContabilizarReembolso(envioEncontrado)
                    If asiento <= 0 Then
                        success = False
                    End If
                End If

                If success Then
                    transaction.Complete()
                    Dim unused = DbContext.SaveChanges()
                    Return "Envío del pedido " + envio.Pedido.ToString + " tramitado correctamente."
                Else
                    transaction.Dispose()
                    Return "Error al tramitar pedido " + envio.Pedido.ToString + "."
                End If
            End Using ' Cerramos el contexto
        End Using ' Cerramos la transaccion
    End Function


    Public Function ContabilizarReembolso(envio As EnviosAgencia) As Integer Implements IAgenciaService.ContabilizarReembolso

        If IsNothing(envio.AgenciasTransporte.CuentaReembolsos) Then
            Throw New Exception("Esta agencia no tiene establecida una cuenta de reembolsos. No se puede contabilizar.")
            Return -1
        End If

        Dim lineaInsertar As New PreContabilidad
        Dim movimientoLiq As ExtractoCliente
        movimientoLiq = CalcularMovimientoLiq(envio)


        With lineaInsertar
            .Empresa = envio.Empresa.Trim
            .Diario = Constantes.DiariosContables.DIARIO_REEMBOLSOS
            .TipoApunte = "3" 'Pago
            .TipoCuenta = "2" 'Cliente
            .Nº_Cuenta = envio.Cliente.Trim
            .Contacto = envio.Contacto.Trim
            .Fecha = Today 'envio.Fecha
            .FechaVto = Today ' envio.Fecha
            .Haber = envio.Reembolso
            .Concepto = GenerarConcepto(envio)
            .Contrapartida = envio.AgenciasTransporte.CuentaReembolsos.Trim
            .Asiento_Automático = False
            .FormaPago = envio.Empresas.FormaPagoEfectivo
            .Vendedor = envio.Vendedor
            If IsNothing(movimientoLiq) Then
                .Nº_Documento = envio.Pedido
                .Delegación = envio.Empresas.DelegaciónVarios
                .FormaVenta = envio.Empresas.FormaVentaVarios
            Else
                .Nº_Documento = movimientoLiq.Nº_Documento
                .Liquidado = movimientoLiq.Nº_Orden
                .Delegación = movimientoLiq.Delegación
                .FormaVenta = movimientoLiq.FormaVenta
                .Ruta = movimientoLiq.Ruta
                .Efecto = movimientoLiq.Efecto
            End If
        End With

        Dim asiento As Integer

        Using transaction As New TransactionScope()
            Using DbContext As New NestoEntities
                ' Iniciamos transacción
                Dim success As Boolean

                Try
                    Dim unused2 = DbContext.PreContabilidad.Add(lineaInsertar)
                    Dim unused1 = DbContext.SaveChanges()
                    asiento = DbContext.prdContabilizar(lineaInsertar.Empresa, Constantes.DiariosContables.DIARIO_REEMBOLSOS, configuracion.usuario)
                    transaction.Complete()
                    success = asiento > 0
                Catch e As Exception
                    transaction.Dispose()
                    Return -1
                End Try

                ' Comprobamos que las transacciones sean correctas
                If success Then
                    ' Reset the context since the operation succeeded. 
                    Dim unused = DbContext.SaveChanges()
                Else
                    Throw New Exception("Se ha producido un error y no se grabado los datos")
                End If
            End Using ' cerramos el contexto
        End Using 'cerramos la transcacción


        Return asiento

    End Function

    Private Function CalcularMovimientoLiq(env As EnviosAgencia) As ExtractoCliente Implements IAgenciaService.CalcularMovimientoLiq
        Return CalcularMovimientoLiq(env, env.Reembolso)
    End Function
    Private Function CalcularMovimientoLiq(env As EnviosAgencia, reembolsoAnterior As Double) As ExtractoCliente Implements IAgenciaService.CalcularMovimientoLiq
        Dim movimientos As ObservableCollection(Of ExtractoCliente)
        Dim movimientosConImporte As ObservableCollection(Of ExtractoCliente)

        If env.Cliente.Trim = Constantes.Clientes.Especiales.AMAZON OrElse env.Cliente.Trim = Constantes.Clientes.Especiales.TIENDA_ONLINE Then
            Return Nothing
        End If

        movimientos = If(reembolsoAnterior > 0,
            CargarExtractoCliente(env.Empresa, env.Cliente, True),
            CargarExtractoCliente(env.Empresa, env.Cliente, False))


        If movimientos.Count = 0 Then
            Return Nothing
        ElseIf movimientos.Count = 1 Then
            Return movimientos.SingleOrDefault
        Else
            If reembolsoAnterior > 0 Then
                movimientosConImporte = New ObservableCollection(Of ExtractoCliente)(From m In movimientos Where m.ImportePdte = reembolsoAnterior)
            Else
                movimientosConImporte = New ObservableCollection(Of ExtractoCliente)(From m In movimientos Where m.ImportePdte = env.Reembolso And m.Fecha = Today) ' con env.Fecha hay problemas cuando la etiqueta es del día anterior
            End If

            Return If(movimientosConImporte.Count = 0, movimientos.LastOrDefault, movimientosConImporte.LastOrDefault)
        End If
    End Function
    Private Function GenerarConcepto(envio As EnviosAgencia) As String Implements IAgenciaService.GenerarConcepto
        Dim agenciaEnvio As AgenciasTransporte = CargarAgencia(envio.Agencia)
        Return Left("S/Pago pedido " + envio.Pedido.ToString + " a " + agenciaEnvio.Nombre.Trim + " c/" + envio.Cliente.Trim, 50)
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

    Public Function EsTodoElPedidoOnline(empresa As String, pedido As Integer) As Boolean Implements IAgenciaService.EsTodoElPedidoOnline
        Using contexto = New NestoEntities
            Dim lineas = contexto.LinPedidoVta.Where(Function(l) l.Empresa = empresa AndAlso l.Número = pedido)
            Dim todoOnline = lineas.All(Function(l) Constantes.FormasVenta.FORMAS_ONLINE.Contains(l.Forma_Venta))
            Return todoOnline
        End Using
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
        Dim numeroFactura As String = Nothing
        Using contexto = New NestoEntities
            numeroFactura = (From l In contexto.LinPedidoVta
                             Where l.Empresa = empresa AndAlso l.Número = numeroPedido AndAlso l.Nº_Factura <> Nothing AndAlso l.Nº_Factura <> ""
                             Select l.Nº_Factura).FirstOrDefault()
        End Using

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
End Class
