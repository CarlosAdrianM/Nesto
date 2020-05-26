Imports Nesto.Models
Imports System.ComponentModel
Imports System.Windows
Imports System.Collections.ObjectModel
Imports System.Windows.Input
Imports System.IO
Imports Microsoft.Win32
Imports System.Windows.Data
Imports System.Globalization
Imports Nesto.Contratos
Imports Nesto.Models.PedidoVenta
Imports System.Net.Http
Imports Newtonsoft.Json
Imports System.Text
Imports System.Threading.Tasks
Imports System.Runtime.InteropServices
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.PedidoVenta
Imports Microsoft.Practices.Unity

'Imports Nesto.Models.Nesto.Models.EF

Public Interface IOService
    Function OpenFileDialog(defaultPath As String) As String

    'Other similar untestable IO operations
    Function OpenFile(path As String) As Stream
End Interface

Public Class ClientesViewModel
    Inherits ViewModelBase
    '    Implements IActiveAware

    Private Shared DbContext As NestoEntities
    Public Property configuracion As IConfiguracion
    Private ReadOnly Property contenedor As IUnityContainer

    'Dim mainModel As New Nesto.Models.MainModel
    Private ruta As String
    Private esVendedorDeFamilias As Boolean = False


    Public Structure tipoIdDescripcion
        Public Sub New(
       ByVal _id As String,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As String
        Property descripcion As String
    End Structure

    Public Sub New()
        ' Este ViewModel se usa con dos Views y eso es un desastre.
        ' Deberíamos separarlo en dos ViewModels diferentes, uno para Clientes y otro para ClientesComercial
        '***************************
        cargarDatos()
    End Sub

    Public Sub New(configuracion As IConfiguracion, contenedor As IUnityContainer)
        Me.configuracion = configuracion
        Me.contenedor = contenedor
        cargarDatos()
        clienteActivo = Nothing
        'inicializarListaClientesVendedor()
    End Sub

#Region "Propiedades"
    Private _empresaActual As String
    Public Property empresaActual As String
        Get
            Return _empresaActual
        End Get
        Set(value As String)
            _empresaActual = value
            listaContactos = New ObservableCollection(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = empresaActual AndAlso c.Nº_Cliente = clienteActual AndAlso c.Estado >= 0)
            If IsNothing(contactoActual) Then
                CargarClienteActualEmpresa()
                listaContactos = New ObservableCollection(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = empresaActual AndAlso c.Nº_Cliente = clienteActual AndAlso c.Estado >= 0)
            End If
            actualizarCliente(_empresaActual, clienteActual, contactoActual)
            OnPropertyChanged("empresaActual")
        End Set
    End Property

    Private Async Function CargarClienteActualEmpresa() As Task
        Dim mainViewModel = New MainViewModel()
        clienteActual = Await mainViewModel.leerParametro(empresaActual, "UltNumCliente")
    End Function


    Private _clienteActual As String
    Public Property clienteActual As String
        Get
            Return _clienteActual
        End Get
        Set(value As String)
            _clienteActual = value
            listaContactos = New ObservableCollection(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = empresaActual AndAlso c.Nº_Cliente = clienteActual AndAlso c.Estado >= 0)
            actualizarCliente(_empresaActual, _clienteActual, _contactoActual)
            OnPropertyChanged("clienteActual")
            If Not IsNothing(clienteActivo) AndAlso Not IsNothing(clienteActivo.Nº_Cliente) Then
                Titulo = String.Format("Cliente {0}", clienteActivo.Nº_Cliente.Trim)
                OnPropertyChanged("Titulo")
            End If
        End Set
    End Property

    Private _contactoActual As String
    Public Property contactoActual As String
        Get
            Return _contactoActual
        End Get
        Set(value As String)
            _contactoActual = value
            actualizarCliente(_empresaActual, _clienteActual, _contactoActual)
            OnPropertyChanged("contactoActual")
        End Set
    End Property

    Private _clienteServidor As ClienteJson
    Public Property clienteServidor As ClienteJson
        Get
            Return _clienteServidor
        End Get
        Set(value As ClienteJson)
            _clienteServidor = value
            OnPropertyChanged("clienteServidor")
        End Set
    End Property

    Private _listaEmpresas As ObservableCollection(Of Empresas)
    Public Property listaEmpresas As ObservableCollection(Of Empresas)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of Empresas))
            _listaEmpresas = value
            OnPropertyChanged("listaEmpresas")
        End Set
    End Property

    Private _nombre As String
    Public Property nombre As String
        Get
            Return _nombre
        End Get
        Set(value As String)
            _nombre = value
            OnPropertyChanged("nombre")
        End Set
    End Property

    Private _cuentaActiva As CCC
    Public Property cuentaActiva As CCC
        Get
            Return _cuentaActiva
        End Get
        Set(value As CCC)
            _cuentaActiva = value
            OnPropertyChanged("cuentaActiva")
        End Set
    End Property

    Private _cuentasBanco As ObservableCollection(Of CCC)
    Public Property cuentasBanco As ObservableCollection(Of CCC)
        Get
            Return _cuentasBanco
        End Get
        Set(value As ObservableCollection(Of CCC))
            _cuentasBanco = value
            OnPropertyChanged("cuentasBanco")
        End Set
    End Property

    Private Sub actualizarCliente(empresa As String, numCliente As String, contacto As String)
        If Not (IsNothing(empresa) Or IsNothing(numCliente) Or IsNothing(contacto)) Then
            Dim cliente = (From c In DbContext.Clientes Where c.Empresa = empresa And c.Nº_Cliente = numCliente And c.Contacto = contacto).FirstOrDefault
            'If IsNothing(cliente) Then 'Si no existe el cliente que tiene en el parámetro
            '    cliente = (From c In DbContext.Clientes Where c.Empresa = empresa).FirstOrDefault
            '    numCliente = cliente.Nº_Cliente
            '    contacto = cliente.Contacto
            '    clienteActual = numCliente
            '    contactoActual = contacto
            'End If
            If Not IsNothing(cliente) Then
                nombre = cliente.Nombre
                cuentasBanco = New ObservableCollection(Of CCC)(From c In DbContext.CCC Where c.Empresa = empresa And c.Cliente = numCliente And c.Contacto = contacto)
                If Not IsNothing(cliente.CCC2) Then
                    cuentaActiva = cuentasBanco.Where(Function(x) x.Número = cliente.CCC2.Número).FirstOrDefault
                End If
                clienteActivo = cliente
                extractoCCC = Nothing
                pedidosCCC = Nothing
            End If
        End If
    End Sub

    Private Async Sub cargarVendedoresPorGrupo()
        If IsNothing(clienteActivo) Then
            Return
        End If
        ' Calculamos el vendedor de peluquería
        If IsNothing(clienteServidor) OrElse clienteServidor.empresa <> clienteActivo.Empresa OrElse clienteServidor.cliente <> clienteActivo.Nº_Cliente OrElse clienteServidor.contacto <> clienteActivo.Contacto Then
            Using client As New HttpClient
                client.BaseAddress = New Uri(configuracion.servidorAPI)
                Dim response As HttpResponseMessage
                Dim respuesta As String = ""
                Dim vendedorConsulta As String = vendedor


                Try
                    Dim urlConsulta As String = "Clientes"
                    urlConsulta += "?empresa=" + clienteActivo.Empresa
                    urlConsulta += "&cliente=" + clienteActivo.Nº_Cliente
                    urlConsulta += "&contacto=" + clienteActivo.Contacto

                    response = Await client.GetAsync(urlConsulta)

                    If response.IsSuccessStatusCode Then
                        respuesta = Await response.Content.ReadAsStringAsync()
                    Else
                        respuesta = ""
                    End If

                Catch ex As Exception
                    Throw New Exception("No se ha podido recuperar el cliente desde el servidor")
                Finally

                End Try

                clienteServidor = JsonConvert.DeserializeObject(Of ClienteJson)(respuesta)
                If Not IsNothing(clienteServidor) AndAlso Not IsNothing(clienteServidor.VendedoresGrupoProducto) AndAlso clienteServidor.VendedoresGrupoProducto.Count > 0 Then
                    vendedorPorGrupo = clienteServidor.VendedoresGrupoProducto.ElementAt(0).vendedor
                Else
                    vendedorPorGrupo = Nothing
                End If



            End Using
        End If
    End Sub
    Private _clienteActivo As Clientes
    Public Property clienteActivo As Clientes
        Get
            Return _clienteActivo
        End Get
        Set(value As Clientes)
            Dim fechaDesde As Date
            _clienteActivo = value

            If Not IsNothing(listaClientesVendedor) Then
                If Not IsNothing(clienteActivoDTO) Then
                    cargarVendedoresPorGrupo()
                    seguimientosOrdenados = New ObservableCollection(Of SeguimientoCliente)(From c In clienteActivo.SeguimientoCliente Order By c.Fecha Descending Take 20)
                    If rangoFechasVenta = "System.Windows.Controls.ComboBoxItem: Ventas de siempre" Then 'esto está fatal, hay que desacoplarlo de la vista 
                        fechaDesde = DateTime.MinValue
                    Else
                        fechaDesde = DateTime.Now.AddYears(-1)
                    End If
                    listaVentas = New ObservableCollection(Of lineaVentaAgrupada)(From l In DbContext.LinPedidoVta Where (l.Empresa = "1" Or l.Empresa = "3") And l.Nº_Cliente = clienteActivo.Nº_Cliente And l.Contacto = clienteActivo.Contacto And l.Estado >= 2 And l.Fecha_Albarán >= fechaDesde Group By l.Producto, l.Texto, l.SubGruposProducto.Descripción Into Sum(l.Cantidad), Max(l.Fecha_Albarán) Select New lineaVentaAgrupada With {.producto = Producto, .nombre = Texto, .cantidad = Sum, .fechaUltVenta = Max, .subGrupo = Descripción})
                    deudaVencida = (Aggregate c In DbContext.ExtractoCliente Where (c.Empresa = "1" Or c.Empresa = "3") And c.Número = clienteActivo.Nº_Cliente And c.Contacto = clienteActivo.Contacto And c.FechaVto < Now And c.ImportePdte <> 0 Into Sum(CType(c.ImportePdte, Decimal?)))
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
            End If

            If IndiceSeleccionado = 1 Then
                CargarFacturas()
            ElseIf IndiceSeleccionado = 2 Then
                CargarPedidos()
            Else
                ListaFacturas = Nothing
                ListaPedidos = Nothing
            End If

            OnPropertyChanged("clienteActivo")
        End Set
    End Property

    Private _clienteActivoDTO As ClienteJson
    Public Property clienteActivoDTO As ClienteJson
        Get
            Return _clienteActivoDTO
        End Get
        Set(value As ClienteJson)
            _clienteActivoDTO = value
            If Not IsNothing(clienteActivoDTO) Then
                clienteActivo = DbContext.Clientes.SingleOrDefault(Function(c) c.Empresa = clienteActivoDTO.empresa AndAlso c.Nº_Cliente = clienteActivoDTO.cliente AndAlso c.Contacto = clienteActivoDTO.contacto)
            Else
                clienteActivo = Nothing
            End If

            OnPropertyChanged("clienteActivoDTO")
        End Set
    End Property

    Private Property _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            _mensajeError = value
            OnPropertyChanged("mensajeError")
        End Set
    End Property

    Private _selectedPath As String
    Public Property selectedPath() As String
        Get
            Return _selectedPath
        End Get
        Set(value As String)
            _selectedPath = value
            OnPropertyChanged("selectedPath")
        End Set
    End Property

    Private Property _listaEstadosCCC As ObservableCollection(Of EstadosCCC)
    Public Property listaEstadosCCC As ObservableCollection(Of EstadosCCC)
        Get
            Return _listaEstadosCCC
        End Get
        Set(value As ObservableCollection(Of EstadosCCC))
            _listaEstadosCCC = value
            OnPropertyChanged("listaEstadosCCC")
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
            If vendedor <> "" Then
                esVendedorDeFamilias = Not IsNothing((From c In DbContext.FamiliasVendedor Where c.Empresa = empresaActual And c.Vendedor = vendedor Take 1).FirstOrDefault)
            End If

            If esVendedorDeFamilias Then
                listaCodigosPostalesVendedor = (From c In DbContext.FamiliasVendedor Where c.Empresa = empresaActual And c.Vendedor = vendedor Group By codPostal = c.CodigoPostal Into susClientes = Group Select codPostal).ToList
            End If

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
                clienteServidor.VendedoresGrupoProducto.Add(New VendedorGrupoProductoDTO With
                                       {
                                       .grupoProducto = "PEL",
                                       .vendedor = "NV",
                                       .usuario = configuracion.usuario
                                       }
                )
            End If
            clienteServidor.VendedoresGrupoProducto.ElementAt(0).vendedor = value
            OnPropertyChanged("vendedorPorGrupo")
        End Set
    End Property

    Private _listaClientesVendedor As ObservableCollection(Of ClienteJson)
    Public Property listaClientesVendedor As ObservableCollection(Of ClienteJson)
        Get
            Return _listaClientesVendedor
        End Get
        Set(value As ObservableCollection(Of ClienteJson))
            _listaClientesVendedor = value
            'clienteActivoDTO = _listaClientesVendedor.FirstOrDefault
            OnPropertyChanged("listaClientesVendedor")
        End Set
    End Property

    Private _seguimientosOrdenados As ObservableCollection(Of SeguimientoCliente)
    Public Property seguimientosOrdenados As ObservableCollection(Of SeguimientoCliente)
        Get
            'If IsNothing(_seguimientosOrdenados) Then
            '    If Not IsNothing(clienteActivo) Then
            '        _seguimientosOrdenados = New ObservableCollection(Of SeguimientoCliente)(From c In clienteActivo.SeguimientoCliente Order By c.Fecha Descending Take 20)
            '    Else
            '        _seguimientosOrdenados = Nothing
            '    End If
            'End If
            Return _seguimientosOrdenados
        End Get
        Set(value As ObservableCollection(Of SeguimientoCliente))
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

    Private _filtro As String
    Public Property filtro As String
        Get
            Return _filtro
        End Get
        Set(value As String)
            _filtro = value
            actualizarFiltro(filtro)
        End Set
    End Property

    Public Sub actualizarFiltro(filtro As String)
        If IsNothing(filtro) Then
            Return
        End If

        If IsNothing(listaClientesVendedor) Then
            inicializarListaClientesVendedor(filtro)
            Return
        End If

        If filtro.Trim <> "" Then
            listaClientesVendedor = New ObservableCollection(Of ClienteJson) _
            (From c In listaClientesVendedor Where
                                                 (Not IsNothing(c.cliente) AndAlso c.cliente.ToLower.Trim = filtro.ToLower) OrElse
                (Not IsNothing(c.direccion) AndAlso c.direccion.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(c.nombre) AndAlso c.nombre.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(c.telefono) AndAlso c.telefono.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(c.cifNif) AndAlso c.cifNif.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(c.poblacion) AndAlso c.poblacion.ToLower.Contains(filtro.ToLower)) OrElse
                (Not IsNothing(c.comentarios) AndAlso c.comentarios.ToLower.Contains(filtro.ToLower)))
        Else
            inicializarListaClientesVendedor(filtro)
            filtro = ""
        End If

        OnPropertyChanged("filtro")
    End Sub

    Private _rangoFechasVenta As String
    Public Property rangoFechasVenta As String
        Get
            Return _rangoFechasVenta
        End Get
        Set(value As String)
            Dim fechaDesde As Date
            _rangoFechasVenta = value
            If Not IsNothing(clienteActivo) Then
                If rangoFechasVenta = "System.Windows.Controls.ComboBoxItem: Ventas de siempre" Then 'esto hay que cambiarlo, que es una ñapa muy gorda
                    fechaDesde = DateTime.MinValue
                Else
                    fechaDesde = DateTime.Now.AddYears(-1)
                End If
                listaVentas = New ObservableCollection(Of lineaVentaAgrupada)(From l In DbContext.LinPedidoVta Where (l.Empresa = "1" Or l.Empresa = "3") And l.Nº_Cliente = clienteActivo.Nº_Cliente And l.Contacto = clienteActivo.Contacto And l.Estado >= 2 And l.Fecha_Albarán >= fechaDesde Group By l.Producto, l.Texto, l.SubGruposProducto.Descripción Into Sum(l.Cantidad), Max(l.Fecha_Albarán) Select New lineaVentaAgrupada With {.producto = Producto, .nombre = Texto, .cantidad = Sum, .fechaUltVenta = Max, .subGrupo = Descripción})
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

    Private _listaSecuencias As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaSecuencias As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaSecuencias
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaSecuencias = value
            OnPropertyChanged("listaSecuencias")
        End Set
    End Property

    Private _listaTipos As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaTipos As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaTipos
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            _listaTipos = value
            OnPropertyChanged("listaTipos")
        End Set
    End Property

    Private _listaCodigosPostalesVendedor As List(Of String)
    Public Property listaCodigosPostalesVendedor As List(Of String)
        Get
            Return _listaCodigosPostalesVendedor
        End Get
        Set(value As List(Of String))
            _listaCodigosPostalesVendedor = value
        End Set
    End Property

    Private _extractoCCC As ObservableCollection(Of ExtractoCliente)
    Public Property extractoCCC() As ObservableCollection(Of ExtractoCliente)
        Get
            Return _extractoCCC
        End Get
        Set(ByVal value As ObservableCollection(Of ExtractoCliente))
            _extractoCCC = value
            OnPropertyChanged("extractoCCC")
            OnPropertyChanged("estaVisibleExtractoCCC")
        End Set
    End Property

    Private _pedidosCCC As ObservableCollection(Of cabeceraPedidoAgrupada)
    Public Property pedidosCCC() As ObservableCollection(Of cabeceraPedidoAgrupada)
        Get
            Return _pedidosCCC
        End Get
        Set(ByVal value As ObservableCollection(Of cabeceraPedidoAgrupada))
            _pedidosCCC = value
            OnPropertyChanged("pedidosCCC")
            OnPropertyChanged("estaVisiblePedidosCCC")
        End Set
    End Property

    Private _listaContactos As ObservableCollection(Of Clientes)
    Public Property listaContactos() As ObservableCollection(Of Clientes)
        Get
            Return _listaContactos
        End Get
        Set(ByVal value As ObservableCollection(Of Clientes))
            _listaContactos = value
            OnPropertyChanged("listaContactos")
            If Not IsNothing(value.FirstOrDefault) Then
                contactoActual = value.FirstOrDefault.Contacto
                OnPropertyChanged("contactoActual")
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
            OnPropertyChanged("estaOcupado")
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

    Private _listaFacturas As ObservableCollection(Of ExtractoClienteDTO)
    Public Property ListaFacturas As ObservableCollection(Of ExtractoClienteDTO)
        Get
            Return _listaFacturas
        End Get
        Set(value As ObservableCollection(Of ExtractoClienteDTO))
            _listaFacturas = value
            OnPropertyChanged("ListaFacturas")
        End Set
    End Property

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
            End If
            OnPropertyChanged("IndiceSeleccionado")
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
    Private Function CanGuardar(ByVal param As Object) As Boolean
        Return DbContext.ChangeTracker.HasChanges()
    End Function
    Private Sub Guardar(ByVal param As Object)
        Try
            'Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified)

            extractoCCC = New ObservableCollection(Of ExtractoCliente)(From e In DbContext.ExtractoCliente Where e.Empresa = empresaActual AndAlso e.Número = clienteActual AndAlso e.Contacto = contactoActual AndAlso e.ImportePdte <> 0 AndAlso e.CCC <> cuentaActiva.Número)
            pedidosCCC = New ObservableCollection(Of cabeceraPedidoAgrupada)((From p In DbContext.CabPedidoVta Join l In DbContext.LinPedidoVta On p.Empresa Equals l.Empresa And p.Número Equals l.Número Where p.Empresa = empresaActual AndAlso p.Nº_Cliente = clienteActual AndAlso (p.CCC <> cuentaActiva.Número Or p.CCC Is Nothing) AndAlso l.Estado >= -1 AndAlso l.Estado <= 1 Select New cabeceraPedidoAgrupada With {.CCC = p.CCC,
                .Fecha = p.Fecha,
                .Numero = p.Número}).Distinct)

            ' hay que comprobar que no se queden dos CCC activos del mismo cliente

            DbContext.SaveChanges()
            mensajeError = ""
        Catch ex As Exception
            mensajeError = ex.InnerException.Message
        End Try

    End Sub

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
        ElseIf Not My.Computer.FileSystem.FileExists(rutaMandato) Then
            Return False
        Else
            Return True
        End If
    End Function
    Private Sub VerMandato(ByVal param As Object)
        Try
            Dim fileName As String = rutaMandato()
            Dim process As System.Diagnostics.Process = New System.Diagnostics.Process
            process.StartInfo.FileName = fileName
            process.Start()
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
    Private Sub NuevoMandato(ByVal param As Object)
        Try
            Dim siguienteNumero As Integer
            'siguienteNumero = 1
            'If Not IsNothing(cuentaActiva) Then
            If cuentasBanco.Count = 0 Then
                siguienteNumero = 1
            Else
                'siguienteNumero = CInt(cuentasBanco.Where(Function(x) x.Cliente.Trim = clienteActual).LastOrDefault.Número) + 1
                siguienteNumero = CInt(cuentasBanco.Where(Function(x) x.Cliente.Trim = clienteActual).OrderBy(Function(x) x.Número).LastOrDefault.Número) + 1
            End If
            'End If
            cuentaActiva = New CCC With {
                .Empresa = empresaActual,
                .Cliente = clienteActual,
                .Contacto = contactoActual,
                .Número = siguienteNumero,
                .Estado = 0,
                .Secuencia = "FRST"
            }
            cuentasBanco.Add(cuentaActiva)
            DbContext.CCC.Add(cuentaActiva)
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
        Dim elegirFichero = New OpenFileDialog
        elegirFichero.Filter = "pdf files (*.pdf)|*.pdf|All files (*.*)|*.*"
        elegirFichero.FilterIndex = 1
        elegirFichero.RestoreDirectory = True

        If elegirFichero.ShowDialog() Then
            Try
                selectedPath = elegirFichero.FileName
                My.Computer.FileSystem.CopyFile(
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
        Try

            ' PUT de clienteServidor
            Using client As New HttpClient
                client.BaseAddress = New Uri(configuracion.servidorAPI)
                Dim response As HttpResponseMessage
                Dim respuesta As String = ""

                Dim urlConsulta As String = "Clientes/ClienteComercial"
                Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(clienteServidor), Encoding.UTF8, "application/json")

                response = client.PutAsync(urlConsulta, content).Result

                If response.IsSuccessStatusCode Then
                    respuesta = response.Content.ReadAsStringAsync().Result
                Else
                    Throw New Exception(response.Content.ReadAsStringAsync().Result)
                End If
            End Using

            mensajeError = "Cliente guardado correctamente"
        Catch ex As Exception
            If Not IsNothing(ex.InnerException) Then
                mensajeError = ex.InnerException.Message
            Else
                mensajeError = ex.Message
            End If
        End Try

    End Sub

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
        Return ListaFacturas IsNot Nothing AndAlso ListaFacturas.Where(Function(f) f.Seleccionada).FirstOrDefault IsNot Nothing
    End Function
    Private Async Sub DescargarFacturas(ByVal param As Object)
        estaOcupado = True
        Try
            Dim np As IntPtr
            SHGetKnownFolderPath(New Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, np)
            Dim path As String = Marshal.PtrToStringUni(np)
            Marshal.FreeCoTaskMem(np)

            For Each fra In ListaFacturas.Where(Function(f) f.Seleccionada)
                Dim factura As Byte() = Await CargarFactura(fra.Empresa, fra.Documento)
                Dim ms As New MemoryStream(factura)
                'write to file
                Dim file As New FileStream(path + "\Cliente_" + fra.Cliente + "_" + fra.Documento + ".pdf", FileMode.Create, FileAccess.Write)
                ms.WriteTo(file)
                file.Close()
                ms.Close()
            Next

            ' Abrimos la carpeta de descargas
            Process.Start(path)
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
        PedidoVentaViewModel.cargarPedido(empresaActual, param.numero, contenedor)
    End Sub


    Private Async Function CargarFactura(empresa As String, numeroFactura As String) As Task(Of Byte())
        If IsNothing(clienteActivo) Then
            Return Nothing
        End If


        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As Byte()

            Try
                Dim urlConsulta As String = "Facturas"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&numeroFactura=" + numeroFactura


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
        If IsNothing(clienteActivo) OrElse (ListaFacturas IsNot Nothing AndAlso clienteActivo.Nº_Cliente?.Trim() = ListaFacturas.FirstOrDefault()?.Cliente) Then
            Return
        End If


        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "ExtractosCliente"
                urlConsulta += "?empresa=" + clienteActivo.Empresa
                urlConsulta += "&cliente=" + clienteActivo.Nº_Cliente
                urlConsulta += "&tipoApunte=1"
                urlConsulta += "&fechaDesde=" + DateTime.Today.AddMonths(-6).ToString("s")
                urlConsulta += "&fechaHasta=" + DateTime.Today.ToString("s")

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

    Private Async Sub CargarPedidos()
        If IsNothing(clienteActivo) OrElse (ListaPedidos IsNot Nothing AndAlso clienteActivo.Nº_Cliente?.Trim() = ListaPedidos.FirstOrDefault()?.cliente) Then
            Return
        End If


        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "PedidosVenta"
                Dim mainViewModel As New MainViewModel
                urlConsulta += "?vendedor=" + Await mainViewModel.leerParametro(empresaActual, "Vendedor")
                urlConsulta += "&cliente=" + clienteActivo.Nº_Cliente

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
            listaClientesVendedor = Nothing
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage


            Try
                estaOcupado = True
                If vendedor = "" Then
                    response = Await client.GetAsync("Clientes?empresa=1&filtro=" + filtro)
                Else
                    response = Await client.GetAsync("Clientes?empresa=1&vendedor=" + vendedor + "&filtro=" + filtro)
                End If


                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaClientesVendedor = JsonConvert.DeserializeObject(Of ObservableCollection(Of ClienteJson))(cadenaJson)
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
        DbContext = New NestoEntities
        listaEmpresas = New ObservableCollection(Of Empresas)(From c In DbContext.Empresas)

        Dim mainViewModel As New MainViewModel
        Dim empresaDefecto As String = Await mainViewModel.leerParametro("1", "EmpresaPorDefecto")
        empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
        ruta = Await mainViewModel.leerParametro(empresaActual, "RutaMandatos")
        Dim clienteDefecto As String = Await mainViewModel.leerParametro(empresaActual, "UltNumCliente")
        vendedor = Await mainViewModel.leerParametro(empresaDefecto, "Vendedor")


        clienteActual = clienteDefecto
        'contactoActual = "0  " 'esto hay que cambiarlo por el ClientePrincipal

        listaEstadosCCC = New ObservableCollection(Of EstadosCCC)(From c In DbContext.EstadosCCC Where c.Empresa = empresaActual)

        Dim rangosFechas As New List(Of String)
        rangosFechas.Add("Ventas del Último Año")
        rangosFechas.Add("Ventas de Siempre")

        If clienteActivo IsNot Nothing Then
            deudaVencida = (Aggregate c In DbContext.ExtractoCliente Where (c.Empresa = "1" Or c.Empresa = "3") And c.Número = clienteActivo.Nº_Cliente And c.Contacto = clienteActivo.Contacto And c.FechaVto < Now And c.ImportePdte <> 0 Into Sum(CType(c.ImportePdte, Decimal?)))
        Else
            deudaVencida = 0
        End If


        listaSecuencias = New ObservableCollection(Of tipoIdDescripcion)
        listaSecuencias.Add(New tipoIdDescripcion("FRST", "Primer adeudo recurrente"))
        listaSecuencias.Add(New tipoIdDescripcion("RCUR", "Resto de adeudos recurrentes"))
        listaSecuencias.Add(New tipoIdDescripcion("OOFF", "Operación de un único pago"))
        listaSecuencias.Add(New tipoIdDescripcion("FNAL", "Último adeudo recurrente"))

        listaTipos = New ObservableCollection(Of tipoIdDescripcion)
        listaTipos.Add(New tipoIdDescripcion(1, "Consumidor final"))
        listaTipos.Add(New tipoIdDescripcion(2, "Profesional"))

        Titulo = "Clientes"
    End Sub
    <DllImport("shell32")>
    Private Shared Function SHGetKnownFolderPath(ByRef rfid As Guid, ByVal dwFlags As UInt32, ByVal hToken As IntPtr, ByRef np As IntPtr) As Int32 : End Function
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

