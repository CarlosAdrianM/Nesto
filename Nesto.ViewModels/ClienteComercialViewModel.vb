Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Input
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Models
Imports Microsoft.Win32
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.Rapports
Imports Newtonsoft.Json
Imports CommunityToolkit.Mvvm.Input
Imports CommunityToolkit.Mvvm.ComponentModel

''' <summary>
''' Nesto#646 (paso 2): la ficha comercial del cliente (Ventanas/ClienteComercial.xaml: vendedores, estado, deudas,
''' pedidos, facturas, reclamación de deuda…). Antes era la mitad de ClientesViewModel; el código se ha movido tal cual.
''' Lo común (empresa/cliente/contacto y la carga de la ficha) está en <see cref="ClienteFichaViewModelBase"/>.
''' </summary>
Public Class ClienteComercialViewModel
    Inherits ClienteFichaViewModelBase
    '    Implements IActiveAware

    ' Nesto#490 (4C.5, tramo 3a): sin IUnityContainer. Lo que se le pedía (abrir un pedido, el registro de errores y el
    ' servicio de pedidos) llega por el constructor. Con el constructor de los tests los tres quedan a Nothing, igual que
    ' antes el contenedor.
    Private ReadOnly abridorPedidos As IAbridorPedidos
    Private ReadOnly Property servicioRapports As IRapportService
    Private EsUsuarioAdministracion As Boolean = False

    ' Nesto#458: la GARANTÍA de quién puede cambiar estado/vendedor vive en el servidor
    ' (ValidadorCambioClienteComercial). Esto de aquí es solo que no se pueda ni intentar:
    ' filtrar el combo al equipo y deshabilitar con el motivo a la vista.
    Private EsOficinaComercial As Boolean = False
    Private EquipoUsuario As New List(Of String)


    Public Sub New(configuracion As IConfiguracion, abridorPedidos As IAbridorPedidos, dialogService As IServicioDialogos, servicio As IClienteComercialService, servicioRapports As IRapportService, servicioAutenticacion As IServicioAutenticacion, servicioErrores As IServicioRegistroErrores, servicioPedidos As IPedidoVentaService)
        Me.configuracion = configuracion
        Me.abridorPedidos = abridorPedidos
        Me.servicioErrores = servicioErrores
        _servicioPedidos = servicioPedidos
        Me.dialogService = dialogService
        Me.servicio = servicio
        Me.servicioRapports = servicioRapports
        Me.servicioAutenticacion = servicioAutenticacion
        _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
        cargarDatos()
        clienteActivo = Nothing
        'inicializarListaClientesVendedor()
        ListaClientesFiltrable = New ColeccionFiltrable(New ObservableCollection(Of ClienteJson)) With {
            .TieneDatosIniciales = False
        }
        AddHandler ListaClientesFiltrable.HayQueCargarDatos, Async Sub()
                                                                 Await Task.Delay(400)
                                                                 inicializarListaClientesVendedor(ListaClientesFiltrable.Filtro)
                                                             End Sub
        AddHandler ListaClientesFiltrable.ListaChanged, Sub()
                                                            If IsNothing(clienteActivoDTO) AndAlso Not IsNothing(ListaClientesFiltrable) AndAlso Not IsNothing(ListaClientesFiltrable.Lista) Then
                                                                clienteActivoDTO = ListaClientesFiltrable.Lista.FirstOrDefault
                                                            End If
                                                        End Sub
        ReclamarDeudaCommand = New RelayCommand(AddressOf OnReclamarDeuda)
        AbrirEnlaceReclamacionCommand = New RelayCommand(AddressOf OnAbrirEnlaceReclamacion, AddressOf CanAbrirEnlaceReclamacion)
        ConfirmarReclamarDeudaCommand = New RelayCommand(AddressOf OnConfirmarReclamarDeuda, AddressOf CanConfirmarReclamarDeuda)
        GuardarEfectoDeudaCommand = New RelayCommand(AddressOf OnGuardarEfectoDeuda, AddressOf CanGuardarEfectoDeuda)
    End Sub

    ' Nesto#340 (1C.8, slice 4): constructor para tests. Inyecta fakes, no toca EF (DbContext
    ' queda a Nothing) ni el contenedor de Prism, y no dispara cargarDatos. ListaClientesFiltrable
    ' queda a Nothing a propósito: el setter de clienteActivo no dispara las cargas HTTP en
    ' cascada. Unity sigue eligiendo el constructor de 8 parámetros (el más largo).
    Public Sub New(configuracion As IConfiguracion, dialogService As IServicioDialogos, servicio As IClienteComercialService, servicioRapports As IRapportService, servicioAutenticacion As IServicioAutenticacion, Optional abridorPedidos As IAbridorPedidos = Nothing)
        Me.configuracion = configuracion
        Me.abridorPedidos = abridorPedidos
        Me.dialogService = dialogService
        Me.servicio = servicio
        Me.servicioRapports = servicioRapports
        Me.servicioAutenticacion = servicioAutenticacion
        ReclamarDeudaCommand = New RelayCommand(AddressOf OnReclamarDeuda)
        AbrirEnlaceReclamacionCommand = New RelayCommand(AddressOf OnAbrirEnlaceReclamacion, AddressOf CanAbrirEnlaceReclamacion)
        ConfirmarReclamarDeudaCommand = New RelayCommand(AddressOf OnConfirmarReclamarDeuda, AddressOf CanConfirmarReclamarDeuda)
        GuardarEfectoDeudaCommand = New RelayCommand(AddressOf OnGuardarEfectoDeuda, AddressOf CanGuardarEfectoDeuda)
    End Sub

#Region "Propiedades"
    Private _clienteServidor As ClienteJson
    Public Property clienteServidor As ClienteJson
        Get
            Return _clienteServidor
        End Get
        Set(value As ClienteJson)
            _clienteServidor = value
            ' Nesto#644 (5): cómo llegó la ficha, para mandar al guardar solo lo que cambie el usuario
            _comercialCargado = ValoresComercialesFicha.DeLaFicha(value)
            OnPropertyChanged("clienteServidor")
            ' Nesto#458: el permiso depende de quién lleva el cliente que se está mirando
            OnPropertyChanged(NameOf(PuedeEditarComercial))
            OnPropertyChanged(NameOf(MotivoComercialBloqueado))
            OnPropertyChanged(NameOf(ExigeSuPedido)) ' Nesto#562
        End Set
    End Property

    Private _comercialCargado As ValoresComercialesFicha

    Private _esJefeDeVentas As Boolean
    Public Property EsJefeDeVentas As Boolean
        Get
            Return _esJefeDeVentas
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_esJefeDeVentas, value)
        End Set
    End Property

    ''' <summary>Nesto#458: vendedor cuyo equipo filtra el combo. Nothing = sin filtro (oficina).</summary>
    Private _filtroEquipoVendedores As String
    Public Property FiltroEquipoVendedores As String
        Get
            Return _filtroEquipoVendedores
        End Get
        Set(value As String)
            Dim unused = SetProperty(_filtroEquipoVendedores, value)
        End Set
    End Property

    ''' <summary>
    ''' Nesto#458: si el usuario puede tocar estado/vendedor DEL CLIENTE QUE MIRA. La oficina
    ''' siempre; un vendedor, solo si el cliente lo lleva alguien de su conjunto (su equipo, y NV
    ''' únicamente para los jefes). Espejo de la regla del servidor, que es quien manda.
    ''' </summary>
    Public ReadOnly Property PuedeEditarComercial As Boolean
        Get
            If EsOficinaComercial OrElse String.IsNullOrWhiteSpace(FiltroEquipoVendedores) Then
                Return True
            End If
            Dim actual As String = clienteServidor?.vendedor?.Trim()
            If String.IsNullOrWhiteSpace(actual) Then
                Return True
            End If
            Return ConjuntoEquipoComercial().Contains(actual, StringComparer.OrdinalIgnoreCase)
        End Get
    End Property

    Public ReadOnly Property MotivoComercialBloqueado As String
        Get
            If PuedeEditarComercial Then
                Return Nothing ' sin tooltip cuando se puede editar
            End If
            Return $"El cliente lo lleva {clienteServidor?.vendedor?.Trim()}, que no es de tu equipo: no puedes cambiarlo"
        End Get
    End Property

    ''' <summary>
    ''' Nesto#562 (NestoAPI#723): solo Administración, Dirección e Informática pueden cambiar si el cliente exige el P.O.
    ''' (el servidor contesta 403 a los demás). Para el resto la casilla es de solo lectura.
    ''' </summary>
    Public ReadOnly Property PuedeCambiarSuPedidoObligatorio As Boolean
        Get
            Return configuracion IsNot Nothing AndAlso
                (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) OrElse
                 configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
                 configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA))
        End Get
    End Property

    Public ReadOnly Property TooltipSuPedidoObligatorio As String
        Get
            Return If(PuedeCambiarSuPedidoObligatorio,
                "Si está marcada, no se puede grabar ningún pedido de este cliente (de ninguno de sus contactos) sin «Su pedido / P.O.», ni facturarlo. Se guarda al marcarla o desmarcarla.",
                "Solo Administración, Dirección o Informática pueden cambiar si el cliente exige el P.O.")
        End Get
    End Property

    ''' <summary>
    ''' Nesto#562 (NestoAPI#723): la casilla «Exige P.O. en todos los pedidos». Al cambiarla se guarda en el momento
    ''' (PUT Clientes/{empresa}/{cliente}/SuPedidoObligatorio), sin esperar al botón Guardar; si falla, vuelve a como estaba.
    ''' </summary>
    Public Property ExigeSuPedido As Boolean
        Get
            Return clienteServidor IsNot Nothing AndAlso clienteServidor.suPedidoObligatorio
        End Get
        Set(value As Boolean)
            If clienteServidor Is Nothing OrElse clienteServidor.suPedidoObligatorio = value Then
                Return
            End If
            Dim unused = CambiarSuPedidoObligatorioAsync(value)
        End Set
    End Property

    Public Async Function CambiarSuPedidoObligatorioAsync(exige As Boolean) As Task
        Dim ficha As ClienteJson = clienteServidor
        If ficha Is Nothing OrElse String.IsNullOrWhiteSpace(ficha.cliente) OrElse ficha.suPedidoObligatorio = exige Then
            Return
        End If
        If Not PuedeCambiarSuPedidoObligatorio Then
            OnPropertyChanged(NameOf(ExigeSuPedido)) ' la casilla vuelve a lo que había
            Return
        End If
        Dim anterior As Boolean = ficha.suPedidoObligatorio
        ficha.suPedidoObligatorio = exige
        OnPropertyChanged(NameOf(ExigeSuPedido))
        Try
            Await servicio.PonerSuPedidoObligatorio(ficha.empresa, ficha.cliente, exige)
        Catch ex As Exception
            ficha.suPedidoObligatorio = anterior
            OnPropertyChanged(NameOf(ExigeSuPedido))
            dialogService?.ShowError($"No se ha podido cambiar si el cliente {ficha.cliente.Trim()} exige el P.O.: {If(ex.InnerException?.Message, ex.Message)}")
        End Try
    End Function

    Private Function ConjuntoEquipoComercial() As List(Of String)
        Dim conjunto As New List(Of String)(EquipoUsuario)
        If Not String.IsNullOrWhiteSpace(vendedor) Then
            conjunto.Add(vendedor.Trim())
        End If
        ' El genérico NV es cosa del JEFE: un vendedor raso solo toca sus propios clientes
        If EsJefeDeVentas Then
            conjunto.Add(Constantes.Vendedores.VENDEDOR_POR_DEFECTO)
        End If
        Return conjunto
    End Function

    Private _deudaSeleccionada As ExtractoClienteDTO
    Public Property DeudaSeleccionada As ExtractoClienteDTO
        Get
            Return _deudaSeleccionada
        End Get
        Set(value As ExtractoClienteDTO)
            Dim unused = SetProperty(_deudaSeleccionada, value)
            GuardarEfectoDeudaCommand.NotifyCanExecuteChanged()
        End Set
    End Property

    Private _estadoPeluqueria As Short
    Public Property estadoPeluqueria As Short
        Get
            Return _estadoPeluqueria
        End Get
        Set(value As Short)
            _estadoPeluqueria = value
            If IsNothing(clienteServidor) OrElse IsNothing(clienteServidor.VendedoresGrupoProducto) Then
                Return
            End If
            If clienteServidor.VendedoresGrupoProducto.Count = 0 Then
                clienteServidor.VendedoresGrupoProducto.Add(New Nesto.Models.VendedorGrupoProductoDTO With
                                       {
                                       .grupoProducto = "PEL",
                                       .vendedor = "NV",
                                       .estado = Constantes.Clientes.ESTADO_NORMAL,
                                       .usuario = configuracion.usuario
                                       }
                )
            End If
            clienteServidor.VendedoresGrupoProducto.ElementAt(0).estado = value
            OnPropertyChanged(NameOf(estadoPeluqueria))
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 4): clienteActivo YA ES la ficha completa de la API (incluye
    ' VendedoresGrupoProducto), así que no hace falta una segunda llamada: clienteServidor se
    ' unifica con clienteActivo. Friend para los tests (el constructor de tests no la lanza).
    Friend Sub cargarVendedoresPorGrupo()
        If IsNothing(clienteActivo) Then
            Return
        End If
        clienteServidor = clienteActivo
        If Not IsNothing(clienteServidor.VendedoresGrupoProducto) AndAlso clienteServidor.VendedoresGrupoProducto.Count > 0 Then
            vendedorPorGrupo = clienteServidor.VendedoresGrupoProducto.ElementAt(0).vendedor
            estadoPeluqueria = clienteServidor.VendedoresGrupoProducto.ElementAt(0).estado
        Else
            vendedorPorGrupo = Nothing
            estadoPeluqueria = Nothing
        End If
    End Sub
    ' Nesto#340 (1C.8, slice 3): los últimos 20 seguimientos se leen de la API
    ' (SeguimientosClientes?empresa=&cliente=&contacto=) en vez de la nav property EF
    ' clienteActivo.SeguimientoCliente. Diferencia deliberada: la API limita a 3 años
    ' (para la ficha comercial el top-20 de más de 3 años no aporta).
    Private Async Sub CargarSeguimientos()
        If IsNothing(clienteActivo) OrElse IsNothing(clienteActivo.cliente) Then
            seguimientosOrdenados = Nothing
            Return
        End If
        Dim testigo As Integer = TestigoFicha() ' Nesto#644 (2)
        Try
            Using client As HttpClient = _clienteApiFactory.Crear()
                Dim urlConsulta As String = "SeguimientosClientes"
                urlConsulta += "?empresa=" + If(clienteActivo.empresa?.Trim, String.Empty)
                urlConsulta += "&cliente=" + clienteActivo.cliente.Trim
                urlConsulta += "&contacto=" + If(clienteActivo.contacto?.Trim, String.Empty)
                Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
                If Not EsFichaActual(testigo) Then
                    Return
                End If
                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    If Not EsFichaActual(testigo) Then
                        Return
                    End If
                    Dim seguimientos = JsonConvert.DeserializeObject(Of List(Of SeguimientoResumen))(respuesta)
                    seguimientosOrdenados = New ObservableCollection(Of SeguimientoResumen)(
                        seguimientos.OrderByDescending(Function(s) s.Fecha).Take(20))
                Else
                    seguimientosOrdenados = New ObservableCollection(Of SeguimientoResumen)()
                End If
            End Using
        Catch ex As Exception
            If EsFichaActual(testigo) Then
                seguimientosOrdenados = New ObservableCollection(Of SeguimientoResumen)()
            End If
        End Try
    End Sub

    ' Nesto#340 (1C.8, slice 2): la deuda vencida se lee de la API (ExtractosCliente/DeudaVencida,
    ' misma consulta: empresas 1 y 3, FechaVto pasada, ImportePdte <> 0) en vez de sumar
    ' ExtractoCliente con EF.
    Private Async Sub CargarDeudaVencida()
        If IsNothing(clienteActivo) OrElse IsNothing(clienteActivo.cliente) Then
            deudaVencida = 0
            Return
        End If
        Dim testigo As Integer = TestigoFicha() ' Nesto#644 (2)
        Try
            Using client As HttpClient = _clienteApiFactory.Crear()
                Dim urlConsulta As String = "ExtractosCliente/DeudaVencida"
                urlConsulta += "?cliente=" + clienteActivo.cliente.Trim
                urlConsulta += "&contacto=" + If(clienteActivo.contacto?.Trim, String.Empty)
                Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
                If Not EsFichaActual(testigo) Then
                    Return
                End If
                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    If Not EsFichaActual(testigo) Then
                        Return
                    End If
                    deudaVencida = JsonConvert.DeserializeObject(Of Decimal)(respuesta)
                Else
                    deudaVencida = 0
                End If
            End Using
        Catch ex As Exception
            If EsFichaActual(testigo) Then
                deudaVencida = 0
            End If
        End Try
    End Sub

    ' Nesto#340 (1C.8): el grid de ventas agrupadas por producto se lee de la API
    ' (ventascliente/productos) en vez de consultar LinPedidoVta con EF. La API replica la
    ' consulta antigua: empresas 1 y 3, Estado >= 2, agrupado por producto con suma de
    ' cantidades y fecha de última venta. Sin fechaDesde devuelve las ventas de siempre.
    Private Async Sub CargarListaVentas()
        If IsNothing(clienteActivo) OrElse IsNothing(clienteActivo.cliente) Then
            listaVentas = Nothing
            Return
        End If
        Dim testigo As Integer = TestigoFicha() ' Nesto#644 (2)
        Try
            Using client As HttpClient = _clienteApiFactory.Crear()
                Dim urlConsulta As String = "ventascliente/productos"
                urlConsulta += "?clienteId=" + clienteActivo.cliente.Trim
                urlConsulta += "&contacto=" + If(clienteActivo.contacto?.Trim, String.Empty)
                If rangoFechasVenta <> "System.Windows.Controls.ComboBoxItem: Ventas de siempre" Then 'esto está fatal, hay que desacoplarlo de la vista
                    urlConsulta += "&fechaDesde=" + Date.Now.AddYears(-1).ToString("s")
                End If

                Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
                If Not EsFichaActual(testigo) Then
                    Return
                End If
                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    If Not EsFichaActual(testigo) Then
                        Return
                    End If
                    listaVentas = JsonConvert.DeserializeObject(Of ObservableCollection(Of lineaVentaAgrupada))(respuesta)
                Else
                    listaVentas = New ObservableCollection(Of lineaVentaAgrupada)()
                End If
            End Using
        Catch ex As Exception
            If EsFichaActual(testigo) Then
                listaVentas = New ObservableCollection(Of lineaVentaAgrupada)()
            End If
        End Try
    End Sub

    Private _clienteActivoDTO As ClienteJson
    Public Property clienteActivoDTO As ClienteJson
        Get
            Return _clienteActivoDTO
        End Get
        Set(value As ClienteJson)
            _clienteActivoDTO = value
            ' Nesto#340 (1C.8, slice 4): la ficha completa se carga de la API (el DTO de la lista
            ' no trae VendedoresGrupoProducto ni PersonasContacto), ya no se consulta EF.
            If Not IsNothing(clienteActivoDTO) Then
                ActualizarClienteAsync(clienteActivoDTO.empresa, clienteActivoDTO.cliente, clienteActivoDTO.contacto)
            Else
                clienteActivo = Nothing
            End If

            OnPropertyChanged("clienteActivoDTO")
        End Set
    End Property

    Private Property _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
            OnPropertyChanged("vendedor")
            ' Nesto#340 (1C.8): eliminadas las consultas EF a FamiliasVendedor — alimentaban
            ' esVendedorDeFamilias/listaCodigosPostalesVendedor, que solo se leían en código
            ' comentado desde hace años (filtro de clientes por códigos postales).
        End Set
    End Property

    Private _vendedorPorGrupo As String
    Public Property vendedorPorGrupo As String
        Get
            Return _vendedorPorGrupo
        End Get
        Set(value As String)
            _vendedorPorGrupo = value
            If IsNothing(clienteServidor) OrElse IsNothing(clienteServidor.VendedoresGrupoProducto) Then
                Return
            End If
            If clienteServidor.VendedoresGrupoProducto.Count = 0 Then
                clienteServidor.VendedoresGrupoProducto.Add(New Nesto.Models.VendedorGrupoProductoDTO With
                                       {
                                       .grupoProducto = "PEL",
                                       .vendedor = "NV",
                                       .estado = Constantes.Clientes.ESTADO_NORMAL,
                                       .usuario = configuracion.usuario
                                       }
                )
            End If
            clienteServidor.VendedoresGrupoProducto.ElementAt(0).vendedor = value
            OnPropertyChanged("vendedorPorGrupo")
        End Set
    End Property

    Private _listaClientesFiltrable As ColeccionFiltrable
    Public Property ListaClientesFiltrable As ColeccionFiltrable
        Get
            Return _listaClientesFiltrable
        End Get
        Set(value As ColeccionFiltrable)
            Dim unused = SetProperty(_listaClientesFiltrable, value)
        End Set
    End Property

    'Private _listaClientesVendedor As ObservableCollection(Of ClienteJson)
    'Public Property listaClientesVendedor As ObservableCollection(Of ClienteJson)
    '    Get
    '        Return _listaClientesVendedor
    '    End Get
    '    Set(value As ObservableCollection(Of ClienteJson))
    '        _listaClientesVendedor = value
    '        If IsNothing(clienteActivoDTO) And Not IsNothing(_listaClientesVendedor) Then
    '            clienteActivoDTO = _listaClientesVendedor.FirstOrDefault
    '        End If
    '        OnPropertyChanged("listaClientesVendedor")
    '    End Set
    'End Property

    ' Nesto#340 (1C.8, slice 3): POCO en vez de la entidad EF SeguimientoCliente (la vista solo
    ' bindea Fecha/Tipo/Vendedor/Comentarios/Usuario).
    Private _seguimientosOrdenados As ObservableCollection(Of SeguimientoResumen)
    Public Property seguimientosOrdenados As ObservableCollection(Of SeguimientoResumen)
        Get
            Return _seguimientosOrdenados
        End Get
        Set(value As ObservableCollection(Of SeguimientoResumen))
            _seguimientosOrdenados = value
            OnPropertyChanged("seguimientosOrdenados")
        End Set
    End Property

    Private _listaVentas As ObservableCollection(Of lineaVentaAgrupada)
    Public Property listaVentas As ObservableCollection(Of lineaVentaAgrupada)
        Get
            Return _listaVentas
        End Get
        Set(value As ObservableCollection(Of lineaVentaAgrupada))
            _listaVentas = value
            OnPropertyChanged("listaVentas")
        End Set
    End Property

    'Private _filtro As String
    'Public Property filtro As String
    '    Get
    '        Return _filtro
    '    End Get
    '    Set(value As String)
    '        _filtro = value
    '        actualizarFiltro(filtro)
    '    End Set
    'End Property

    'Public Sub actualizarFiltro(filtro As String)
    '    If IsNothing(filtro) Then
    '        Return
    '    End If

    '    If IsNothing(listaClientesVendedor) Then
    '        inicializarListaClientesVendedor(filtro)
    '        Return
    '    End If

    '    If filtro.Trim <> "" Then
    '        listaClientesVendedor = New ObservableCollection(Of ClienteJson) _
    '        (From c In listaClientesVendedor Where
    '                                             (Not IsNothing(c.cliente) AndAlso c.cliente.ToLower.Trim = filtro.ToLower) OrElse
    '            (Not IsNothing(c.direccion) AndAlso c.direccion.ToLower.Contains(filtro.ToLower)) OrElse
    '            (Not IsNothing(c.nombre) AndAlso c.nombre.ToLower.Contains(filtro.ToLower)) OrElse
    '            (Not IsNothing(c.telefono) AndAlso c.telefono.ToLower.Contains(filtro.ToLower)) OrElse
    '            (Not IsNothing(c.cifNif) AndAlso c.cifNif.ToLower.Contains(filtro.ToLower)) OrElse
    '            (Not IsNothing(c.poblacion) AndAlso c.poblacion.ToLower.Contains(filtro.ToLower)) OrElse
    '            (Not IsNothing(c.comentarios) AndAlso c.comentarios.ToLower.Contains(filtro.ToLower)))
    '    Else
    '        inicializarListaClientesVendedor(filtro)
    '        filtro = ""
    '    End If

    '    OnPropertyChanged("filtro")
    'End Sub

    Private _rangoFechasVenta As String
    Public Property rangoFechasVenta As String
        Get
            Return _rangoFechasVenta
        End Get
        Set(value As String)
            _rangoFechasVenta = value
            If Not IsNothing(clienteActivo) Then
                CargarListaVentas()
            End If
            OnPropertyChanged("rangoFechasVenta")
        End Set
    End Property

    Private _deudaVencida As Nullable(Of Decimal)
    Public Property deudaVencida As Nullable(Of Decimal)
        Get
            Return _deudaVencida
        End Get
        Set(value As Nullable(Of Decimal))
            _deudaVencida = value
            OnPropertyChanged("deudaVencida")
        End Set
    End Property

    Private _listaDeudas As ObservableCollection(Of ExtractoClienteDTO)
    Public Property ListaDeudas As ObservableCollection(Of ExtractoClienteDTO)
        Get
            Return _listaDeudas
        End Get
        Set(value As ObservableCollection(Of ExtractoClienteDTO))
            Dim unused = SetProperty(_listaDeudas, value)
        End Set
    End Property

    Private _listaFacturas As ObservableCollection(Of ExtractoClienteDTO)
    Public Property ListaFacturas As ObservableCollection(Of ExtractoClienteDTO)
        Get
            Return _listaFacturas
        End Get
        Set(value As ObservableCollection(Of ExtractoClienteDTO))
            Dim unused = SetProperty(_listaFacturas, value)
        End Set
    End Property

    ''' <summary>
    ''' Facturas seleccionadas en el SelectorFacturas (Issue #279)
    ''' </summary>
    Private _facturasSeleccionadas As ObservableCollection(Of FacturaClienteDTO)
    Public Property FacturasSeleccionadas As ObservableCollection(Of FacturaClienteDTO)
        Get
            Return _facturasSeleccionadas
        End Get
        Set(value As ObservableCollection(Of FacturaClienteDTO))
            If SetProperty(_facturasSeleccionadas, value) Then
                CommandManager.InvalidateRequerySuggested()
                ' Nesto#259: se propone el correo de facturas del cliente (solo propuesta: no bloquea nada)
                Dim unused = ProponerCorreoFacturasAsync()
            End If
        End Set
    End Property

    ''' <summary>
    ''' Nesto#259: a dónde se mandan las facturas marcadas. Se propone el correo de facturas del cliente, pero se puede
    ''' cambiar (varios separados por «;» o «,»).
    ''' </summary>
    Private _correoEnvioFacturas As String
    Public Property CorreoEnvioFacturas As String
        Get
            Return _correoEnvioFacturas
        End Get
        Set(value As String)
            Dim unused = SetProperty(_correoEnvioFacturas, value)
        End Set
    End Property

    ' El último correo propuesto y de qué cliente: si el usuario no lo ha cambiado y marca facturas de otro cliente,
    ' se cambia por el de ese cliente; lo que haya escrito él no se pisa nunca.
    Private _ultimoCorreoPropuesto As String
    Private _clienteDelCorreoPropuesto As String

    Public Async Function ProponerCorreoFacturasAsync() As Task
        Dim primera As FacturaClienteDTO = FacturasSeleccionadas?.FirstOrDefault()
        If primera Is Nothing OrElse servicio Is Nothing Then
            Return
        End If
        Dim cliente As String = primera.Cliente?.Trim()
        Dim escritoPorElUsuario As Boolean = Not String.IsNullOrWhiteSpace(CorreoEnvioFacturas) AndAlso
            Not String.Equals(CorreoEnvioFacturas, _ultimoCorreoPropuesto, StringComparison.OrdinalIgnoreCase)
        Dim yaEsDeEsteCliente As Boolean = Not String.IsNullOrWhiteSpace(CorreoEnvioFacturas) AndAlso cliente = _clienteDelCorreoPropuesto
        If escritoPorElUsuario OrElse yaEsDeEsteCliente Then
            Return
        End If
        Try
            Dim correo As String = Await servicio.LeerCorreoFacturas(primera.Empresa?.Trim(), primera.Documento?.Trim())
            If String.IsNullOrWhiteSpace(correo) Then
                Return
            End If
            ' Mientras se leía, el usuario puede haber escrito otro: el suyo manda
            If String.IsNullOrWhiteSpace(CorreoEnvioFacturas) OrElse String.Equals(CorreoEnvioFacturas, _ultimoCorreoPropuesto, StringComparison.OrdinalIgnoreCase) Then
                _ultimoCorreoPropuesto = correo.Trim()
                _clienteDelCorreoPropuesto = cliente
                CorreoEnvioFacturas = _ultimoCorreoPropuesto
            End If
        Catch
            ' Solo es una propuesta: sin ella el usuario escribe el correo
        End Try
    End Function

    Private _listaPedidos As ObservableCollection(Of ResumenPedido)
    Public Property ListaPedidos As ObservableCollection(Of ResumenPedido)
        Get
            Return _listaPedidos
        End Get
        Set(value As ObservableCollection(Of ResumenPedido))
            _listaPedidos = value
            OnPropertyChanged("ListaPedidos")
        End Set
    End Property

    Private _indiceSeleccionado As Integer
    Public Property IndiceSeleccionado As Integer
        Get
            Return _indiceSeleccionado
        End Get
        Set(value As Integer)
            _indiceSeleccionado = value
            If _indiceSeleccionado = 1 Then ' Facturas
                CargarFacturas()
            ElseIf _indiceSeleccionado = 2 Then 'Pedidos
                CargarPedidos()
            ElseIf _indiceSeleccionado = 3 Then 'Deudas
                CargarDeudas()
            End If
            OnPropertyChanged("IndiceSeleccionado")
        End Set
    End Property

    Public ReadOnly Property SumaDeudasSeleccionadas As Decimal
        Get
            If IsNothing(ListaDeudas) OrElse Not ListaDeudas.Any(Function(l) l.Seleccionada) Then
                Return 0
            End If
            Return ListaDeudas.Where(Function(l) l.Seleccionada).Sum(Function(l) l.ImportePendiente)
        End Get
    End Property

    Private _importeReclamarDeuda As Decimal
    Public Property ImporteReclamarDeuda As Decimal
        Get
            Return _importeReclamarDeuda
        End Get
        Set(value As Decimal)
            Dim unused = SetProperty(_importeReclamarDeuda, value)
            ConfirmarReclamarDeudaCommand.NotifyCanExecuteChanged()
        End Set
    End Property

    ' NestoAPI#295: el asunto por defecto SOLO cuando hay efectos seleccionados (en ese caso el
    ' concepto contable identifica el pago por el efecto liquidado). Sin efectos, el usuario debe
    ' escribir un concepto real: se contabiliza tal cual en el extracto del cliente.
    Public Const ASUNTO_PAGO_POR_DEFECTO As String = "Enlace de pago a Nueva Visión"

    Private _asuntoReclamarDeuda As String = String.Empty
    Public Property AsuntoReclamarDeuda As String
        Get
            Return _asuntoReclamarDeuda
        End Get
        Set(value As String)
            If SetProperty(_asuntoReclamarDeuda, value) Then
                ConfirmarReclamarDeudaCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    ' NestoAPI#295: pone el asunto por defecto al seleccionar efectos (si el usuario no ha
    ' escrito nada) y lo vacía al deseleccionarlos todos (si sigue siendo el por defecto).
    Private Sub ActualizarAsuntoPorDefecto()
        Dim hayEfectosSeleccionados = ListaDeudas IsNot Nothing AndAlso ListaDeudas.Any(Function(l) l.Seleccionada)
        If hayEfectosSeleccionados AndAlso String.IsNullOrWhiteSpace(AsuntoReclamarDeuda) Then
            AsuntoReclamarDeuda = ASUNTO_PAGO_POR_DEFECTO
        ElseIf Not hayEfectosSeleccionados AndAlso AsuntoReclamarDeuda = ASUNTO_PAGO_POR_DEFECTO Then
            AsuntoReclamarDeuda = String.Empty
        End If
    End Sub

    Private _correoReclamarDeuda As String
    Public Property CorreoReclamarDeuda As String
        Get
            Return _correoReclamarDeuda
        End Get
        Set(value As String)
            Dim unused = SetProperty(_correoReclamarDeuda, value)
            ConfirmarReclamarDeudaCommand.NotifyCanExecuteChanged()
            EnviarEnlacePagoPedidoCommand.NotifyCanExecuteChanged()
        End Set
    End Property

    Private _motivoCambioEstado As String
    Public Property MotivoCambioEstado As String
        Get
            Return _motivoCambioEstado
        End Get
        Set(value As String)
            Dim unused = SetProperty(_motivoCambioEstado, value)
            GuardarEfectoDeudaCommand.NotifyCanExecuteChanged()
        End Set
    End Property

    Private _movilReclamarDeuda As String
    Public Property MovilReclamarDeuda As String
        Get
            Return _movilReclamarDeuda
        End Get
        Set(value As String)
            Dim unused = SetProperty(_movilReclamarDeuda, value)
            ConfirmarReclamarDeudaCommand.NotifyCanExecuteChanged()
            EnviarEnlacePagoPedidoCommand.NotifyCanExecuteChanged()
        End Set
    End Property

    Private _nombreReclamarDeuda As String
    Public Property NombreReclamarDeuda As String
        Get
            Return _nombreReclamarDeuda
        End Get
        Set(value As String)
            Dim unused = SetProperty(_nombreReclamarDeuda, value)
        End Set
    End Property


    Private _motorPagosSeleccionado As String = "Paygold"
    Public Property MotorPagosSeleccionado As String
        Get
            Return _motorPagosSeleccionado
        End Get
        Set(value As String)
            Dim unused = SetProperty(_motorPagosSeleccionado, value)
        End Set
    End Property

    Public ReadOnly Property PuedeElegirMotorPagos As Boolean
        Get
            Return EsUsuarioAdministracion
        End Get
    End Property

    Private _enlaceReclamarDeuda As String
    Public Property EnlaceReclamarDeuda As String
        Get
            Return _enlaceReclamarDeuda
        End Get
        Set(value As String)
            Dim unused = SetProperty(_enlaceReclamarDeuda, value)
            AbrirEnlaceReclamacionCommand.NotifyCanExecuteChanged()
        End Set
    End Property

    ''' <summary>
    ''' Histórico de enlaces de pago NestoPago emitidos al cliente.
    ''' Issue Nesto#322.
    ''' </summary>
    Private _historialPagos As ObservableCollection(Of PagoTPVDTO)
    Public Property HistorialPagos As ObservableCollection(Of PagoTPVDTO)
        Get
            Return _historialPagos
        End Get
        Set(value As ObservableCollection(Of PagoTPVDTO))
            Dim unused = SetProperty(_historialPagos, value)
        End Set
    End Property

    Private _pagoSeleccionado As PagoTPVDTO
    Public Property PagoSeleccionado As PagoTPVDTO
        Get
            Return _pagoSeleccionado
        End Get
        Set(value As PagoTPVDTO)
            Dim unused = SetProperty(_pagoSeleccionado, value)
        End Set
    End Property

