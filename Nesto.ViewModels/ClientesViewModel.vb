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
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports Unity

Public Interface IOService
    Function OpenFileDialog(defaultPath As String) As String

    'Other similar untestable IO operations
    Function OpenFile(path As String) As Stream
End Interface

Public Class ClientesViewModel
    Inherits BindableBase
    '    Implements IActiveAware

    ' Nesto#340 (1C.8): VM 100% sin EF — el DbContext se eliminó con el último resto
    ' (listaEmpresas por API + borrado del código muerto de FamiliasVendedor).
    Public Property configuracion As IConfiguracion
    Private ReadOnly Property contenedor As IUnityContainer
    Private ReadOnly Property dialogService As IDialogService
    Private ReadOnly Property servicio As IClienteComercialService
    Private ReadOnly Property servicioRapports As IRapportService
    Private ReadOnly Property servicioAutenticacion As IServicioAutenticacion
    ' Nesto#369: factoría que crea HttpClient con AuthTokenHandler (adjunta el JWT → usuario en ELMAH).
    Private _clienteApiFactory As IClienteApiFactory

    'Dim mainModel As New Nesto.Models.MainModel
    Private ruta As String
    Private EsUsuarioAdministracion As Boolean = False


    Public Structure tipoIdDescripcion
        Public Sub New(
       ByVal _id As String,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub

        Public Property id As String
        Public Property descripcion As String
    End Structure

    Public Sub New()
        ' Este ViewModel se usa con dos Views y eso es un desastre.
        ' Deberíamos separarlo en dos ViewModels diferentes, uno para Clientes y otro para ClientesComercial
        '***************************
        cargarDatos()
        configuracion = Prism.Ioc.ContainerLocator.Container.Resolve(GetType(IConfiguracion))
        _servicioAutenticacion = Prism.Ioc.ContainerLocator.Container.Resolve(GetType(IServicioAutenticacion))
        _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
        ' Nesto#340 (1C.8, slice 4): actualizarCliente carga la ficha por la API en las dos vistas,
        ' así que el servicio también hace falta en este constructor. cargarDatos no lo usa hasta
        ' después de su primer Await, cuando ya está asignado.
        servicio = New ClienteComercialService(configuracion, servicioAutenticacion)
    End Sub

    Public Sub New(configuracion As IConfiguracion, contenedor As IUnityContainer, dialogService As IDialogService, servicio As IClienteComercialService, servicioRapports As IRapportService, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        Me.contenedor = contenedor
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
        ReclamarDeudaCommand = New DelegateCommand(AddressOf OnReclamarDeuda)
        AbrirEnlaceReclamacionCommand = New DelegateCommand(AddressOf OnAbrirEnlaceReclamacion, AddressOf CanAbrirEnlaceReclamacion)
        ConfirmarReclamarDeudaCommand = New DelegateCommand(AddressOf OnConfirmarReclamarDeuda, AddressOf CanConfirmarReclamarDeuda)
        GuardarEfectoDeudaCommand = New DelegateCommand(AddressOf OnGuardarEfectoDeuda, AddressOf CanGuardarEfectoDeuda)
    End Sub

    ' Nesto#340 (1C.8, slice 4): constructor para tests. Inyecta fakes, no toca EF (DbContext
    ' queda a Nothing) ni el contenedor de Prism, y no dispara cargarDatos. ListaClientesFiltrable
    ' queda a Nothing a propósito: el setter de clienteActivo no dispara las cargas HTTP en
    ' cascada. Unity sigue eligiendo el constructor de 6 parámetros (el más largo).
    Public Sub New(configuracion As IConfiguracion, dialogService As IDialogService, servicio As IClienteComercialService, servicioRapports As IRapportService, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        Me.dialogService = dialogService
        Me.servicio = servicio
        Me.servicioRapports = servicioRapports
        Me.servicioAutenticacion = servicioAutenticacion
        ReclamarDeudaCommand = New DelegateCommand(AddressOf OnReclamarDeuda)
        AbrirEnlaceReclamacionCommand = New DelegateCommand(AddressOf OnAbrirEnlaceReclamacion, AddressOf CanAbrirEnlaceReclamacion)
        ConfirmarReclamarDeudaCommand = New DelegateCommand(AddressOf OnConfirmarReclamarDeuda, AddressOf CanConfirmarReclamarDeuda)
        GuardarEfectoDeudaCommand = New DelegateCommand(AddressOf OnGuardarEfectoDeuda, AddressOf CanGuardarEfectoDeuda)
    End Sub

#Region "Propiedades"
    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            Dim unused = SetProperty(_titulo, value)
        End Set
    End Property

    Private _empresaActual As String
    Public Property empresaActual As String
        Get
            Return _empresaActual
        End Get
        Set(value As String)
            _empresaActual = value
            CargarListaContactos()
            If IsNothing(contactoActual) Then
                Dim unused = CargarClienteActualEmpresa()
            End If
            ActualizarClienteAsync(_empresaActual, clienteActual, contactoActual)
            RaisePropertyChanged("empresaActual")
        End Set
    End Property

    Private Async Function CargarClienteActualEmpresa() As Task
        Dim mainViewModel = New MainViewModel()
        clienteActual = Await mainViewModel.leerParametro(empresaActual, "UltNumCliente")
    End Function

    ' Nesto#340 (1C.7): la lista de contactos se lee de la API (PlantillaVentas/DireccionesEntrega,
    ' mismo filtro Estado >= 0) en vez de consultar Clientes con EF. Se mantiene la colección de
    ' Clientes como contenedor porque el ComboBox solo usa el campo Contacto y así los bindings
    ' quedan intactos. Al asignar clienteActual el setter relanza la carga, así que no hace falta
    ' recargar tras CargarClienteActualEmpresa.
    Private _servicioDireccionesEntrega As ControlesUsuario.Services.IServicioDireccionesEntrega
    Private ReadOnly Property ServicioDireccionesEntrega As ControlesUsuario.Services.IServicioDireccionesEntrega
        Get
            If _servicioDireccionesEntrega Is Nothing Then
                _servicioDireccionesEntrega = Prism.Ioc.ContainerLocator.Container.Resolve(GetType(ControlesUsuario.Services.IServicioDireccionesEntrega))
            End If
            Return _servicioDireccionesEntrega
        End Get
    End Property

    Private Async Sub CargarListaContactos()
        Try
            If String.IsNullOrWhiteSpace(_empresaActual) OrElse String.IsNullOrWhiteSpace(_clienteActual) Then
                listaContactos = New ObservableCollection(Of Clientes)()
                Return
            End If
            Dim direcciones = Await ServicioDireccionesEntrega.ObtenerDireccionesEntrega(_empresaActual.Trim(), _clienteActual.Trim())
            listaContactos = New ObservableCollection(Of Clientes)(
                direcciones.Select(Function(d) New Clientes With {
                    .Empresa = _empresaActual,
                    .Nº_Cliente = _clienteActual,
                    .Contacto = d.contacto
                }))
        Catch ex As Exception
            listaContactos = New ObservableCollection(Of Clientes)()
        End Try
    End Sub

    Private _clienteActual As String
    Public Property clienteActual As String
        Get
            Return _clienteActual
        End Get
        Set(value As String)
            _clienteActual = value
            CargarListaContactos()
            ActualizarClienteAsync(_empresaActual, _clienteActual, _contactoActual)
            RaisePropertyChanged(NameOf(clienteActual))
            ' Nesto#340 (1C.8, slice 4): el Titulo se pone en actualizarCliente al terminar la
            ' carga async (aquí clienteActivo aún sería el cliente anterior).
        End Set
    End Property

    Private _contactoActual As String
    Public Property contactoActual As String
        Get
            Return _contactoActual
        End Get
        Set(value As String)
            _contactoActual = value
            ActualizarClienteAsync(_empresaActual, _clienteActual, _contactoActual)
            RaisePropertyChanged("contactoActual")
        End Set
    End Property

    Private _clienteServidor As ClienteJson
    Public Property clienteServidor As ClienteJson
        Get
            Return _clienteServidor
        End Get
        Set(value As ClienteJson)
            _clienteServidor = value
            RaisePropertyChanged("clienteServidor")
        End Set
    End Property

    Private _deudaSeleccionada As ExtractoClienteDTO
    Public Property DeudaSeleccionada As ExtractoClienteDTO
        Get
            Return _deudaSeleccionada
        End Get
        Set(value As ExtractoClienteDTO)
            Dim unused = SetProperty(_deudaSeleccionada, value)
            GuardarEfectoDeudaCommand.RaiseCanExecuteChanged()
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
            RaisePropertyChanged(NameOf(estadoPeluqueria))
        End Set
    End Property

    ' Nesto#340 (1C.8, último resto EF): POCO del API en vez de la entidad EF Empresas.
    Private _listaEmpresas As ObservableCollection(Of EmpresaModel)
    Public Property listaEmpresas As ObservableCollection(Of EmpresaModel)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of EmpresaModel))
            _listaEmpresas = value
            RaisePropertyChanged("listaEmpresas")
        End Set
    End Property

    Private _nombre As String
    Public Property nombre As String
        Get
            Return _nombre
        End Get
        Set(value As String)
            _nombre = value
            RaisePropertyChanged("nombre")
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 5): la cuenta activa deja de ser la entidad EF y pasa al POCO
    ' CCCModel (mismos nombres de propiedad que bindea la vista, con dirty flag propio).
    Private _cuentaActiva As CCCModel
    Public Property cuentaActiva As CCCModel
        Get
            Return _cuentaActiva
        End Get
        Set(value As CCCModel)
            _cuentaActiva = value
            RaisePropertyChanged("cuentaActiva")
            RaisePropertyChanged(NameOf(descripcionEstadoCCC))
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 4): la vista mostraba clienteActivo.CCC2.EstadosCCC.Descripción (dos
    ' nav properties de EF). Con el POCO, la descripción se calcula con la cuenta activa y la
    ' lista de estados que ya está cargada.
    Public ReadOnly Property descripcionEstadoCCC As String
        Get
            If IsNothing(cuentaActiva) OrElse IsNothing(listaEstadosCCC) Then
                Return String.Empty
            End If
            Return listaEstadosCCC.FirstOrDefault(Function(e) e.Número = cuentaActiva.Estado)?.Descripción
        End Get
    End Property

    Private _cuentasBanco As ObservableCollection(Of CCCModel)
    Public Property cuentasBanco As ObservableCollection(Of CCCModel)
        Get
            Return _cuentasBanco
        End Get
        Set(value As ObservableCollection(Of CCCModel))
            _cuentasBanco = value
            RaisePropertyChanged("cuentasBanco")
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 4): la ficha del cliente se carga de la API (GET Clientes) en vez de
    ' DbContext.Clientes, y clienteActivo pasa a ser el DTO. La cuenta activa se localiza por el
    ' campo escalar ccc (antes nav property CCC2). Slice 5: cuentasBanco también viene de la API
    ' (GET Clientes/CCCs) como POCOs con dirty flag; ya no queda EF en la carga de la ficha.
    ' El Titulo se pone aquí (antes en el setter de clienteActual) porque la carga es async.
    ' Es Function As Task (no Sub) para que los tests puedan await; los setters la invocan como
    ' sentencia (fire-and-forget) y la excepción queda capturada aquí dentro.
    Public Async Function ActualizarClienteAsync(empresa As String, numCliente As String, contacto As String) As Task
        If IsNothing(empresa) OrElse IsNothing(numCliente) OrElse IsNothing(contacto) Then
            Return
        End If
        Try
            Dim cliente As ClienteJson = Await servicio.LeerCliente(empresa, numCliente, contacto)
            If Not IsNothing(cliente) Then
                nombre = cliente.nombre
                Dim cccs As List(Of CCCModel) = Await servicio.LeerCCCs(empresa, numCliente, contacto)
                cuentasBanco = New ObservableCollection(Of CCCModel)(If(cccs, New List(Of CCCModel)))
                If Not String.IsNullOrWhiteSpace(cliente.ccc) AndAlso Not IsNothing(cuentasBanco) Then
                    cuentaActiva = cuentasBanco.Where(Function(x) x.Número.Trim = cliente.ccc.Trim).FirstOrDefault
                End If
                clienteActivo = cliente
                If Not IsNothing(cliente.cliente) Then
                    Titulo = String.Format("Cliente {0}", cliente.cliente.Trim)
                End If
                extractoCCC = Nothing
                pedidosCCC = Nothing
            End If
        Catch ex As Exception
            mensajeError = ex.Message
        End Try
    End Function

    ' Nesto#340 (1C.8, slice 4): clienteActivo YA ES la ficha completa de la API (incluye
    ' VendedoresGrupoProducto), así que no hace falta una segunda llamada: clienteServidor se
    ' unifica con clienteActivo.
    Private Sub cargarVendedoresPorGrupo()
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
        Try
            Using client As HttpClient = _clienteApiFactory.Crear()
                Dim urlConsulta As String = "SeguimientosClientes"
                urlConsulta += "?empresa=" + If(clienteActivo.empresa?.Trim, String.Empty)
                urlConsulta += "&cliente=" + clienteActivo.cliente.Trim
                urlConsulta += "&contacto=" + If(clienteActivo.contacto?.Trim, String.Empty)
                Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    Dim seguimientos = JsonConvert.DeserializeObject(Of List(Of SeguimientoResumen))(respuesta)
                    seguimientosOrdenados = New ObservableCollection(Of SeguimientoResumen)(
                        seguimientos.OrderByDescending(Function(s) s.Fecha).Take(20))
                Else
                    seguimientosOrdenados = New ObservableCollection(Of SeguimientoResumen)()
                End If
            End Using
        Catch ex As Exception
            seguimientosOrdenados = New ObservableCollection(Of SeguimientoResumen)()
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
        Try
            Using client As HttpClient = _clienteApiFactory.Crear()
                Dim urlConsulta As String = "ExtractosCliente/DeudaVencida"
                urlConsulta += "?cliente=" + clienteActivo.cliente.Trim
                urlConsulta += "&contacto=" + If(clienteActivo.contacto?.Trim, String.Empty)
                Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    deudaVencida = JsonConvert.DeserializeObject(Of Decimal)(respuesta)
                Else
                    deudaVencida = 0
                End If
            End Using
        Catch ex As Exception
            deudaVencida = 0
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
        Try
            Using client As HttpClient = _clienteApiFactory.Crear()
                Dim urlConsulta As String = "ventascliente/productos"
                urlConsulta += "?clienteId=" + clienteActivo.cliente.Trim
                urlConsulta += "&contacto=" + If(clienteActivo.contacto?.Trim, String.Empty)
                If rangoFechasVenta <> "System.Windows.Controls.ComboBoxItem: Ventas de siempre" Then 'esto está fatal, hay que desacoplarlo de la vista
                    urlConsulta += "&fechaDesde=" + Date.Now.AddYears(-1).ToString("s")
                End If

                Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    listaVentas = JsonConvert.DeserializeObject(Of ObservableCollection(Of lineaVentaAgrupada))(respuesta)
                Else
                    listaVentas = New ObservableCollection(Of lineaVentaAgrupada)()
                End If
            End Using
        Catch ex As Exception
            listaVentas = New ObservableCollection(Of lineaVentaAgrupada)()
        End Try
    End Sub

    ' Nesto#340 (1C.8, slice 4): POCO de la API en vez de la entidad EF Clientes. Es la misma
    ' ficha que clienteServidor (se cargan de una sola llamada en actualizarCliente).
    Private _clienteActivo As ClienteJson
    Public Property clienteActivo As ClienteJson
        Get
            Return _clienteActivo
        End Get
        Set(value As ClienteJson)
            _clienteActivo = value

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

            RaisePropertyChanged("clienteActivo")
        End Set
    End Property

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

            RaisePropertyChanged("clienteActivoDTO")
        End Set
    End Property

    Private Property _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            _mensajeError = value
            RaisePropertyChanged("mensajeError")
        End Set
    End Property

    Private _selectedPath As String
    Public Property selectedPath() As String
        Get
            Return _selectedPath
        End Get
        Set(value As String)
            _selectedPath = value
            RaisePropertyChanged("selectedPath")
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 5): estados de CCC desde la API (POCO con Número/Descripción, los
    ' nombres que bindea la vista). Como la carga ahora es async, se notifica también la
    ' descripción del estado por si la ficha se cargó antes que el catálogo.
    Private Property _listaEstadosCCC As ObservableCollection(Of EstadoCCCModel)
    Public Property listaEstadosCCC As ObservableCollection(Of EstadoCCCModel)
        Get
            Return _listaEstadosCCC
        End Get
        Set(value As ObservableCollection(Of EstadoCCCModel))
            _listaEstadosCCC = value
            RaisePropertyChanged("listaEstadosCCC")
            RaisePropertyChanged(NameOf(descripcionEstadoCCC))
        End Set
    End Property

    Private Property _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
            RaisePropertyChanged("vendedor")
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
            RaisePropertyChanged("vendedorPorGrupo")
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
    '        RaisePropertyChanged("listaClientesVendedor")
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
            RaisePropertyChanged("seguimientosOrdenados")
        End Set
    End Property

    Private _listaVentas As ObservableCollection(Of lineaVentaAgrupada)
    Public Property listaVentas As ObservableCollection(Of lineaVentaAgrupada)
        Get
            Return _listaVentas
        End Get
        Set(value As ObservableCollection(Of lineaVentaAgrupada))
            _listaVentas = value
            RaisePropertyChanged("listaVentas")
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

    '    RaisePropertyChanged("filtro")
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
            RaisePropertyChanged("rangoFechasVenta")
        End Set
    End Property

    Private _deudaVencida As Nullable(Of Decimal)
    Public Property deudaVencida As Nullable(Of Decimal)
        Get
            Return _deudaVencida
        End Get
        Set(value As Nullable(Of Decimal))
            _deudaVencida = value
            RaisePropertyChanged("deudaVencida")
        End Set
    End Property

    Private _listaSecuencias As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaSecuencias As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaSecuencias
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaSecuencias = value
            RaisePropertyChanged("listaSecuencias")
        End Set
    End Property

    Private _listaTipos As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaTipos As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaTipos
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaTipos = value
            RaisePropertyChanged("listaTipos")
        End Set
    End Property

    ' Nesto#340 (1C.8, slice 5): los avisos de efectos con otro CCC vienen del PUT Clientes/CCCs
    ' como POCO ligero (FechaVto/Concepto/ImportePdte/CCC, lo único que bindea el grid).
    Private _extractoCCC As ObservableCollection(Of ExtractoCCCModel)
    Public Property extractoCCC() As ObservableCollection(Of ExtractoCCCModel)
        Get
            Return _extractoCCC
        End Get
        Set(ByVal value As ObservableCollection(Of ExtractoCCCModel))
            _extractoCCC = value
            RaisePropertyChanged("extractoCCC")
            RaisePropertyChanged("estaVisibleExtractoCCC")
        End Set
    End Property

    Private _pedidosCCC As ObservableCollection(Of cabeceraPedidoAgrupada)
    Public Property pedidosCCC() As ObservableCollection(Of cabeceraPedidoAgrupada)
        Get
            Return _pedidosCCC
        End Get
        Set(ByVal value As ObservableCollection(Of cabeceraPedidoAgrupada))
            _pedidosCCC = value
            RaisePropertyChanged("pedidosCCC")
            RaisePropertyChanged("estaVisiblePedidosCCC")
        End Set
    End Property

    Private _listaContactos As ObservableCollection(Of Clientes)
    Public Property listaContactos() As ObservableCollection(Of Clientes)
        Get
            Return _listaContactos
        End Get
        Set(ByVal value As ObservableCollection(Of Clientes))
            _listaContactos = value
            RaisePropertyChanged("listaContactos")
            If Not IsNothing(value.FirstOrDefault) Then
                contactoActual = value.FirstOrDefault.Contacto
                RaisePropertyChanged("contactoActual")
            End If
        End Set
    End Property

    Private _estaOcupado As Boolean
    Public Property estaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(value As Boolean)
            _estaOcupado = value
            RaisePropertyChanged("estaOcupado")
        End Set
    End Property

    Public ReadOnly Property estaVisibleExtractoCCC As Visibility
        Get
            If Not IsNothing(extractoCCC) AndAlso extractoCCC.Count > 0 Then
                Return Visibility.Visible
            Else
                Return Visibility.Hidden
            End If
        End Get
    End Property

    Public ReadOnly Property estaVisiblePedidosCCC As Visibility
        Get
            If Not IsNothing(pedidosCCC) AndAlso pedidosCCC.Count > 0 Then
                Return Visibility.Visible
            Else
                Return Visibility.Hidden
            End If
        End Get
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
            End If
        End Set
    End Property

    Private _listaPedidos As ObservableCollection(Of ResumenPedido)
    Public Property ListaPedidos As ObservableCollection(Of ResumenPedido)
        Get
            Return _listaPedidos
        End Get
        Set(value As ObservableCollection(Of ResumenPedido))
            _listaPedidos = value
            RaisePropertyChanged("ListaPedidos")
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
            RaisePropertyChanged("IndiceSeleccionado")
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
            ConfirmarReclamarDeudaCommand.RaiseCanExecuteChanged()
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
                ConfirmarReclamarDeudaCommand.RaiseCanExecuteChanged()
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
            ConfirmarReclamarDeudaCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _motivoCambioEstado As String
    Public Property MotivoCambioEstado As String
        Get
            Return _motivoCambioEstado
        End Get
        Set(value As String)
            Dim unused = SetProperty(_motivoCambioEstado, value)
            GuardarEfectoDeudaCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _movilReclamarDeuda As String
    Public Property MovilReclamarDeuda As String
        Get
            Return _movilReclamarDeuda
        End Get
        Set(value As String)
            Dim unused = SetProperty(_movilReclamarDeuda, value)
            ConfirmarReclamarDeudaCommand.RaiseCanExecuteChanged()
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
            AbrirEnlaceReclamacionCommand.RaiseCanExecuteChanged()
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
    Private _cmdGuardar As ICommand
    Public ReadOnly Property cmdGuardar() As ICommand
        Get
            If _cmdGuardar Is Nothing Then
                _cmdGuardar = New RelayCommand(AddressOf Guardar, AddressOf CanGuardar)
            End If
            Return _cmdGuardar
        End Get
    End Property
    ' Nesto#340 (1C.8, slice 5): el dirty pasa del ChangeTracker de EF al flag EsModificado de
    ' los POCOs CCCModel (mismo patrón que AlquilerModel en 1C.3).
    Private Function CanGuardar(ByVal param As Object) As Boolean
        Return Not IsNothing(cuentaActiva) AndAlso Not IsNothing(cuentasBanco) AndAlso
            cuentasBanco.Any(Function(c) c.EsModificado) AndAlso
            (File.Exists(rutaMandato) OrElse (cuentaActiva.Estado <> 5 And cuentaActiva.Estado <> 1))
    End Function
    ' Nesto#340 (1C.8, slice 5): guardar pasa por PUT api/Clientes/CCCs. El servidor hace el
    ' upsert de los CCC modificados y devuelve los avisos (efectos pendientes y pedidos abiertos
    ' que apuntan a OTRO CCC) que antes calculaba este VM con dos consultas EF.
    Private Async Sub Guardar(ByVal param As Object)
        Try
            ' Nesto#396: sin cuenta activa no hay nada que guardar. CanGuardar ya lo filtra, pero el comando
            ' puede dispararse en un teardown con cuentaActiva ya a Nothing; sin esta guarda, cuentaActiva.Número
            ' abajo lanza NRE.
            If IsNothing(cuentaActiva) OrElse IsNothing(cuentasBanco) Then
                Return
            End If

            Dim modificados = cuentasBanco.Where(Function(c) c.EsModificado).ToList()
            If Not modificados.Any() Then
                Return
            End If

            Dim peticion As New GuardarCCCsRequest With {
                .empresa = empresaActual,
                .cliente = clienteActual,
                .contacto = contactoActual,
                .cccActivo = cuentaActiva.Número,
                .cccs = modificados
            }
            Dim respuesta As GuardarCCCsRespuesta = Await servicio.GuardarCCCs(peticion)

            extractoCCC = New ObservableCollection(Of ExtractoCCCModel)(If(respuesta?.extractoOtroCCC, New List(Of ExtractoCCCModel)))
            pedidosCCC = New ObservableCollection(Of cabeceraPedidoAgrupada)(If(respuesta?.pedidosOtroCCC, New List(Of cabeceraPedidoAgrupada)))

            For Each ccc In modificados
                ccc.EsModificado = False
            Next
            mensajeError = ""
        Catch ex As Exception
            ' Nesto#396: ex.InnerException puede ser Nothing (p.ej. un NullReferenceException pelado). Usar
            ' ex.Message en ese caso, para no provocar un SEGUNDO NRE dentro del Catch que escapa como
            ' DispatcherUnhandledException y tumba la aplicación (mismo patrón que VerMandato/NuevoMandato).
            ' Nesto#399: EF anida el error real (SqlException / raiserror de trigger) en
            ' DbUpdateException→UpdateException, cuyos mensajes son genéricos ("An error occurred when
            ' updating the entries…"). Mostramos la excepción MÁS INTERNA (el error real, accionable) y
            ' registramos la excepción completa en ELMAH vía /api/Errores para poder diagnosticarla.
            mensajeError = ExcepcionMasInterna(ex).Message
            Dim servicioErrores = contenedor?.Resolve(Of IServicioRegistroErrores)()
            Dim unused2 = servicioErrores?.RegistrarErrorAsync(ex, "ClientesViewModel.Guardar (Nuevo Mandato / CCC)")
        End Try

    End Sub

    ' Nesto#399: EF anida el error real varias capas (DbUpdateException→UpdateException→SqlException).
    ' Devuelve la excepción más interna para mostrar/loguear el mensaje real y no el genérico de EF.
    Private Shared Function ExcepcionMasInterna(ByVal ex As Exception) As Exception
        Dim actual As Exception = ex
        While actual.InnerException IsNot Nothing
            actual = actual.InnerException
        End While
        Return actual
    End Function

    Private _cmdVerMandato As ICommand
    Public ReadOnly Property cmdVerMandato() As ICommand
        Get
            If _cmdVerMandato Is Nothing Then
                _cmdVerMandato = New RelayCommand(AddressOf VerMandato, AddressOf CanVerMandato)
            End If
            Return _cmdVerMandato
        End Get
    End Property
    Private Function CanVerMandato(ByVal param As Object) As Boolean
        'Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified Or System.Data.EntityState.Deleted)
        If IsNothing(cuentaActiva) Then
            Return False
        ElseIf Not File.Exists(rutaMandato) Then
            Return False
        Else
            Return True
        End If
    End Function
    Private Sub VerMandato(ByVal param As Object)
        Try
            Dim fileName As String = rutaMandato()
            Dim process As New System.Diagnostics.Process
            process.StartInfo.FileName = fileName
            process.StartInfo.UseShellExecute = True
            Dim unused = process.Start()
            process.WaitForExit()
            mensajeError = ""
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try
    End Sub

    Private _cmdNuevoMandato As ICommand
    Public ReadOnly Property cmdNuevoMandato() As ICommand
        Get
            If _cmdNuevoMandato Is Nothing Then
                _cmdNuevoMandato = New RelayCommand(AddressOf NuevoMandato, AddressOf CanNuevoMandato)
            End If
            Return _cmdNuevoMandato
        End Get
    End Property
    Private Function CanNuevoMandato(ByVal param As Object) As Boolean
        Return True
    End Function
    ' Nesto#340 (1C.8, slice 5): el CCC nuevo es un POCO marcado como modificado; se persiste
    ' al Guardar (PUT api/Clientes/CCCs hace el upsert), igual que antes hacía SaveChanges.
    Private Sub NuevoMandato(ByVal param As Object)
        Try
            Dim siguienteNumero As Integer
            If IsNothing(cuentasBanco) Then
                cuentasBanco = New ObservableCollection(Of CCCModel)
            End If
            If cuentasBanco.Count = 0 Then
                siguienteNumero = 1
            Else
                siguienteNumero = CInt(cuentasBanco.Where(Function(x) x.Cliente.Trim = clienteActual).OrderBy(Function(x) x.Número).LastOrDefault.Número) + 1
            End If
            cuentaActiva = New CCCModel With {
                .Empresa = empresaActual,
                .Cliente = clienteActual,
                .Contacto = contactoActual,
                .Número = siguienteNumero.ToString(),
                .Estado = 0,
                .Secuencia = "FRST",
                .EsModificado = True
            }
            cuentasBanco.Add(cuentaActiva)
            mensajeError = ""

        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try
    End Sub

    Private _cmdAsignarMandato As ICommand
    Public ReadOnly Property cmdAsignarMandato() As ICommand
        Get
            If _cmdAsignarMandato Is Nothing Then
                _cmdAsignarMandato = New RelayCommand(AddressOf AsignarMandato, AddressOf CanAsignarMandato)
            End If
            Return _cmdAsignarMandato
        End Get
    End Property
    Private Function CanAsignarMandato(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub AsignarMandato(ByVal param As Object)
        Dim elegirFichero = New OpenFileDialog With {
            .Filter = "pdf files (*.pdf)|*.pdf|All files (*.*)|*.*",
            .FilterIndex = 1,
            .RestoreDirectory = True
        }

        If elegirFichero.ShowDialog() Then
            Try
                selectedPath = elegirFichero.FileName
                FileIO.FileSystem.CopyFile(
                selectedPath,
                rutaMandato,
                Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,
                Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing)
            Catch ex As Exception
                If IsNothing(ex.InnerException) Then
                    mensajeError = ex.Message
                Else
                    mensajeError = ex.InnerException.Message
                End If
            Finally
                If selectedPath Is Nothing Then
                    selectedPath = String.Empty
                End If
            End Try
        End If
    End Sub

    Private _cmdGuardarVendedores As ICommand
    Public ReadOnly Property cmdGuardarVendedores() As ICommand
        Get
            If _cmdGuardarVendedores Is Nothing Then
                _cmdGuardarVendedores = New RelayCommand(AddressOf GuardarVendedores, AddressOf CanGuardarVendedores)
            End If
            Return _cmdGuardarVendedores
        End Get
    End Property
    Private Function CanGuardarVendedores(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub GuardarVendedores(ByVal param As Object)
        Dim continuar As Boolean = False
        Dim p As New DialogParameters From {
            {"message", "¿Desea guardar los vendedores?"}
        }
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              If r.Result = ButtonResult.OK Then
                                                                  continuar = True
                                                              End If
                                                          End Sub)
        If Not continuar Then
            Return
        End If

        Try

            ' PUT de clienteServidor
            Using client As HttpClient = _clienteApiFactory.Crear()
                Dim response As HttpResponseMessage
                Dim respuesta As String = ""

                Dim urlConsulta As String = "Clientes/ClienteComercial"
                clienteServidor.estado = clienteActivo.estado
                clienteServidor.usuario = configuracion.usuario
                ' Nesto#340 (1C.8, slice 4): el PUT solo gestiona vendedores; quitamos
                ' PersonasContacto del payload para que el [EmailAddress] del DTO del servidor no
                ' devuelva 400 por correos mal grabados en fichas antiguas.
                Dim payload As Newtonsoft.Json.Linq.JObject = Newtonsoft.Json.Linq.JObject.FromObject(clienteServidor)
                Dim unused = payload.Remove("PersonasContacto")
                Dim content As HttpContent = New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

                response = client.PutAsync(urlConsulta, content).Result

                If response.IsSuccessStatusCode Then
                    respuesta = response.Content.ReadAsStringAsync().Result
                Else
                    Throw New Exception(response.Content.ReadAsStringAsync().Result)
                End If
            End Using

            mensajeError = "Cliente guardado correctamente"

            Dim c As New DialogParameters From {
                {"message", "Se han guardado correctamente los cambios"}
            }
            dialogService.ShowDialog("NotificationDialog", c, Sub(r)

                                                              End Sub)
        Catch ex As Exception
            If Not IsNothing(ex.InnerException) Then
                mensajeError = ex.InnerException.Message
            Else
                mensajeError = ex.Message
                dialogService.ShowError(mensajeError)
            End If
        End Try

    End Sub

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
                _descargarFacturasCommand = New RelayCommand(AddressOf DescargarFacturas, AddressOf CanDescargarFacturas)
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

            For Each fra In FacturasSeleccionadas
                Dim factura As Byte() = Await CargarFactura(fra.Empresa, fra.Documento, MostrarImagenesFacturas)
                If factura IsNot Nothing Then
                    Dim ms As New MemoryStream(factura)
                    'write to file
                    Dim file As New FileStream(path + "\Cliente_" + fra.Cliente?.Trim() + "_" + fra.Documento?.Trim() + ".pdf", FileMode.Create, FileAccess.Write)
                    ms.WriteTo(file)
                    file.Close()
                    ms.Close()
                End If
            Next

            ' Abrimos la carpeta de descargas
            Dim unused = Process.Start(New ProcessStartInfo(path) With {
                .UseShellExecute = True
            })
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

    Private _cargarPedidoCommand As ICommand
    Public ReadOnly Property CargarPedidoCommand() As ICommand
        Get
            If _cargarPedidoCommand Is Nothing Then
                _cargarPedidoCommand = New RelayCommand(AddressOf CargarPedido, AddressOf CanCargarPedido)
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
        PedidoVentaViewModel.CargarPedido(empresaActual, pedido.numero, contenedor)
    End Sub

    Public ReadOnly Property AbrirPedidoAction As Action(Of String, Integer)
        Get
            Return Sub(empresa, numeroPedido) PedidoVentaViewModel.CargarPedido(empresa, numeroPedido, contenedor)
        End Get
    End Property

    Private _imprimirMandatoCommand As ICommand
    Public ReadOnly Property ImprimirMandatoCommand() As ICommand
        Get
            If _imprimirMandatoCommand Is Nothing Then
                _imprimirMandatoCommand = New RelayCommand(AddressOf OnImprimirMandato, AddressOf CanImprimirMandato)
            End If
            Return _imprimirMandatoCommand
        End Get
    End Property
    Private Function CanImprimirMandato(ByVal param As Object) As Boolean
        Return Not String.IsNullOrEmpty(empresaActual) AndAlso Not String.IsNullOrEmpty(clienteActual) AndAlso
            Not String.IsNullOrEmpty(contactoActual) AndAlso Not IsNothing(cuentaActiva) AndAlso Not String.IsNullOrEmpty(cuentaActiva.Número)
    End Function
    Private Async Sub OnImprimirMandato(ByVal param As Object)
        estaOcupado = True
        Try
            'Dim np As IntPtr
            'SHGetKnownFolderPath(New Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, np)
            'Dim path As String = Marshal.PtrToStringUni(np)
            'Marshal.FreeCoTaskMem(np)

            'Dim mandato As Byte() = Await CargarMandato(empresaActual.Trim, clienteActual.Trim, contactoActual.Trim, cuentaActiva.Número.Trim).ConfigureAwait(True)
            'Dim ms As New MemoryStream(mandato)
            ''write to file
            'Dim file As New FileStream(path + "\Mandato_" + empresaActual.Trim + "_" + clienteActual.Trim + "_" + cuentaActiva.Número.Trim + ".pdf", FileMode.Create, FileAccess.Write)
            'ms.WriteTo(file)
            'file.Close()
            'ms.Close()

            '' Abrimos la carpeta de descargas
            'Process.Start(New ProcessStartInfo(path) With {
            '    .UseShellExecute = True
            '})
            Dim urlPdf As String = $"{configuracion.servidorAPI}Clientes/MandatoPDF?empresa={empresaActual.Trim}&cliente={clienteActual.Trim}&contacto={contactoActual.Trim}&ccc={cuentaActiva.Número.Trim}"
            Dim unused = Process.Start(New ProcessStartInfo(urlPdf) With {
                .UseShellExecute = True
            })
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



    Private _reclamarDeudaCommand As DelegateCommand
    Public Property ReclamarDeudaCommand As DelegateCommand
        Get
            Return _reclamarDeudaCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_reclamarDeudaCommand, value)
        End Set
    End Property
    Private Async Sub OnReclamarDeuda()
        Dim errorReclamacion As Exception = Nothing
        Try
            Using client As HttpClient = _clienteApiFactory.Crear()

            If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

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
                    Throw New Exception($"Error al crear enlace de pago: {errorMsg}")
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido procesar el enlace de pago: " & ex.Message)
            End Try
            End Using
        Catch ex As Exception
            ' En vez de crashear la aplicación (Async Sub sin manejo => excepción no observada),
            ' capturamos el error para informar al usuario y registrarlo en ELMAH.
            ' (VB no permite Await dentro de un Catch, por eso se trata fuera del Try).
            errorReclamacion = ex
        End Try

        If errorReclamacion IsNot Nothing Then
            Try
                Dim servicioErrores = contenedor?.Resolve(Of IServicioRegistroErrores)()
                If servicioErrores IsNot Nothing Then
                    Await servicioErrores.RegistrarErrorAsync(errorReclamacion, "ClientesViewModel.OnReclamarDeuda")
                End If
            Catch
                ' El registro de errores nunca debe impedir mostrar el aviso al usuario.
            End Try

            Dim mensaje As String = If(errorReclamacion.InnerException IsNot Nothing, errorReclamacion.InnerException.Message, errorReclamacion.Message)
            Dim p As New DialogParameters From {
                {"message", "No se ha podido reclamar la deuda." & vbCrLf & vbCrLf & mensaje}
            }
            dialogService.ShowDialog("NotificationDialog", p, Sub(r)
                                                              End Sub)
        End If
    End Sub

    Public Property AbrirEnlaceReclamacionCommand As DelegateCommand
    Private Function CanAbrirEnlaceReclamacion() As Boolean
        Return Not String.IsNullOrEmpty(EnlaceReclamarDeuda)
    End Function
    Private Sub OnAbrirEnlaceReclamacion()
        Dim psi As New System.Diagnostics.ProcessStartInfo(EnlaceReclamarDeuda) With {
            .UseShellExecute = True
        }
        Dim unused = System.Diagnostics.Process.Start(psi)
    End Sub

    Public Property ConfirmarReclamarDeudaCommand As DelegateCommand
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
        Dim p As New DialogParameters From {
            {"message", "¿Desea reclamar la deuda?"}
        }
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              If r.Result = ButtonResult.OK Then
                                                                  ReclamarDeudaCommand.Execute()
                                                              End If
                                                          End Sub)
    End Sub

    Public Property GuardarEfectoDeudaCommand As DelegateCommand
    Private Function CanGuardarEfectoDeuda() As Boolean
        Return Not IsNothing(DeudaSeleccionada) AndAlso EsUsuarioAdministracion AndAlso Not String.IsNullOrWhiteSpace(MotivoCambioEstado)
    End Function
    Private Async Sub OnGuardarEfectoDeuda()
        Dim p As New DialogParameters
        Dim confirmacion As Boolean
        p.Add("message", "¿Desea guardar los cambios?")
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              confirmacion = r.Result = ButtonResult.OK
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

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsByteArrayAsync()
                Else
                    respuesta = Nothing
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido cargar la lista de facturas desde el servidor")
            Finally

            End Try

            Return respuesta
        End Using


    End Function

    Private Async Sub CargarFacturas()
        If IsNothing(clienteActivo) OrElse (ListaFacturas IsNot Nothing AndAlso clienteActivo.cliente?.Trim() = ListaFacturas.FirstOrDefault()?.Cliente) Then
            Return
        End If


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

            ListaFacturas = JsonConvert.DeserializeObject(Of ObservableCollection(Of ExtractoClienteDTO))(respuesta)
        End Using


    End Sub

    Private Async Function CargarMandato(empresa As String, cliente As String, contacto As String, ccc As String) As Task(Of Byte())

        Using client As HttpClient = _clienteApiFactory.Crear()
            Dim response As HttpResponseMessage
            Dim respuesta As Byte()

            Try
                Dim urlConsulta As String = "Clientes"
                urlConsulta += "?empresa=" + empresa.Trim
                urlConsulta += "&cliente=" + cliente.Trim
                urlConsulta += "&contacto=" + contacto.Trim
                urlConsulta += "&ccc=" + ccc.Trim

                response = Await client.GetAsync(urlConsulta).ConfigureAwait(True)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsByteArrayAsync()
                Else
                    respuesta = Nothing
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido cargar el mandato desde el servidor")
            Finally

            End Try

            Return respuesta
        End Using


    End Function

    Private Async Sub CargarPedidos()
        If IsNothing(clienteActivo) OrElse (ListaPedidos IsNot Nothing AndAlso clienteActivo.cliente?.Trim() = ListaPedidos.FirstOrDefault()?.cliente) Then
            Return
        End If


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

            ListaPedidos = JsonConvert.DeserializeObject(Of ObservableCollection(Of ResumenPedido))(respuesta)
        End Using


    End Sub

    Private Async Sub CargarDeudas()
        If IsNothing(clienteActivo) OrElse (ListaDeudas IsNot Nothing AndAlso clienteActivo.cliente?.Trim() = ListaDeudas.FirstOrDefault()?.Cliente) Then
            Return
        End If

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
            Dim lista = JsonConvert.DeserializeObject(Of ObservableCollection(Of ExtractoClienteDTO))(respuesta)
            For Each l In lista
                l.Seleccionada = l.Vencimiento < Date.Today OrElse IsNothing(l.Vencimiento)
                AddHandler l.PropertyChanged, New PropertyChangedEventHandler(AddressOf LineaDeudaPropertyChangedEventHandler)
            Next
            ListaDeudas = New ObservableCollection(Of ExtractoClienteDTO)(lista.OrderBy(Function(l) l.Fecha))
            ImporteReclamarDeuda = SumaDeudasSeleccionadas
            ActualizarAsuntoPorDefecto() ' NestoAPI#295: las vencidas vienen preseleccionadas
        End Using

        ' Nesto#340 (1C.8, slice 4): las personas de contacto vienen en la ficha de la API con su
        ' Cargo real; se mapean a la entidad-contenedor que espera CorreoCliente (mismo patrón que
        ' PlantillaVenta, sin consultar EF).
        Dim personasContacto As New List(Of PersonasContactoCliente)
        If Not IsNothing(clienteActivo.PersonasContacto) Then
            For Each persona In clienteActivo.PersonasContacto
                personasContacto.Add(New PersonasContactoCliente With {
                    .Cargo = If(persona.Cargo, 0),
                    .CorreoElectrónico = persona.CorreoElectronico
                })
            Next
        End If
        Dim correo As New CorreoCliente(personasContacto)
        CorreoReclamarDeuda = correo.CorreoAgencia ' TODO: desarrollar correo deuda
        Dim telefono As New Telefono(clienteActivo.telefono)
        MovilReclamarDeuda = telefono.MovilUnico

        CargarHistorialPagos()

    End Sub

    ''' <summary>
    ''' Carga el histórico de enlaces de pago NestoPago del cliente actual.
    ''' Issue Nesto#322.
    ''' </summary>
    Private Async Sub CargarHistorialPagos()
        If IsNothing(clienteActivo) Then
            Return
        End If

        Using client As HttpClient = _clienteApiFactory.Crear()
            Try
                If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Return
                End If

                Dim empresa = clienteActivo.empresa?.Trim()
                Dim cliente = clienteActivo.cliente?.Trim()
                Dim response = Await client.GetAsync($"Pagos/Cliente/{empresa}/{cliente}")

                If response.IsSuccessStatusCode Then
                    Dim respuesta = Await response.Content.ReadAsStringAsync()
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
    Private Function rutaMandato() As String
        Return ruta + empresaActual.Trim + "_" + clienteActual.Trim + "_" + contactoActual.Trim + "_" + cuentaActiva.Número.Trim + ".pdf"
    End Function
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
    Private Async Sub cargarDatos()
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        ' Nesto#340 (1C.8, último resto EF): empresas por API; si falla, mensaje y seguimos
        ' (el resto de la ficha no depende del combo).
        Try
            listaEmpresas = New ObservableCollection(Of EmpresaModel)(Await servicio.LeerEmpresas())
        Catch ex As Exception
            listaEmpresas = New ObservableCollection(Of EmpresaModel)
            mensajeError = $"No se han podido cargar las empresas: {ex.Message}"
        End Try

        Dim mainViewModel As New MainViewModel
        Dim empresaDefecto As String = Await mainViewModel.leerParametro("1", "EmpresaPorDefecto")
        empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
        ruta = Await mainViewModel.leerParametro(empresaActual, "RutaMandatos")
        Dim clienteDefecto As String = Await mainViewModel.leerParametro(empresaActual, "UltNumCliente")
        vendedor = Await mainViewModel.leerParametro(empresaDefecto, "Vendedor")
        If Not IsNothing(configuracion) Then
            EsUsuarioAdministracion = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
            RaisePropertyChanged(NameOf(PuedeElegirMotorPagos))
        End If

        Dim motorPagosParametro As String = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, "MotorPagos")
        If Not String.IsNullOrWhiteSpace(motorPagosParametro) Then
            MotorPagosSeleccionado = motorPagosParametro.Trim()
        End If

        clienteActual = clienteDefecto
        'contactoActual = "0  " 'esto hay que cambiarlo por el ClientePrincipal

        ' Nesto#340 (1C.8, slice 5): estados de CCC desde la API (la tabla no se toca desde EF).
        Try
            listaEstadosCCC = New ObservableCollection(Of EstadoCCCModel)(Await servicio.LeerEstadosCCC(empresaActual))
        Catch ex As Exception
            mensajeError = ex.Message
        End Try

        Dim rangosFechas As New List(Of String) From {
            "Ventas del Último Año",
            "Ventas de Siempre"
        }

        CargarDeudaVencida()

        listaSecuencias = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion("FRST", "Primer adeudo recurrente"),
            New tipoIdDescripcion("RCUR", "Resto de adeudos recurrentes"),
            New tipoIdDescripcion("OOFF", "Operación de un único pago"),
            New tipoIdDescripcion("FNAL", "Último adeudo recurrente")
        }

        listaTipos = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(1, "Consumidor final"),
            New tipoIdDescripcion(2, "Profesional")
        }

        Titulo = "Clientes"
    End Sub
    <DllImport("shell32")>
    Private Shared Function SHGetKnownFolderPath(ByRef rfid As Guid, ByVal dwFlags As UInteger, ByVal hToken As IntPtr, ByRef np As IntPtr) As Integer : End Function