Public Class lineaVentaAgrupada
    Private _producto As String
    Public Property producto As String
        Get
            Return _producto
        End Get
        Set(value As String)
            _producto = value
        End Set
    End Property

    Private _nombre As String
    Public Property nombre As String
        Get
            Return _nombre
        End Get
        Set(value As String)
            _nombre = value
        End Set
    End Property

    Private _cantidad As Integer
    Public Property cantidad As Integer
        Get
            Return _cantidad
        End Get
        Set(value As Integer)
            _cantidad = value
        End Set
    End Property

    Private _fechaUltVenta As Date
    Public Property fechaUltVenta As Date
        Get
            Return _fechaUltVenta
        End Get
        Set(value As Date)
            _fechaUltVenta = value
        End Set
    End Property

    Private _subGrupo As String
    Public Property subGrupo As String
        Get
            Return _subGrupo
        End Get
        Set(value As String)
            _subGrupo = value
        End Set
    End Property
End Class

Public Class cabeceraPedidoAgrupada
    Public Property Numero As Integer
    Public Property Fecha As Date
    Public Property CCC As String
End Class

Public Class ExtractoClienteDTO
    Public Property Id As Integer
    Public Property Empresa As String
    Public Property Asiento As Integer
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Fecha As DateTime
    Public Property Tipo As String
    Public Property Documento As String
    Public Property Efecto As String
    Public Property Concepto As String
    Public Property Importe As Decimal
    Public Property ImportePendiente As Decimal
    Public Property Vendedor As String
    Public Property Vencimiento As DateTime
    Public Property CCC As String
    Public Property Ruta As String
    Public Property Estado As String

    Public Property Seleccionada As Boolean

End Class