#End Region
#Region "Comandos"
    Private _cmdGuardarVendedores As ICommand
    Public ReadOnly Property cmdGuardarVendedores() As ICommand
        Get
            If _cmdGuardarVendedores Is Nothing Then
                _cmdGuardarVendedores = ComandosReevaluables.Crear(AddressOf GuardarVendedores, AddressOf CanGuardarVendedores)
            End If
            Return _cmdGuardarVendedores
        End Get
    End Property
    Private Function CanGuardarVendedores(ByVal param As Object) As Boolean
        Return True
    End Function
#Region "Nesto#558: motivo al cambiar el estado del cliente"

    ' El estado de la ficha tal como se cargó (se fija en el setter de clienteActivo y al guardar con éxito).
    Private _estadoCargado As Integer?

    ''' <summary>Nesto#558: el estado con que se cargó la ficha; con él se sabe si el guardado cambia el estado.</summary>
    Public ReadOnly Property EstadoCargado As Integer?
        Get
            Return _estadoCargado
        End Get
    End Property

    ''' <summary>
    ''' Nesto#558 (NestoAPI#716, 2.2): si el estado pasa a una baja (9, -1, 93, 11, 22, 95, 99) o sale de una, pregunta el
    ''' motivo (catálogo MotivoCambioEstadoCliente) y un comentario, los dos opcionales, y los deja en la ficha para que
    ''' viajen en el PUT de ClienteComercial (motivoCambioEstado / comentarioCambioEstado). Si el estado no cambia, o la
    ''' persona no contesta nada, no se manda nada. Nunca impide guardar. Devuelve True si se ha preguntado.
    ''' </summary>
    Public Function PedirMotivoCambioEstadoSiHaceFalta() As Boolean
        If clienteServidor Is Nothing OrElse clienteActivo Is Nothing Then
            Return False
        End If
        clienteServidor.motivoCambioEstado = Nothing
        clienteServidor.comentarioCambioEstado = Nothing
        If Not MotivoCambioEstadoCliente.PideMotivo(_estadoCargado, clienteActivo.estado) Then
            Return False
        End If
        Dim respuesta As MotivoCambioEstado = PeticionMotivoCambioEstado.Pedir(dialogService, "Motivo del cambio de estado",
            $"El cliente {clienteActivo.cliente?.Trim()} pasa del estado {If(_estadoCargado.HasValue, _estadoCargado.Value.ToString(), "?")} al {clienteActivo.estado}. Si lo sabe, indique el motivo; puede dejarlo en blanco.")
        If respuesta IsNot Nothing Then
            clienteServidor.motivoCambioEstado = respuesta.Motivo
            clienteServidor.comentarioCambioEstado = respuesta.Comentario
        End If
        Return True
    End Function

#End Region

    Private Async Sub GuardarVendedores(ByVal param As Object)
        Await GuardarFichaComercialAsync()
    End Sub

    ''' <summary>
    ''' El botón Guardar de la ficha comercial (PUT Clientes/ClienteComercial). Nesto#644 (13): asíncrono de verdad (antes
    ''' hacía .Result en el hilo de la pantalla, que se queda bloqueada si hay que renovar el token); (11): si no se guarda
    ''' —la API lo rechaza o no se puede llegar a ella— se dice siempre en una ventana (antes, un fallo de red solo iba a
    ''' mensajeError, que esta ficha no enseña).
    ''' </summary>
    Friend Async Function GuardarFichaComercialAsync() As Task
        If clienteServidor Is Nothing OrElse clienteActivo Is Nothing Then
            dialogService.ShowError("Elija un cliente en la lista antes de guardar.")
            Return
        End If
        If ClaveCliente(clienteServidor) <> ClaveCliente(clienteActivo) Then
            dialogService.ShowError("Lo que se iba a guardar no es del cliente que está en pantalla: vuelva a elegirlo en la lista.")
            Return
        End If
        clienteServidor.estado = clienteActivo.estado
        clienteServidor.usuario = configuracion.usuario
        ' Nesto#644 (5 y 6): solo lo que ha cambiado el usuario; y nada si el guardado no haría lo que se ve en pantalla
        Dim previa As PeticionGuardadoFichaComercial = GuardadoFichaComercial.Preparar(clienteServidor, _comercialCargado,
            vendedorPorGrupo, estadoPeluqueria, configuracion.usuario)
        If previa.MotivoNoSePuede IsNot Nothing Then
            dialogService.ShowError(previa.MotivoNoSePuede)
            Return
        End If
        If Not previa.HayCambios Then
            dialogService.ShowNotification("Guardar", "No ha cambiado nada de la ficha: no hay nada que guardar.")
            Return
        End If

        Dim continuar As Boolean = False
        Dim p As New ParametrosDialogo From {
            {"message", "¿Desea guardar los cambios (vendedores, estado y país fiscal)?"}
        }
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              If r.Result = ResultadoBoton.OK Then
                                                                  continuar = True
                                                              End If
                                                          End Sub)
        If Not continuar Then
            Return
        End If

        Dim numeroCliente As String = clienteServidor.cliente?.Trim()
        ' Nesto#558: si el estado pasa a una baja (o sale de ella), motivo y comentario opcionales en el PUT
        PedirMotivoCambioEstadoSiHaceFalta()
        Dim peticion As PeticionGuardadoFichaComercial = GuardadoFichaComercial.Preparar(clienteServidor, _comercialCargado,
            vendedorPorGrupo, estadoPeluqueria, configuracion.usuario)
        Dim motivoNoGuardado As String = Nothing
        Dim motivoEstadoPeluqueria As String = Nothing
        Dim primerPutHecho As Boolean = False
        Try
            Using client As HttpClient = _clienteApiFactory.Crear()
                motivoNoGuardado = Await PonerFichaComercialAsync(client, peticion.Payload)
                primerPutHecho = motivoNoGuardado Is Nothing
                ' Nesto#644 (6): la API crea la fila de peluquería con el estado 0; con la fila ya creada, el estado elegido
                If primerPutHecho AndAlso peticion.PayloadEstadoPeluqueria IsNot Nothing Then
                    motivoEstadoPeluqueria = Await PonerFichaComercialAsync(client, peticion.PayloadEstadoPeluqueria)
                End If
            End Using
        Catch ex As Exception
            ' Nesto#644 (11): la red caída (HttpRequestException con el motivo dentro) también se dice
            Dim motivo As String = If(ex.InnerException?.Message, ex.Message)
            If primerPutHecho Then
                motivoEstadoPeluqueria = motivo
            Else
                motivoNoGuardado = motivo
            End If
        End Try

        If motivoNoGuardado IsNot Nothing Then
            mensajeError = MensajeFichaComercialNoGuardada(numeroCliente, motivoNoGuardado)
            dialogService.ShowError(mensajeError)
            Return
        End If

        Dim estadoPeluqueriaGuardado As Short = If(motivoEstadoPeluqueria Is Nothing, estadoPeluqueria, GuardadoFichaComercial.ESTADO_PELUQUERIA_AL_CREAR)
        _comercialCargado = ValoresComercialesFicha.Guardados(clienteServidor, _comercialCargado, vendedorPorGrupo, estadoPeluqueriaGuardado)
        ' Nesto#558: desde ahora, el estado guardado es el de partida para el siguiente cambio
        _estadoCargado = clienteActivo?.estado
        If clienteServidor IsNot Nothing Then
            clienteServidor.motivoCambioEstado = Nothing
            clienteServidor.comentarioCambioEstado = Nothing
        End If
        If motivoEstadoPeluqueria IsNot Nothing Then
            mensajeError = $"Se ha guardado el cliente {numeroCliente}, pero el estado de peluquería ha quedado en {GuardadoFichaComercial.ESTADO_PELUQUERIA_AL_CREAR} (no en {estadoPeluqueria}): {motivoEstadoPeluqueria} Vuelva a ponerlo y guarde otra vez."
            estadoPeluqueria = GuardadoFichaComercial.ESTADO_PELUQUERIA_AL_CREAR
            dialogService.ShowError(mensajeError)
            Return
        End If
        mensajeError = Nothing

        Dim c As New ParametrosDialogo From {
            {"message", "Se han guardado correctamente los cambios"}
        }
        dialogService.ShowDialog("NotificationDialog", c, Sub(r)

                                                          End Sub)
    End Function

    ''' <summary>Un PUT de Clientes/ClienteComercial: Nothing si se ha guardado; si no, el motivo que da la API, legible.</summary>
    Private Shared Async Function PonerFichaComercialAsync(client As HttpClient, payload As Newtonsoft.Json.Linq.JObject) As Task(Of String)
        Dim content As HttpContent = New StringContent(payload.ToString(), Encoding.UTF8, "application/json")
        Dim response As HttpResponseMessage = Await client.PutAsync("Clientes/ClienteComercial", content)
        If response.IsSuccessStatusCode Then
            Return Nothing
        End If
        Return HttpErrorHelper.ParsearErrorHttp(Await response.Content.ReadAsStringAsync())
    End Function

    ''' <summary>Nesto#644 (11): el aviso de que la ficha comercial NO se ha guardado, con el motivo.</summary>
    Friend Shared Function MensajeFichaComercialNoGuardada(numeroCliente As String, motivo As String) As String
        Return String.Format("No se ha guardado el cliente {0}: {1}", numeroCliente, If(String.IsNullOrWhiteSpace(motivo), "error desconocido.", motivo.Trim()))
    End Function

    Private _mostrarImagenesFacturas As Boolean = False
    Public Property MostrarImagenesFacturas As Boolean
        Get
            Return _mostrarImagenesFacturas
        End Get
        Set(value As Boolean)
            SetProperty(_mostrarImagenesFacturas, value)
        End Set
    End Property

    Private _descargarFacturasCommand As ICommand
    Public ReadOnly Property DescargarFacturasCommand() As ICommand
        Get
            If _descargarFacturasCommand Is Nothing Then
                _descargarFacturasCommand = ComandosReevaluables.Crear(AddressOf DescargarFacturas, AddressOf CanDescargarFacturas)
            End If
            Return _descargarFacturasCommand
        End Get
    End Property
    Private Function CanDescargarFacturas(ByVal param As Object) As Boolean
        Return FacturasSeleccionadas IsNot Nothing AndAlso FacturasSeleccionadas.Any()
    End Function
    Private Async Sub DescargarFacturas(ByVal param As Object)
        estaOcupado = True
        Try
            Dim np As IntPtr
            Dim unused1 = SHGetKnownFolderPath(New Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, np)
            Dim path As String = Marshal.PtrToStringUni(np)
            Marshal.FreeCoTaskMem(np)

            ' NestoAPI#453: si una factura falla, las demás se descargan igual, pero el motivo de
            ' las que no han salido se le dice al usuario. Antes se saltaban en silencio: ni PDF
            ' ni explicación, y quien lo sufría no tenía forma de saber que faltaba algo.
            Dim descargadas As Integer = 0
            Dim fallidas As New List(Of String)

            For Each fra In FacturasSeleccionadas
                Try
                    Dim factura As Byte() = Await CargarFactura(fra.Empresa, fra.Documento, MostrarImagenesFacturas)
                    If factura Is Nothing OrElse factura.Length = 0 Then
                        Throw New Exception("el servidor no ha devuelto el PDF")
                    End If

                    Using ms As New MemoryStream(factura)
                        Dim nombreArchivo As String = path + "\Cliente_" + fra.Cliente?.Trim() + "_" + fra.Documento?.Trim() + ".pdf"
                        Using file As New FileStream(nombreArchivo, FileMode.Create, FileAccess.Write)
                            ms.WriteTo(file)
                        End Using
                    End Using
                    descargadas += 1
                Catch ex As Exception
                    Dim motivo As String = If(ex.InnerException Is Nothing, ex.Message, ex.InnerException.Message)
                    fallidas.Add(fra.Documento?.Trim() & ": " & motivo)
                End Try
            Next

            If fallidas.Any() Then
                mensajeError = "No se han podido descargar " & fallidas.Count &
                               If(fallidas.Count = 1, " factura:", " facturas:") & vbCrLf &
                               String.Join(vbCrLf, fallidas)
            End If

            ' Abrimos la carpeta de descargas solo si hay algo que enseñar
            If descargadas > 0 Then
                Dim unused = Process.Start(New ProcessStartInfo(path) With {
                    .UseShellExecute = True
                })
            End If
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        Finally
            estaOcupado = False
        End Try


    End Sub

    Private _enviarFacturasPorCorreoCommand As ICommand
    ''' <summary>Nesto#259 (Manuel, 05/10/26): las facturas marcadas, en un solo correo, al correo escrito.</summary>
    Public ReadOnly Property EnviarFacturasPorCorreoCommand() As ICommand
        Get
            If _enviarFacturasPorCorreoCommand Is Nothing Then
                _enviarFacturasPorCorreoCommand = ComandosReevaluables.Crear(AddressOf OnEnviarFacturasPorCorreo, AddressOf CanDescargarFacturas)
            End If
            Return _enviarFacturasPorCorreoCommand
        End Get
    End Property
    Private Async Sub OnEnviarFacturasPorCorreo(ByVal param As Object)
        Await EnviarFacturasPorCorreoAsync()
    End Sub

    Public Async Function EnviarFacturasPorCorreoAsync() As Task
        If FacturasSeleccionadas Is Nothing OrElse Not FacturasSeleccionadas.Any() Then
            Return
        End If
        Dim correos As String = CorreoEnvioFacturas?.Trim()
        If String.IsNullOrWhiteSpace(correos) Then
            dialogService.ShowError("Escribe el correo al que quieres mandar las facturas.")
            Return
        End If
        Dim numeros As List(Of String) = FacturasSeleccionadas _
            .Select(Function(f) f.Documento?.Trim()) _
            .Where(Function(n) Not String.IsNullOrEmpty(n)) _
            .Distinct() _
            .ToList()
        Dim pregunta As String = If(numeros.Count = 1,
            $"¿Enviar la factura {numeros(0)} a {correos}?",
            $"¿Enviar {numeros.Count} facturas a {correos}?")
        If Not dialogService.ShowConfirmationAnswer("Enviar facturas por correo", pregunta) Then
            Return
        End If

        estaOcupado = True
        Try
            Dim empresa As String = FacturasSeleccionadas.First().Empresa?.Trim()
            Dim resultado As ResultadoEnvioFacturasCorreo = Await servicio.EnviarFacturasPorCorreo(empresa, numeros, correos)
            If resultado.Enviado Then
                dialogService.ShowNotification("Facturas enviadas", resultado.Mensaje)
            Else
                dialogService.ShowError(resultado.Mensaje)
            End If
        Catch ex As Exception
            dialogService.ShowError("No se han enviado las facturas: " & If(ex.InnerException?.Message, ex.Message))
        Finally
            estaOcupado = False
        End Try
    End Function

    Private _cargarPedidoCommand As ICommand
    Public ReadOnly Property CargarPedidoCommand() As ICommand
        Get
            If _cargarPedidoCommand Is Nothing Then
                _cargarPedidoCommand = ComandosReevaluables.Crear(AddressOf CargarPedido, AddressOf CanCargarPedido)
            End If
            Return _cargarPedidoCommand
        End Get
    End Property
    Private Function CanCargarPedido(ByVal param As Object) As Boolean
        Return ListaPedidos IsNot Nothing
    End Function
    Private Sub CargarPedido(ByVal param As Object)
        ' Nesto#423: el CommandParameter es el SelectedItem del DataGrid y puede no ser un
        ' ResumenPedido (Nothing si el doble clic cae en la cabecera o en el hueco del grid, o el
        ' NewItemPlaceholder). El late binding param.numero sobre eso revienta con NRE.
        Dim pedido = TryCast(param, PedidoVentaModel.ResumenPedido)
        If pedido Is Nothing Then
            Return
        End If
        abridorPedidos?.Abrir(empresaActual, pedido.numero)
    End Sub

    Public ReadOnly Property AbrirPedidoAction As Action(Of String, Integer)
        Get
            Return Sub(empresa, numeroPedido) abridorPedidos?.Abrir(empresa, numeroPedido)
        End Get
    End Property

    Private _reclamarDeudaCommand As RelayCommand
    Public Property ReclamarDeudaCommand As RelayCommand
        Get
            Return _reclamarDeudaCommand
        End Get
        Private Set(value As RelayCommand)
            Dim unused = SetProperty(_reclamarDeudaCommand, value)
        End Set
    End Property
    ' NestoAPI#609: antes de crear el enlace se propone la corrección del concepto (lo ve el cliente y acaba en
    ' su extracto). Si la API no la tiene o falla, se sigue con lo escrito sin avisar.
    Private _revisorConceptoPago As ControlesUsuario.Services.IRevisorConceptoPago
    Friend Property RevisorConceptoPago As ControlesUsuario.Services.IRevisorConceptoPago
        Get
            If _revisorConceptoPago Is Nothing AndAlso _clienteApiFactory IsNot Nothing AndAlso dialogService IsNot Nothing Then
                _revisorConceptoPago = New ControlesUsuario.Services.RevisorConceptoPago(
                    New ControlesUsuario.Services.ServicioRevisionConcepto(_clienteApiFactory), dialogService)
            End If
            Return _revisorConceptoPago
        End Get
        Set(value As ControlesUsuario.Services.IRevisorConceptoPago)
            _revisorConceptoPago = value
        End Set
    End Property

    ''' <summary>Nesto#611: título del cuadro que pide el concepto cuando el enlace no liquida efectos.</summary>
    Public Const TITULO_PEDIR_CONCEPTO_PAGO As String = "Concepto del pago"
    ''' <summary>Nesto#611: qué se pide y por qué, con los mismos ejemplos que da la API.</summary>
    Public Const TEXTO_PEDIR_CONCEPTO_PAGO As String =
        "El enlace no liquida efectos concretos (no hay ninguno marcado o su suma no coincide con el importe), así que el " &
        "concepto es lo único que dirá qué ha pagado el cliente: se contabiliza tal cual en su extracto." & vbCrLf & vbCrLf &
        "Escriba un concepto que identifique el pago, por ejemplo «Pago pedido 123456» o «Pago señal curso quiromasaje»."

    ''' <summary>Nesto#611: como la API (FormateadorConcepto.EsGenericoOVacio): vacío o el asunto por defecto no identifican el pago.</summary>
    Friend Shared Function EsConceptoGenericoOVacio(concepto As String) As Boolean
        Return String.IsNullOrWhiteSpace(concepto) OrElse
            String.Equals(concepto.Trim(), ASUNTO_PAGO_POR_DEFECTO, StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>
    ''' Nesto#611: el mensaje de una validación de la API (400), tal cual: el "Message" del JSON de BadRequest o, si no
    ''' viene así, el cuerpo entero.
    ''' </summary>
    Friend Shared Function MensajeValidacionApi(cuerpo As String) As String
        If String.IsNullOrWhiteSpace(cuerpo) Then
            Return "La solicitud de pago no es válida."
        End If
        Try
            Dim json = Newtonsoft.Json.Linq.JToken.Parse(cuerpo)
            If json.Type = Newtonsoft.Json.Linq.JTokenType.String Then
                Return json.ToString().Trim()
            End If
            Dim objeto = TryCast(json, Newtonsoft.Json.Linq.JObject)
            Dim mensaje As String = objeto?("Message")?.ToString()
            If Not String.IsNullOrWhiteSpace(mensaje) Then
                Return mensaje.Trim()
            End If
        Catch ex As JsonException
            ' No es JSON: el texto tal cual
        End Try
        Return cuerpo.Trim()
    End Function

    ''' <summary>Nesto#611: la API ha rechazado la solicitud (400): es una validación, no un error de la aplicación.</summary>
    Private Class ValidacionPagoException
        Inherits Exception
        Public Sub New(mensaje As String)
            MyBase.New(mensaje)
        End Sub
    End Class

    Private Async Sub OnReclamarDeuda()
        Await ReclamarDeudaAsync()
    End Sub

    Friend Async Function ReclamarDeudaAsync() As Task
        Dim errorReclamacion As Exception = Nothing
        Dim errorValidacion As String = Nothing
        Try
            ' Siempre crear PagoTPV con efectos (auditoría + URL propia)
            Dim efectosSeleccionados = ListaDeudas?.Where(Function(l) l.Seleccionada).ToList()

            Dim efectos As New List(Of Object)
            If efectosSeleccionados IsNot Nothing Then
                For Each efecto In efectosSeleccionados
                    efectos.Add(New With {
                        .ExtractoClienteId = efecto.Id,
                        .Importe = efecto.ImportePendiente,
                        .Documento = efecto.Documento?.Trim(),
                        .Efecto = efecto.Efecto?.Trim(),
                        .Contacto = efecto.Contacto?.Trim(),
                        .Vendedor = efecto.Vendedor?.Trim(),
                        .FormaVenta = efecto.FormaVenta?.Trim(),
                        .Delegacion = efecto.Delegacion?.Trim(),
                        .TipoApunte = efecto.Tipo?.Trim()
                    })
                Next
            End If

            ' Nesto#438: los efectos solo viajan si su suma coincide con el importe a cobrar.
            ' Si el usuario cambió el importe a mano (o la selección incluye abonos y la suma
            ' no cuadra), el enlace va SIN efectos (importe libre): mandar lista e importe
            ' incoherentes era un 400 seguro del servidor ("la suma de los efectos no
            ' coincide con el importe total") sin pista de qué corregir.
            Dim sumaEfectos As Decimal = If(efectosSeleccionados Is Nothing, 0D,
                efectosSeleccionados.Sum(Function(l) l.ImportePendiente))
            If efectos.Any() AndAlso sumaEfectos <> ImporteReclamarDeuda Then
                efectos.Clear()
            End If

            ' Nesto#611 (Reina, 03/11/26: cinco veces «Indique un concepto…»): sin efectos que liquidar la API exige un
            ' concepto real; se pide aquí antes de llamarla. Pasa también con efectos marcados cuyo importe no cuadra
            ' (Nesto#438: van sin efectos) y el asunto por defecto en el cuadro. Si no se escribe nada, no se crea el enlace.
            If Not efectos.Any() AndAlso EsConceptoGenericoOVacio(AsuntoReclamarDeuda) Then
                Dim concepto As String = dialogService.GetText(TITULO_PEDIR_CONCEPTO_PAGO, TEXTO_PEDIR_CONCEPTO_PAGO)
                If EsConceptoGenericoOVacio(concepto) Then
                    Return
                End If
                AsuntoReclamarDeuda = concepto.Trim()
            End If

            ' NestoAPI#609: el concepto elegido (corregido o el mío) queda en el cuadro y es el que se manda.
            If RevisorConceptoPago IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(AsuntoReclamarDeuda) AndAlso
                    AsuntoReclamarDeuda.Trim() <> ASUNTO_PAGO_POR_DEFECTO Then
                AsuntoReclamarDeuda = Await RevisorConceptoPago.ElegirConcepto(AsuntoReclamarDeuda,
                    clienteActivo?.empresa?.Trim(), clienteActivo?.cliente?.Trim())
            End If

            Using client As HttpClient = _clienteApiFactory.Crear()

            If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim solicitud = New With {
                .Empresa = clienteActivo?.empresa.Trim(),
                .Cliente = clienteActivo?.cliente.Trim(),
                .Contacto = clienteActivo?.contacto?.Trim(),
                .Importe = ImporteReclamarDeuda,
                .Descripcion = AsuntoReclamarDeuda,
                .Correo = CorreoReclamarDeuda,
                .Movil = MovilReclamarDeuda,
                .Efectos = efectos
            }

            Try
                Dim solicitudJson As String = JsonConvert.SerializeObject(solicitud)
                Dim content As New StringContent(solicitudJson, Encoding.UTF8, "application/json")
                Dim response = Await client.PostAsync("Pagos", content)

                If response.IsSuccessStatusCode Then
                    Dim respuesta = Await response.Content.ReadAsStringAsync()
                    Dim resultado = JsonConvert.DeserializeObject(Of Dictionary(Of String, Object))(respuesta)

                    If MotorPagosSeleccionado = "NestoPago" Then
                        ' Mostrar URL de la página propia
                        If resultado.ContainsKey("UrlPaginaPago") Then
                            EnlaceReclamarDeuda = resultado("UrlPaginaPago").ToString()
                        End If
                    Else
                        ' Motor Paygold: enviar también por P2F para que el cliente reciba SMS/correo
                        Dim reclamacion As New ReclamacionDeuda With {
                            .Cliente = clienteActivo?.cliente.Trim(),
                            .Asunto = AsuntoReclamarDeuda,
                            .Correo = CorreoReclamarDeuda,
                            .Importe = ImporteReclamarDeuda,
                            .Movil = MovilReclamarDeuda,
                            .Nombre = NombreReclamarDeuda,
                            .Direccion = clienteActivo.direccion,
                            .TextoSMS = "Este es un mensaje de @COMERCIO@. Puede pagar el importe pendiente de @IMPORTE@ @MONEDA@ aquí: @URL@"
                        }
                        Dim reclamacionJson As String = JsonConvert.SerializeObject(reclamacion)
                        Dim contentP2F As New StringContent(reclamacionJson, Encoding.UTF8, "application/json")
                        Dim responseP2F = Await client.PostAsync("ReclamacionDeuda", contentP2F)

                        If responseP2F.IsSuccessStatusCode Then
                            Dim respuestaP2F = Await responseP2F.Content.ReadAsStringAsync()
                            reclamacion = JsonConvert.DeserializeObject(Of ReclamacionDeuda)(respuestaP2F)
                            If reclamacion.TramitadoOK Then
                                EnlaceReclamarDeuda = reclamacion.Enlace
                            End If
                        End If
                    End If
                Else
                    Dim errorMsg = Await response.Content.ReadAsStringAsync()
                    If response.StatusCode = Net.HttpStatusCode.BadRequest Then
                        ' Nesto#611: validación de la API (concepto, importe...): el mensaje tal cual, sin prefijos
                        Throw New ValidacionPagoException(MensajeValidacionApi(errorMsg))
                    End If
                    Throw New Exception($"Error al crear enlace de pago: {errorMsg}")
                End If

            Catch ex As ValidacionPagoException
                Throw
            Catch ex As Exception
                Throw New Exception("No se ha podido procesar el enlace de pago: " & ex.Message)
            End Try
            End Using
        Catch ex As ValidacionPagoException
            ' Nesto#611: no es un error de la aplicación: ni ELMAH ni prefijo técnico
            errorValidacion = ex.Message
        Catch ex As Exception
            ' En vez de crashear la aplicación (Async Sub sin manejo => excepción no observada),
            ' capturamos el error para informar al usuario y registrarlo en ELMAH.
            ' (VB no permite Await dentro de un Catch, por eso se trata fuera del Try).
            errorReclamacion = ex
        End Try

        If errorValidacion IsNot Nothing Then
            dialogService.ShowError(errorValidacion)
            Return
        End If

        If errorReclamacion IsNot Nothing Then
            Try
                If servicioErrores IsNot Nothing Then
                    Await servicioErrores.RegistrarErrorAsync(errorReclamacion, "ClientesViewModel.OnReclamarDeuda")
                End If
            Catch
                ' El registro de errores nunca debe impedir mostrar el aviso al usuario.
            End Try

            Dim mensaje As String = If(errorReclamacion.InnerException IsNot Nothing, errorReclamacion.InnerException.Message, errorReclamacion.Message)
            Dim p As New ParametrosDialogo From {
                {"message", "No se ha podido reclamar la deuda." & vbCrLf & vbCrLf & mensaje}
            }
            dialogService.ShowDialog("NotificationDialog", p, Sub(r)
                                                              End Sub)
        End If
    End Function

    Public Property AbrirEnlaceReclamacionCommand As RelayCommand
    Private Function CanAbrirEnlaceReclamacion() As Boolean
        Return Not String.IsNullOrEmpty(EnlaceReclamarDeuda)
    End Function
    Private Sub OnAbrirEnlaceReclamacion()
        Dim psi As New System.Diagnostics.ProcessStartInfo(EnlaceReclamarDeuda) With {
            .UseShellExecute = True
        }
        Dim unused = System.Diagnostics.Process.Start(psi)
    End Sub

#Region "Cobro de un pedido desde la pestaña Pedidos (sugerencia 417)"
    ' Sugerencia 417 de Novedades (Paloma, 29/09/26): en la pestaña Pedidos, al seleccionar un pedido pendiente,
    ' mandar el enlace de pago (o copiar los datos de la transferencia si es prepago) sin abrir el pedido. Usa lo
    ' mismo que el detalle del pedido (IPedidoVentaService.EnviarCobroTarjeta y LeerDatosTransferencia), con el
    ' total del pedido recién leído de la API y el correo y el móvil del cliente (los de la pestaña Deudas).

    Private _servicioPedidos As IPedidoVentaService
    ''' <summary>Llega por el constructor; los tests lo asignan.</summary>
    Public Property ServicioPedidos As IPedidoVentaService
        Get
            Return _servicioPedidos
        End Get
        Set(value As IPedidoVentaService)
            _servicioPedidos = value
        End Set
    End Property

    ''' <summary>Dónde se copian el enlace y los datos de transferencia. Se cambia en los tests.</summary>
    Public Property PortapapelesCobroPedido As IPortapapelesTexto = New PortapapelesTextoWpf()

    Private _pedidoSeleccionadoCobro As ResumenPedido
    Public Property PedidoSeleccionadoCobro As ResumenPedido
        Get
            Return _pedidoSeleccionadoCobro
        End Get
        Set(value As ResumenPedido)
            If SetProperty(_pedidoSeleccionadoCobro, value) Then
                Dim unused = CargarPedidoParaCobroAsync(value)
            End If
        End Set
    End Property

    Private _pedidoParaCobro As PedidoVentaDTO
    ''' <summary>El pedido seleccionado, leído entero de la API (Nothing mientras carga o si no hay).</summary>
    Public Property PedidoParaCobro As PedidoVentaDTO
        Get
            Return _pedidoParaCobro
        End Get
        Private Set(value As PedidoVentaDTO)
            Dim unused = SetProperty(_pedidoParaCobro, value)
            OnPropertyChanged(NameOf(ImporteCobroPedido))
            OnPropertyChanged(NameOf(PedidoCobroPendiente))
            OnPropertyChanged(NameOf(PedidoCobroEsPrepagoPorTransferencia))
            OnPropertyChanged(NameOf(TextoCobroPedido))
            EnviarEnlacePagoPedidoCommand.NotifyCanExecuteChanged()
            CopiarDatosTransferenciaPedidoCommand.NotifyCanExecuteChanged()
        End Set
    End Property

    ''' <summary>Total del pedido con IVA: lo mismo que propone el detalle del pedido para el cobro con tarjeta.</summary>
    Public ReadOnly Property ImporteCobroPedido As Decimal
        Get
            Return If(PedidoParaCobro Is Nothing, 0D, PedidoParaCobro.Total)
        End Get
    End Property

    ''' <summary>Pendiente = le queda alguna línea sin facturar. Lo ya facturado se cobra desde Deudas.</summary>
    Public ReadOnly Property PedidoCobroPendiente As Boolean
        Get
            Return PedidoParaCobro IsNot Nothing AndAlso PedidoParaCobro.Lineas IsNot Nothing AndAlso
                PedidoParaCobro.Lineas.Any(Function(l) Not l.estaFacturada)
        End Get
    End Property

    Public ReadOnly Property PedidoCobroEsPrepagoPorTransferencia As Boolean
        Get
            Return PedidoParaCobro IsNot Nothing AndAlso
                DatosTransferenciaPedido.EsPrepagoPorTransferencia(PedidoParaCobro.formaPago, PedidoParaCobro.plazosPago)
        End Get
    End Property

    Public ReadOnly Property TextoCobroPedido As String
        Get
            If PedidoSeleccionadoCobro Is Nothing Then
                Return "Selecciona un pedido para mandar el enlace de pago."
            End If
            If PedidoParaCobro Is Nothing Then
                Return $"Cargando el pedido {PedidoSeleccionadoCobro.numero}…"
            End If
            If Not PedidoCobroPendiente Then
                Return $"El pedido {PedidoParaCobro.numero} ya está facturado: se cobra desde la pestaña Deudas."
            End If
            Return $"Pedido {PedidoParaCobro.numero}: {ImporteCobroPedido.ToString("C", CultureInfo.GetCultureInfo("es-ES"))} (IVA incluido)"
        End Get
    End Property

    Friend Async Function CargarPedidoParaCobroAsync(resumen As ResumenPedido) As Task
        PedidoParaCobro = Nothing
        OnPropertyChanged(NameOf(TextoCobroPedido))
        If resumen Is Nothing OrElse resumen.numero <= 0 OrElse ServicioPedidos Is Nothing Then
            Return
        End If
        Try
            Dim empresa As String = If(String.IsNullOrWhiteSpace(resumen.empresa), empresaActual, resumen.empresa)
            Dim leido As PedidoVentaDTO = Await ServicioPedidos.cargarPedido(empresa, resumen.numero)
            ' Si mientras cargaba se ha seleccionado otro, se descarta: nunca se cobra el pedido equivocado
            If PedidoSeleccionadoCobro Is resumen Then
                PedidoParaCobro = leido
            End If
        Catch ex As Exception
            dialogService.ShowError($"No se ha podido leer el pedido {resumen.numero}: {ex.Message}")
        End Try
    End Function

    Private _enviarEnlacePagoPedidoCommand As AsyncRelayCommand
    Public ReadOnly Property EnviarEnlacePagoPedidoCommand As AsyncRelayCommand
        Get
            If _enviarEnlacePagoPedidoCommand Is Nothing Then
                _enviarEnlacePagoPedidoCommand = New AsyncRelayCommand(AddressOf EnviarEnlacePagoPedidoAsync, AddressOf CanEnviarEnlacePagoPedido)
            End If
            Return _enviarEnlacePagoPedidoCommand
        End Get
    End Property

    Friend Function CanEnviarEnlacePagoPedido() As Boolean
        Return PedidoCobroPendiente AndAlso ImporteCobroPedido > 0 AndAlso PedidoCobroEsDelClienteEnPantalla() AndAlso
            (Not String.IsNullOrWhiteSpace(CorreoReclamarDeuda) OrElse Not String.IsNullOrWhiteSpace(MovilReclamarDeuda))
    End Function

    ''' <summary>
    ''' Nesto#644 (1): el correo y el móvil son los del cliente en pantalla, así que el pedido a cobrar tiene que ser suyo:
    ''' nunca el enlace de un pedido al correo o al móvil de otro cliente.
    ''' </summary>
    Private Function PedidoCobroEsDelClienteEnPantalla() As Boolean
        If PedidoParaCobro Is Nothing OrElse clienteActivo Is Nothing Then
            Return True
        End If
        Return String.Equals(PedidoParaCobro.cliente?.Trim(), clienteActivo.cliente?.Trim(), StringComparison.OrdinalIgnoreCase)
    End Function

    Friend Async Function EnviarEnlacePagoPedidoAsync() As Task
        If Not CanEnviarEnlacePagoPedido() Then
            Return
        End If
        Dim pedido As PedidoVentaDTO = PedidoParaCobro
        Dim destino As String = String.Join(" y ", {CorreoReclamarDeuda, MovilReclamarDeuda}.Where(Function(d) Not String.IsNullOrWhiteSpace(d)))
        If Not dialogService.ShowConfirmationAnswer("Enlace de pago",
                $"¿Mandar a {destino} el enlace de pago del pedido {pedido.numero} por {ImporteCobroPedido.ToString("C", CultureInfo.GetCultureInfo("es-ES"))}?") Then
            Return
        End If
        Try
            Dim enlace As String = Await ServicioPedidos.EnviarCobroTarjeta(CorreoReclamarDeuda, MovilReclamarDeuda, ImporteCobroPedido,
                pedido.numero.ToString(), pedido.empresa, pedido.cliente)
            If Not String.IsNullOrEmpty(enlace) Then
                PortapapelesCobroPedido.CopiarTexto(enlace)
                dialogService.ShowNotification("Enlace de pago", "Enlace de pago enviado y copiado al portapapeles:" & vbCrLf & enlace)
            Else
                dialogService.ShowNotification("Enlace de pago", "Enlace de pago enviado correctamente.")
            End If
        Catch ex As Exception
            dialogService.ShowError("No se ha podido crear el enlace de pago: " & ex.Message)
        End Try
    End Function

    Private _copiarDatosTransferenciaPedidoCommand As AsyncRelayCommand
    Public ReadOnly Property CopiarDatosTransferenciaPedidoCommand As AsyncRelayCommand
        Get
            If _copiarDatosTransferenciaPedidoCommand Is Nothing Then
                _copiarDatosTransferenciaPedidoCommand = New AsyncRelayCommand(AddressOf CopiarDatosTransferenciaPedidoAsync,
                    Function() PedidoCobroPendiente AndAlso PedidoCobroEsPrepagoPorTransferencia)
            End If
            Return _copiarDatosTransferenciaPedidoCommand
        End Get
    End Property

    Friend Async Function CopiarDatosTransferenciaPedidoAsync() As Task
        Dim pedido As PedidoVentaDTO = PedidoParaCobro
        If pedido Is Nothing OrElse Not PedidoCobroEsPrepagoPorTransferencia Then
            Return
        End If
        Try
            Dim datos As DatosTransferenciaPedidoModel = Await ServicioPedidos.LeerDatosTransferencia(pedido.empresa, pedido.numero)
            If datos Is Nothing OrElse String.IsNullOrWhiteSpace(datos.Texto) Then
                dialogService.ShowError("No se han podido leer los datos de transferencia del pedido.")
                Return
            End If
            PortapapelesCobroPedido.CopiarTexto(datos.Texto)
            dialogService.ShowNotification(DatosTransferenciaPedido.TITULO, DatosTransferenciaPedido.MENSAJE_COPIADOS & vbCrLf & vbCrLf & datos.Texto)
        Catch ex As Exception
            dialogService.ShowError("No se han podido copiar los datos de transferencia: " & ex.Message)
        End Try
    End Function