#End Region
End Class

Public Class StringTrimmingConverter
    Implements IValueConverter

    Public Function ConvertBack(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return value
    End Function

    Public Function Convert(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.Convert
        If IsNothing(value) Then
            Return ""
        Else
            Return value.ToString().Trim
        End If

    End Function

End Class

Public Class datosBancoConverter
    Implements IValueConverter

    Public Function ConvertBack(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return value
    End Function

    Public Function Convert(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.Convert
        If IsNothing(value) Then
            Return "Sin datos de banco"
        Else
            Return "Tiene datos de banco"
        End If
    End Function

End Class

''' <summary>
''' Nesto#340 (1C.8, slice 3): fila del grid de seguimientos de la ficha de cliente. Los nombres
''' coinciden con SeguimientoClienteDTO de la API para deserializar sin mapeos.
''' </summary>
Public Class SeguimientoResumen
    Public Property Fecha As Date
    Public Property Tipo As String
    Public Property Vendedor As String
    Public Property Comentarios As String
    Public Property Usuario As String
End Class

Public Class lineaVentaAgrupada
    Public Property producto As String

    Public Property nombre As String
    Public Property cantidad As Integer

    Public Property familia As String

    Public Property fechaUltVenta As Date

    Public Property subGrupo As String
End Class

Public Class cabeceraPedidoAgrupada
    Public Property Numero As Integer
    ' Nesto#340 (1C.8, slice 5): nullable porque CabPedidoVta.Fecha puede venir nula de la API
    Public Property Fecha As Date?
    Public Property CCC As String
End Class

' Nesto#340 (1C.8, slice 5): POCO del CCC de la ficha de clientes. Mantiene los nombres de
' propiedad que bindea Clientes.xaml (SelectedItem.Pais, .Nº_Cuenta, etc.) y mapea con
' JsonProperty los que no coinciden con el CCCDTO de la API. El dirty que antes llevaba el
' ChangeTracker de EF lo lleva EsModificado (los setters editables lo activan).
Public Class CCCModel
    Inherits BindableBase

    <JsonIgnore>
    Public Property EsModificado As Boolean

    Public Property Empresa As String
    Public Property Cliente As String
    Public Property Contacto As String
    <JsonProperty("numero")>
    Public Property Número As String

    Private _pais As String
    Public Property Pais As String
        Get
            Return _pais
        End Get
        Set(value As String)
            If SetProperty(_pais, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _dcIban As String
    <JsonProperty("dcIban")>
    Public Property DC_IBAN As String
        Get
            Return _dcIban
        End Get
        Set(value As String)
            If SetProperty(_dcIban, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _entidad As String
    Public Property Entidad As String
        Get
            Return _entidad
        End Get
        Set(value As String)
            If SetProperty(_entidad, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _oficina As String
    Public Property Oficina As String
        Get
            Return _oficina
        End Get
        Set(value As String)
            If SetProperty(_oficina, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _dc As String
    Public Property DC As String
        Get
            Return _dc
        End Get
        Set(value As String)
            If SetProperty(_dc, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _numeroCuenta As String
    <JsonProperty("numeroCuenta")>
    Public Property Nº_Cuenta As String
        Get
            Return _numeroCuenta
        End Get
        Set(value As String)
            If SetProperty(_numeroCuenta, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _bic As String
    Public Property BIC As String
        Get
            Return _bic
        End Get
        Set(value As String)
            If SetProperty(_bic, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _estado As Short
    Public Property Estado As Short
        Get
            Return _estado
        End Get
        Set(value As Short)
            If SetProperty(_estado, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _tipoMandato As Short?
    Public Property TipoMandato As Short?
        Get
            Return _tipoMandato
        End Get
        Set(value As Short?)
            If SetProperty(_tipoMandato, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _fechaMandato As Date?
    Public Property FechaMandato As Date?
        Get
            Return _fechaMandato
        End Get
        Set(value As Date?)
            If SetProperty(_fechaMandato, value) Then
                EsModificado = True
            End If
        End Set
    End Property

    Private _secuencia As String
    Public Property Secuencia As String
        Get
            Return _secuencia
        End Get
        Set(value As String)
            If SetProperty(_secuencia, value) Then
                EsModificado = True
            End If
        End Set
    End Property
End Class

' Nesto#340 (1C.8, slice 5): estados de CCC de la API; Número/Descripción son los nombres
' que bindea el combo de la vista.
Public Class EstadoCCCModel
    <JsonProperty("numero")>
    Public Property Número As Short
    <JsonProperty("descripcion")>
    Public Property Descripción As String
End Class

' Nesto#340 (1C.8, slice 5): fila del aviso de efectos pendientes con otro CCC (PUT Clientes/CCCs).
' Nombres = columnas que bindea el grid extractoCCC de Clientes.xaml.
Public Class ExtractoCCCModel
    <JsonProperty("fechaVencimiento")>
    Public Property FechaVto As Date?
    Public Property Concepto As String
    <JsonProperty("importePendiente")>
    Public Property ImportePdte As Decimal
    Public Property CCC As String
End Class

' Nesto#340 (1C.8, slice 5): petición/respuesta del PUT api/Clientes/CCCs.
Public Class GuardarCCCsRequest
    Public Property empresa As String
    Public Property cliente As String
    Public Property contacto As String
    Public Property cccActivo As String
    Public Property cccs As List(Of CCCModel)
End Class

Public Class GuardarCCCsRespuesta
    Public Property extractoOtroCCC As List(Of ExtractoCCCModel)
    Public Property pedidosOtroCCC As List(Of cabeceraPedidoAgrupada)
End Class

Public Class ExtractoClienteDTO
    Inherits BindableBase
    Public Property Id As Integer
    Public Property Empresa As String
    Public Property Asiento As Integer
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Fecha As Date
    Public Property Tipo As String
    Public Property Documento As String
    Public Property Efecto As String
    Public Property Concepto As String
    Public Property Importe As Decimal
    Public Property ImportePendiente As Decimal
    Public Property Vendedor As String
    Public Property Vencimiento As Date
    Public Property CCC As String
    Public Property Ruta As String
    Public Property Estado As String
    Public Property FormaVenta As String
    Public Property Delegacion As String
    Public Property FormaPago As String
    Public Property Usuario As String
    Private _seleccionada As Boolean
    Public Property Seleccionada As Boolean
        Get
            Return _seleccionada
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_seleccionada, value)
        End Set
    End Property

End Class

''' <summary>
''' DTO para pagos TPV (enlaces de pago NestoPago).
''' Issue Nesto#322: Histórico de enlaces de pago en ventana de Clientes.
''' </summary>
Public Class PagoTPVDTO
    Public Property Id As Integer
    Public Property NumeroOrden As String
    Public Property Tipo As String
    Public Property Empresa As String
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Importe As Decimal
    Public Property Descripcion As String
    Public Property Correo As String
    Public Property Movil As String
    Public Property Estado As String
    Public Property CodigoRespuesta As String
    Public Property CodigoAutorizacion As String
    Public Property FechaCreacion As DateTime
    Public Property FechaActualizacion As DateTime?
    Public Property Usuario As String
    Public Property Efectos As List(Of EfectoTPVDTO)
    Public Property PagoOriginalId As Integer?

    Public ReadOnly Property EstadoDescriptivo As String
        Get
            If PagoOriginalId.HasValue Then
                Return Estado & " (reintento)"
            End If
            Return Estado
        End Get
    End Property
End Class

Public Class EfectoTPVDTO
    Public Property Id As Integer
    Public Property ExtractoClienteId As Integer
    Public Property Importe As Decimal
    Public Property Documento As String
    Public Property Efecto As String
End Class