#End Region

    Public Property ConfirmarReclamarDeudaCommand As RelayCommand
    Private Function CanConfirmarReclamarDeuda() As Boolean
        ' NestoAPI#295: sin efectos seleccionados hace falta un concepto real (el genérico se
        ' contabilizaría en el extracto y no se sabría qué pagó el cliente). El servidor lo
        ' valida igualmente (400).
        Dim hayEfectosSeleccionados = ListaDeudas IsNot Nothing AndAlso ListaDeudas.Any(Function(l) l.Seleccionada)
        Dim conceptoValido = hayEfectosSeleccionados OrElse
            (Not String.IsNullOrWhiteSpace(AsuntoReclamarDeuda) AndAlso AsuntoReclamarDeuda.Trim() <> ASUNTO_PAGO_POR_DEFECTO)
        Return conceptoValido AndAlso ImporteReclamarDeuda >= 1 AndAlso (Not IsNothing(CorreoReclamarDeuda) OrElse Not IsNothing(MovilReclamarDeuda))
    End Function
    Private Sub OnConfirmarReclamarDeuda()
        Dim p As New ParametrosDialogo From {
            {"message", "¿Desea reclamar la deuda?"}
        }
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              If r.Result = ResultadoBoton.OK Then
                                                                  ReclamarDeudaCommand.Execute(Nothing)
                                                              End If
                                                          End Sub)
    End Sub

    Public Property GuardarEfectoDeudaCommand As RelayCommand
    Private Function CanGuardarEfectoDeuda() As Boolean
        Return Not IsNothing(DeudaSeleccionada) AndAlso EsUsuarioAdministracion AndAlso Not String.IsNullOrWhiteSpace(MotivoCambioEstado)
    End Function
    Private Async Sub OnGuardarEfectoDeuda()
        Dim p As New ParametrosDialogo
        Dim confirmacion As Boolean
        p.Add("message", "¿Desea guardar los cambios?")
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              confirmacion = r.Result = ResultadoBoton.OK
                                                          End Sub)
        If Not confirmacion Then
            Return
        End If

        Try
            Dim seguimientoMotivo As New SeguimientoClienteDTO With {
                .Empresa = DeudaSeleccionada.Empresa,
                .Cliente = DeudaSeleccionada.Cliente,
                .Contacto = DeudaSeleccionada.Contacto,
                .Tipo = Constantes.Rapports.Tipos.TIPO_VISITA_TELEFONICA,
                .Estado = Constantes.Rapports.Estados.GESTION_ADMINISTRATIVA,
                .Comentarios = $"{configuracion.usuario.Substring(configuracion.usuario.IndexOf("\") + 1).Trim()} cambió el estado del extracto de cliente con nº orden {DeudaSeleccionada.Id} a estado {If(String.IsNullOrEmpty(DeudaSeleccionada.Estado), "en blanco", DeudaSeleccionada.Estado.ToUpper)} dando como motivo: {MotivoCambioEstado.Trim()}",
                .Fecha = Date.Now,
                .NumOrdenExtracto = DeudaSeleccionada.Id,
                .Usuario = configuracion.usuario
            }
            Dim unused = Await servicioRapports.crearRapport(seguimientoMotivo)
            DeudaSeleccionada.Usuario = configuracion.usuario
            Await servicio.ModificarExtractoCliente(DeudaSeleccionada)
            dialogService.ShowNotification("Efecto modificado correctamente")
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        End Try
    End Sub


    Private Async Function CargarFactura(empresa As String, numeroFactura As String, Optional mostrarImagenes As Boolean = False) As Task(Of Byte())
        If IsNothing(clienteActivo) Then
            Return Nothing
        End If


        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim response As HttpResponseMessage
            Dim respuesta As Byte()

            Try
                If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                Dim urlConsulta As String = "Facturas"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&numeroFactura=" + numeroFactura
                urlConsulta += "&mostrarImagenes=" + mostrarImagenes.ToString().ToLower()


                response = Await client.GetAsync(urlConsulta)

                If Not response.IsSuccessStatusCode Then
                    ' NestoAPI#453: el servidor manda el motivo real en el cuerpo (texto plano en
                    ' los BadRequest de FacturasController, JSON en el resto). Antes esto devolvía
                    ' Nothing y el que llamaba se saltaba la factura EN SILENCIO: el usuario se
                    ' quedaba sin PDF y sin saber por qué.
                    Dim cuerpo As String = Await response.Content.ReadAsStringAsync()
                    Throw New Exception(HttpErrorHelper.ParsearErrorHttp(cuerpo))
                End If

                respuesta = Await response.Content.ReadAsByteArrayAsync()

            Catch ex As HttpRequestException
                Throw New Exception("No se ha podido conectar con el servidor para descargar la factura " &
                                    numeroFactura, ex)
            Catch ex As System.Threading.Tasks.TaskCanceledException
                Throw New Exception("El servidor ha tardado demasiado en generar la factura " &
                                    numeroFactura, ex)
            End Try

            Return respuesta
        End Using


    End Function

    Private Async Sub CargarFacturas()
        If IsNothing(clienteActivo) OrElse (ListaFacturas IsNot Nothing AndAlso clienteActivo.cliente?.Trim() = ListaFacturas.FirstOrDefault()?.Cliente) Then
            Return
        End If
        Dim testigo As Integer = TestigoFicha() ' Nesto#644 (2)


        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "ExtractosCliente"
                urlConsulta += "?empresa=" + clienteActivo.empresa
                urlConsulta += "&cliente=" + clienteActivo.cliente
                urlConsulta += "&tipoApunte=1"
                If EsUsuarioAdministracion Then
                    urlConsulta += "&fechaDesde=" + Date.Today.AddMonths(-72).ToString("s")
                Else
                    urlConsulta += "&fechaDesde=" + Date.Today.AddMonths(-6).ToString("s")
                End If
                urlConsulta += "&fechaHasta=" + Date.Today.ToString("s")

                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido cargar la lista de facturas desde el servidor")
            Finally

            End Try

            If Not EsFichaActual(testigo) Then
                Return ' Nesto#644 (2): ya es otro cliente
            End If
            ListaFacturas = JsonConvert.DeserializeObject(Of ObservableCollection(Of ExtractoClienteDTO))(respuesta)
        End Using


    End Sub

    Private Async Sub CargarPedidos()
        If IsNothing(clienteActivo) OrElse (ListaPedidos IsNot Nothing AndAlso clienteActivo.cliente?.Trim() = ListaPedidos.FirstOrDefault()?.cliente) Then
            Return
        End If
        Dim testigo As Integer = TestigoFicha() ' Nesto#644 (2)


        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "PedidosVenta"
                Dim mainViewModel As New MainViewModel
                Dim permitirTodosClientes As String = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PermitirVerClientesTodosLosVendedores)
                If permitirTodosClientes.Trim <> "1" Then
                    urlConsulta += "?vendedor=" + Await mainViewModel.leerParametro(empresaActual, "Vendedor")
                Else
                    urlConsulta += "?vendedor="
                End If
                urlConsulta += "&cliente=" + clienteActivo.cliente

                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = String.Empty
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido cargar la lista de pedidos desde el servidor")
            Finally

            End Try

            If Not EsFichaActual(testigo) Then
                Return ' Nesto#644 (2): ya es otro cliente
            End If
            ListaPedidos = JsonConvert.DeserializeObject(Of ObservableCollection(Of ResumenPedido))(respuesta)
        End Using


    End Sub

    Private Async Sub CargarDeudas()
        If IsNothing(clienteActivo) OrElse (ListaDeudas IsNot Nothing AndAlso clienteActivo.cliente?.Trim() = ListaDeudas.FirstOrDefault()?.Cliente) Then
            Return
        End If
        Dim testigo As Integer = TestigoFicha() ' Nesto#644 (2)

        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim response As HttpResponseMessage
            Dim respuesta As String = String.Empty

            Try
                Dim urlConsulta As String = "ExtractosCliente"
                urlConsulta += "?cliente=" + clienteActivo.cliente

                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = String.Empty
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido cargar la lista de deudas desde el servidor")
            Finally

            End Try
            If Not EsFichaActual(testigo) Then
                Return ' Nesto#644 (2): ya es otro cliente; sus efectos no pueden acabar en la reclamación del nuevo
            End If
            Dim lista = JsonConvert.DeserializeObject(Of ObservableCollection(Of ExtractoClienteDTO))(respuesta)
            For Each l In lista
                l.Seleccionada = l.Vencimiento < Date.Today OrElse IsNothing(l.Vencimiento)
                AddHandler l.PropertyChanged, New PropertyChangedEventHandler(AddressOf LineaDeudaPropertyChangedEventHandler)
            Next
            ListaDeudas = New ObservableCollection(Of ExtractoClienteDTO)(lista.OrderBy(Function(l) l.Fecha))
            ImporteReclamarDeuda = SumaDeudasSeleccionadas
            ActualizarAsuntoPorDefecto() ' NestoAPI#295: las vencidas vienen preseleccionadas
        End Using

        RellenarContactoReclamacion(clienteActivo)

        CargarHistorialPagos()

    End Sub

    ''' <summary>
    ''' El correo y el móvil a los que se manda la reclamación de deuda o el enlace de pago de un pedido: los del cliente
    ''' <paramref name="cliente"/> (Nothing = ninguno). Nesto#644 (1): antes solo se ponían al cargar las deudas, y el cobro de
    ''' un pedido usaba los del cliente anterior; ahora también al cambiar de cliente (<see cref="ReiniciarDatosDelCliente"/>).
    ''' </summary>
    Private Sub RellenarContactoReclamacion(cliente As ClienteJson)
        If cliente Is Nothing Then
            CorreoReclamarDeuda = Nothing
            MovilReclamarDeuda = Nothing
            Return
        End If
        ' Nesto#340 (1C.8, slice 4): las personas de contacto vienen en la ficha de la API con su
        ' Cargo real; se mapean a la entidad-contenedor que espera CorreoCliente (mismo patrón que
        ' PlantillaVenta, sin consultar EF).
        Dim personasContacto As New List(Of PersonasContactoCliente)
        If Not IsNothing(cliente.PersonasContacto) Then
            For Each persona In cliente.PersonasContacto
                personasContacto.Add(New PersonasContactoCliente With {
                    .Cargo = If(persona.Cargo, 0),
                    .CorreoElectrónico = persona.CorreoElectronico
                })
            Next
        End If
        Dim correo As New CorreoCliente(personasContacto)
        CorreoReclamarDeuda = correo.CorreoAgencia ' TODO: desarrollar correo deuda
        Dim telefono As New Telefono(cliente.telefono)
        MovilReclamarDeuda = telefono.MovilUnico
    End Sub

    ''' <summary>
    ''' Carga el histórico de enlaces de pago NestoPago del cliente actual.
    ''' Issue Nesto#322.
    ''' </summary>
    Private Async Sub CargarHistorialPagos()
        If IsNothing(clienteActivo) Then
            Return
        End If
        Dim testigo As Integer = TestigoFicha() ' Nesto#644 (2)

        Using client As HttpClient = _clienteApiFactory.Crear()
            Try
                If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Return
                End If
                If Not EsFichaActual(testigo) Then
                    Return
                End If

                Dim empresa = clienteActivo.empresa?.Trim()
                Dim cliente = clienteActivo.cliente?.Trim()
                Dim response = Await client.GetAsync($"Pagos/Cliente/{empresa}/{cliente}")

                If response.IsSuccessStatusCode Then
                    Dim respuesta = Await response.Content.ReadAsStringAsync()
                    If Not EsFichaActual(testigo) Then
                        Return ' ya es otro cliente
                    End If
                    Dim pagos = JsonConvert.DeserializeObject(Of List(Of PagoTPVDTO))(respuesta)
                    HistorialPagos = New ObservableCollection(Of PagoTPVDTO)(pagos)
                End If
            Catch
                ' Fail-safe: no bloquear la carga de deudas si falla el historial
            End Try
        End Using
    End Sub

    Private Sub LineaDeudaPropertyChangedEventHandler(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = "Seleccionada" Then
            ImporteReclamarDeuda = SumaDeudasSeleccionadas
            ActualizarAsuntoPorDefecto()
        End If
    End Sub


#End Region

#Region "Funciones"
    Private Async Sub inicializarListaClientesVendedor(filtro As String)
        'If vendedor <> "" Then
        '    If esVendedorDeFamilias Then
        '        Return New ObservableCollection(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = empresaActual And c.Estado >= 0 And listaCodigosPostalesVendedor.Contains(c.CodPostal) Order By c.CodPostal, c.Dirección)
        '    Else
        '        Return New ObservableCollection(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = empresaActual And c.Vendedor = vendedor And c.Estado >= 0 Order By c.CodPostal, c.Dirección)
        '    End If
        'Else
        '    Return New ObservableCollection(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = empresaActual And c.Estado >= 0)
        'End If
        If IsNothing(filtro) OrElse (filtro.Length < 4 AndAlso Not IsNumeric(filtro)) Then
            'listaClientesVendedor = Nothing
            ListaClientesFiltrable.ListaOriginal = Nothing
            Return
        End If

        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim response As HttpResponseMessage


            Try
                estaOcupado = True
                If Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PermitirVerClientesTodosLosVendedores) Then
                    response = Await client.GetAsync("Clientes?empresa=1&filtro=" + filtro)
                Else
                    response = Await client.GetAsync("Clientes?empresa=1&vendedor=" + vendedor + "&filtro=" + filtro)
                End If


                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Dim coleccionJson = JsonConvert.DeserializeObject(Of ObservableCollection(Of ClienteJson))(cadenaJson)
                    ListaClientesFiltrable.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(coleccionJson)
                Else
                    mensajeError = "Se ha producido un error al cargar los clientes"
                End If
            Catch ex As Exception
                mensajeError = ex.Message
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    ' Nesto#646: lo que esta ficha hacía dentro de cargarDatos y del setter de clienteActivo.
    Protected Overrides Async Function CargarDatosAntesDeElegirClienteAsync(mainViewModel As MainViewModel, empresaDefecto As String) As Task
        vendedor = Await mainViewModel.leerParametro(empresaDefecto, "Vendedor")
        If Not IsNothing(configuracion) Then
            EsUsuarioAdministracion = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
            OnPropertyChanged(NameOf(PuedeElegirMotorPagos))
            ' Nesto#458: mismos grupos "de oficina" que el servidor no restringe
            EsOficinaComercial = EsUsuarioAdministracion OrElse
                configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
                configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA)
        End If

        ' Nesto#458: para un vendedor, el combo se filtra a su equipo (más NV si es jefe)
        If Not EsOficinaComercial AndAlso Not String.IsNullOrWhiteSpace(vendedor) Then
            Try
                Dim equipo As List(Of VendedorDTO) = Await servicio.LeerVendedoresEquipo(empresaActual, vendedor.Trim())
                EquipoUsuario = equipo.Select(Function(v) v.Vendedor?.Trim()).
                    Where(Function(v) Not String.IsNullOrWhiteSpace(v)).ToList()
                EsJefeDeVentas = EquipoUsuario.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1
                FiltroEquipoVendedores = vendedor.Trim()
            Catch
                ' Sin equipo resoluble el combo se queda sin filtrar; el servidor sigue mandando
            End Try
        End If
        OnPropertyChanged(NameOf(PuedeEditarComercial))
        OnPropertyChanged(NameOf(MotivoComercialBloqueado))

        Dim motorPagosParametro As String = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, "MotorPagos")
        If Not String.IsNullOrWhiteSpace(motorPagosParametro) Then
            MotorPagosSeleccionado = motorPagosParametro.Trim()
        End If
    End Function

    Protected Overrides Sub CargarDatosDespuesDeElegirCliente()
        CargarDeudaVencida()
    End Sub

    Protected Overrides Sub AlCambiarClienteActivo(value As ClienteJson)
        ' Nesto#558: el estado con que llegó la ficha (el TextBox edita este mismo objeto, así que es la única
        ' ocasión de saber de qué estado se viene al guardar).
        _estadoCargado = value?.estado

        ' Nesto#644 (1 y 4): con OTRO cliente (o empresa o contacto) no queda nada de la reclamación ni del cobro del anterior
        Dim clave As String = ClaveCliente(value)
        If clave <> _claveClienteDatos Then
            _claveClienteDatos = clave
            ReiniciarDatosDelCliente(value)
        End If

        If Not IsNothing(ListaClientesFiltrable) AndAlso Not IsNothing(ListaClientesFiltrable.Lista) Then
            If Not IsNothing(clienteActivoDTO) Then
                cargarVendedoresPorGrupo()
                CargarSeguimientos()
                CargarListaVentas()
                CargarDeudaVencida()
            Else
                seguimientosOrdenados = Nothing
                listaVentas = Nothing
            End If
        ElseIf IsNothing(value) Then
            seguimientosOrdenados = Nothing
            listaVentas = Nothing
            deudaVencida = 0
            ListaFacturas = Nothing
            ListaPedidos = Nothing
            ListaDeudas = Nothing
        End If

        If IndiceSeleccionado = 1 Then
            CargarFacturas()
        ElseIf IndiceSeleccionado = 2 Then
            CargarPedidos()
        ElseIf IndiceSeleccionado = 3 Then
            CargarDeudas()
        Else
            ListaFacturas = Nothing
            ListaPedidos = Nothing
            ListaDeudas = Nothing
        End If
    End Sub

    ' Nesto#644 (1 y 4): de qué cliente son los datos de la reclamación y del cobro que hay en pantalla.
    Private _claveClienteDatos As String

    Private Shared Function ClaveCliente(cliente As ClienteJson) As String
        If cliente Is Nothing Then
            Return Nothing
        End If
        Return String.Join("|", cliente.empresa?.Trim(), cliente.cliente?.Trim(), cliente.contacto?.Trim())
    End Function

    ''' <summary>
    ''' Nesto#644: al pasar a otro cliente (o a ninguno), lo que era del anterior se quita: el correo y el móvil pasan a ser
    ''' los del nuevo y el pedido elegido para cobrar se suelta (1); las facturas, pedidos y deudas del anterior no se quedan en
    ''' pantalla mientras se cargan las del nuevo (2: no se pueden reclamar sus efectos con el cliente nuevo); y se vacían el
    ''' enlace, el asunto, el importe, el nombre, el motivo del cambio de estado y el historial de pagos (4). Sin cliente (no se
    ''' ha podido leer, Nesto#644 3) se vacía además lo demás que se ve de él.
    ''' </summary>
    Private Sub ReiniciarDatosDelCliente(cliente As ClienteJson)
        RellenarContactoReclamacion(cliente)
        PedidoSeleccionadoCobro = Nothing
        ListaFacturas = Nothing
        ListaPedidos = Nothing
        ListaDeudas = Nothing
        EnlaceReclamarDeuda = Nothing
        AsuntoReclamarDeuda = String.Empty
        ImporteReclamarDeuda = 0
        NombreReclamarDeuda = Nothing
        MotivoCambioEstado = Nothing
        HistorialPagos = Nothing
        If cliente Is Nothing Then
            seguimientosOrdenados = Nothing
            listaVentas = Nothing
            deudaVencida = 0
        End If
    End Sub
    <DllImport("shell32")>
    Private Shared Function SHGetKnownFolderPath(ByRef rfid As Guid, ByVal dwFlags As UInteger, ByVal hToken As IntPtr, ByRef np As IntPtr) As Integer : End Function
#End Region
End Class
