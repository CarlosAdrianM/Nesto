Imports System.Net.Http
Imports System.Collections.ObjectModel
Imports Newtonsoft.Json
Imports Nesto.Modulos.PlantillaVenta.PlantillaVentaModel
Imports System.Text
Imports Nesto.Models.PedidoVenta
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Newtonsoft.Json.Linq
Imports Xceed.Wpf.Toolkit
Imports Prism.Events
Imports Nesto.Models.Nesto.Models
Imports Prism.Regions
Imports Prism.Commands
Imports Unity
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs
Imports Prism.Mvvm
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports System.Text.Encodings.Web
Imports ControlesUsuario.Models

Public Class PlantillaVentaViewModel
    Inherits BindableBase
    Implements INavigationAware

    Public Property configuracion As IConfiguracion
    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly servicio As IPlantillaVentaService
    Private ReadOnly eventAggregator As IEventAggregator
    Private ReadOnly dialogService As IDialogService
    Public Property Titulo As String
    Private Const ESTADO_LINEA_CURSO As Integer = 1
    Private Const ESTADO_LINEA_PRESUPUESTO As Integer = -3

    Private Const PAGINA_SELECCION_CLIENTE As String = "SeleccionCliente"
    Private Const PAGINA_SELECCION_PRODUCTOS As String = "SeleccionProductos"
    Private Const PAGINA_SELECCION_ENTREGA As String = "SeleccionEntrega"
    Private Const PAGINA_FINALIZAR As String = "Finalizar"


    Dim formaVentaPedido, delegacionUsuario, almacenRutaUsuario, iva, vendedorUsuario As String
    Dim ultimoClienteAbierto As String = ""

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IPlantillaVentaService, eventAggregator As IEventAggregator, dialogService As IDialogService)
        Me.configuracion = configuracion
        Me.container = container
        Me.regionManager = regionManager
        Me.servicio = servicio
        Me.eventAggregator = eventAggregator
        Me.dialogService = dialogService

        Titulo = "Plantilla Ventas"

        cmdAbrirPlantillaVenta = New DelegateCommand(Of Object)(AddressOf OnAbrirPlantillaVenta, AddressOf CanAbrirPlantillaVenta)
        cmdActualizarPrecioProducto = New DelegateCommand(Of Object)(AddressOf OnActualizarPrecioProducto, AddressOf CanActualizarPrecioProducto)
        cmdActualizarProductosPedido = New DelegateCommand(Of LineaPlantillaJson)(AddressOf OnActualizarProductosPedido, AddressOf CanActualizarProductosPedido)
        cmdBuscarEnTodosLosProductos = New DelegateCommand(Of String)(AddressOf OnBuscarEnTodosLosProductos, AddressOf CanBuscarEnTodosLosProductos)
        cmdCargarClientesVendedor = New DelegateCommand(AddressOf OnCargarClientesVendedor, AddressOf CanCargarClientesVendedor)
        cmdCargarFormasPago = New DelegateCommand(Of Object)(AddressOf OnCargarFormasPago, AddressOf CanCargarFormasPago)
        cmdCargarFormasVenta = New DelegateCommand(Of Object)(AddressOf OnCargarFormasVenta, AddressOf CanCargarFormasVenta)
        cmdCargarPlazosPago = New DelegateCommand(Of Object)(AddressOf OnCargarPlazosPago, AddressOf CanCargarPlazosPago)
        CargarProductoCommand = New DelegateCommand(Of Object)(AddressOf OnCargarProducto, AddressOf CanCargarProducto)
        cmdCargarProductosPlantilla = New DelegateCommand(AddressOf OnCargarProductosPlantilla)
        cmdCargarStockProducto = New DelegateCommand(Of Object)(AddressOf OnCargarStockProducto, AddressOf CanCargarStockProducto)
        cmdCargarUltimasVentas = New DelegateCommand(Of Object)(AddressOf OnCargarUltimasVentas, AddressOf CanCargarUltimasVentas)
        cmdComprobarPendientes = New DelegateCommand(AddressOf OnComprobarPendientes)
        cmdCrearPedido = New DelegateCommand(AddressOf OnCrearPedido, AddressOf CanCrearPedido)
        cmdInsertarProducto = New DelegateCommand(Of Object)(AddressOf OnInsertarProducto, AddressOf CanInsertarProducto)
        cmdCalcularSePuedeServirPorGlovo = New DelegateCommand(AddressOf OnCalcularSePuedeServirPorGlovo)
        CambiarIvaCommand = New DelegateCommand(AddressOf OnCambiarIva)
        CargarCorreoYMovilTarjeta = New DelegateCommand(AddressOf OnCargarCorreoYMovilTarjeta)
        NoAmpliarPedidoCommand = New DelegateCommand(AddressOf OnNoAmpliarPedido, AddressOf CanNoAmpliarPedido)
        SoloConStockCommand = New DelegateCommand(AddressOf OnSoloConStock, AddressOf CanSoloConStock)
        CopiarClientePortapapelesCommand = New DelegateCommand(AddressOf OnCopiarClientePortapapeles)

        ' Al leer los clientes lo lee del parámetro PermitirVerClientesTodosLosVendedores
        todosLosVendedores = False

        listaAlmacenes = New ObservableCollection(Of tipoAlmacen)
        almacenSeleccionado = New tipoAlmacen("ALG", "Algete")
        listaAlmacenes.Add(almacenSeleccionado)
        listaAlmacenes.Add(New tipoAlmacen("REI", "Reina"))

        'ListaFiltrosProducto = New ObservableCollection(Of String)
        ListaFiltrableProductos = New ColeccionFiltrable(New ObservableCollection(Of LineaPlantillaJson)) With {
            .TieneDatosIniciales = True
        }
        AddHandler ListaFiltrableProductos.FiltroChanged, Sub(sender As Object, args As EventArgs)
                                                              'listaProductos = AplicarFiltro(ListaFiltrable.Filtro)
                                                              cmdBuscarEnTodosLosProductos.RaiseCanExecuteChanged()
                                                          End Sub
        AddHandler ListaFiltrableProductos.HayQueCargarDatos, Async Sub()
                                                                  Await Task.Delay(400)
                                                                  cmdBuscarEnTodosLosProductos.Execute(ListaFiltrableProductos.Filtro)
                                                              End Sub
        AddHandler ListaFiltrableProductos.ListaChanged, Sub()
                                                             RaisePropertyChanged(NameOf(listaProductosPedido))
                                                             RaisePropertyChanged(NameOf(baseImponiblePedido))
                                                             RaisePropertyChanged(NameOf(totalPedido))
                                                             SoloConStockCommand.RaiseCanExecuteChanged()
                                                         End Sub
        AddHandler ListaFiltrableProductos.ElementoSeleccionadoChanged, Sub(sender As Object, args As EventArgs)
                                                                            cmdCargarUltimasVentas.Execute(ListaFiltrableProductos.ElementoSeleccionado)
                                                                        End Sub


        eventAggregator.GetEvent(Of ClienteCreadoEvent).Subscribe(AddressOf ActualizarCliente)
    End Sub

    Private Sub ActualizarCliente(cliente As Clientes)
        Dim clienteEncontrado As ClienteJson = listaClientes?.SingleOrDefault(Function(c) c.empresa = cliente.Empresa.Trim() AndAlso c.cliente = cliente.Nº_Cliente.Trim() AndAlso c.contacto = cliente.Contacto.Trim())
        If Not IsNothing(clienteEncontrado) Then
            clienteEncontrado.nombre = cliente.Nombre
            clienteEncontrado.direccion = cliente.Dirección
            clienteEncontrado.poblacion = cliente.Población
            clienteEncontrado.comentarios = cliente.Comentarios
            clienteEncontrado.estado = cliente.Estado
            clienteEncontrado.cifNif = cliente.CIF_NIF
        End If
    End Sub


#Region "Propiedades"
    '*** Propiedades de Nesto
    Private vendedor As String
    Private ultimaOferta As Integer = 0

    Private _almacenSeleccionado As tipoAlmacen
    Public Property almacenSeleccionado As tipoAlmacen
        Get
            Return _almacenSeleccionado
        End Get
        Set(value As tipoAlmacen)
            SetProperty(_almacenSeleccionado, value)
            If Not IsNothing(ListaFiltrableProductos) AndAlso Not IsNothing(ListaFiltrableProductos.Lista) Then
                Application.Current.Dispatcher.Invoke(New Action(Async Sub()
                                                                     estaOcupado = True
                                                                     Dim listaCast As ObservableCollection(Of LineaPlantillaJson) = New ObservableCollection(Of LineaPlantillaJson)
                                                                     For Each linea In ListaFiltrableProductos.ListaOriginal
                                                                         listaCast.Add(linea)
                                                                     Next
                                                                     Dim nuevosStocks As ObservableCollection(Of LineaPlantillaJson) = Await servicio.PonerStocks(listaCast, value.id)
                                                                     ListaFiltrableProductos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(nuevosStocks)
                                                                     estaOcupado = False
                                                                 End Sub))
            End If
        End Set
    End Property


    Public ReadOnly Property baseImponiblePedido As Decimal
        Get
            Dim baseImponible As Decimal = 0
            If (Not IsNothing(listaProductosPedido) AndAlso listaProductosPedido.Count > 0) Then
                baseImponible = listaProductosPedido.Sum(Function(l) l.cantidad * l.precio - Math.Round(l.cantidad * l.precio * l.descuento, 2, MidpointRounding.AwayFromZero))
            End If
            RaisePropertyChanged(NameOf(baseImponibleParaPortes))
            Return baseImponible
        End Get
    End Property

    Public ReadOnly Property baseImponibleParaPortes As Decimal
        Get
            Dim baseImponible As Decimal = 0
            If (Not IsNothing(listaProductosPedido) AndAlso listaProductosPedido.Count > 0) Then
                baseImponible = listaProductosPedido.Where(Function(l) l.esSobrePedido = False).Sum(Function(l) l.cantidad * l.precio - Math.Round(l.precio * l.descuento * l.cantidad, 2, MidpointRounding.AwayFromZero))
            End If
            Return baseImponible
        End Get
    End Property

    Private _clienteSeleccionado As ClienteJson
    Public Property clienteSeleccionado As ClienteJson
        Get
            Return _clienteSeleccionado
        End Get
        Set(ByVal value As ClienteJson)
            If Not IsNothing(value) AndAlso String.IsNullOrWhiteSpace(value.cifNif) Then
                If ultimoClienteAbierto = value.cliente Then
                    Dim mensajeError As String = String.Format("A este cliente le faltan datos. Si continua es{0}posible que no pueda finalizar el pedido. Elija entre rellenar los{0}datos que faltan (Cancel) o continuar con el pedido (OK)", Environment.NewLine)
                    Dim continuar As Boolean
                    dialogService.ShowConfirmation("Faltan datos en el cliente", mensajeError, Sub(r)
                                                                                                   continuar = (r.Result = ButtonResult.OK)
                                                                                               End Sub)
                    If continuar Then
                        SeleccionarElCliente(value)
                    Else
                        NavegarAClienteCrear(value)
                    End If
                Else
                    ultimoClienteAbierto = value.cliente
                    NavegarAClienteCrear(value)
                End If
            ElseIf Not IsNothing(value) Then
                SeleccionarElCliente(value)
            End If
            RaisePropertyChanged(NameOf(SePuedeFinalizar))
        End Set
    End Property

    Private _cobroTarjetaCorreo As String
    Public Property CobroTarjetaCorreo As String
        Get
            Return _cobroTarjetaCorreo
        End Get
        Set(value As String)
            SetProperty(_cobroTarjetaCorreo, value)
        End Set
    End Property

    Private _cobroTarjetaMovil As String
    Public Property CobroTarjetaMovil As String
        Get
            Return _cobroTarjetaMovil
        End Get
        Set(value As String)
            SetProperty(_cobroTarjetaMovil, value)
        End Set
    End Property

    Private Sub SeleccionarElCliente(value As ClienteJson)
        SetProperty(_clienteSeleccionado, value)
        RaisePropertyChanged(NameOf(hayUnClienteSeleccionado))
        Titulo = String.Format("Plantilla Ventas ({0})", value.cliente)
        cmdCargarProductosPlantilla.Execute()
        cmdComprobarPendientes.Execute()
        iva = clienteSeleccionado.iva
        PaginaActual = PaginasWizard.Where(Function(p) p.Name = PAGINA_SELECCION_PRODUCTOS).First
        RaisePropertyChanged(NameOf(clienteSeleccionado))
    End Sub

    Private Sub NavegarAClienteCrear(value As ClienteJson)
        Dim parameters As NavigationParameters = New NavigationParameters()
        parameters.Add("empresaParameter", value.empresa)
        parameters.Add("clienteParameter", value.cliente)
        parameters.Add("contactoParameter", value.contacto)
        regionManager.RequestNavigate("MainRegion", "CrearClienteView", parameters)
    End Sub

    Private _direccionEntregaSeleccionada As DireccionesEntregaCliente
    Public Property direccionEntregaSeleccionada As DireccionesEntregaCliente
        Get
            Return _direccionEntregaSeleccionada
        End Get
        Set(ByVal value As DireccionesEntregaCliente)
            SetProperty(_direccionEntregaSeleccionada, value)
            If fechaMinimaEntrega > fechaEntrega Then
                fechaEntrega = fechaMinimaEntrega
            End If
            RaisePropertyChanged(NameOf(textoFacturacionElectronica))
            RaisePropertyChanged(NameOf(fechaMinimaEntrega))
            ' Se hace así para que coja la fecha de hoy cuando se pueda
            ' Si lo hacemos en otro orden, da error porque ponemos una fecha
            ' menor a la que nos permite el datapicker
            If fechaMinimaEntrega < fechaEntrega Then
                fechaEntrega = fechaMinimaEntrega
            End If
        End Set
    End Property

    Private _direccionGoogleMaps As String
    Public Property DireccionGoogleMaps As String
        Get
            Return _direccionGoogleMaps
        End Get
        Set(value As String)
            SetProperty(_direccionGoogleMaps, value)
        End Set
    End Property

    Private _enviarPorGlovo As Boolean
    Public Property EnviarPorGlovo As Boolean
        Get
            Return _enviarPorGlovo
        End Get
        Set(value As Boolean)
            SetProperty(_enviarPorGlovo, value)
            If _enviarPorGlovo Then
                almacenSeleccionado = listaAlmacenes.Single(Function(a) a.id = "REI")
            End If
        End Set
    End Property

    Private _esPresupuesto As Boolean
    Public Property EsPresupuesto As Boolean
        Get
            Return _esPresupuesto
        End Get
        Set(value As Boolean)
            SetProperty(_esPresupuesto, value)
            RaisePropertyChanged(NameOf(SePuedeFinalizar))
        End Set
    End Property
    Public ReadOnly Property EsTarjetaPrepago As Boolean
        Get
            Return Not IsNothing(formaPagoSeleccionada) AndAlso Not IsNothing(plazoPagoSeleccionado) AndAlso
                formaPagoSeleccionada.formaPago = Constantes.FormasPago.TARJETA AndAlso
                plazoPagoSeleccionado.plazoPago = Constantes.PlazosPago.PREPAGO
        End Get
    End Property


    Private _estaOcupado As Boolean = False
    Public Property estaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaOcupado, value)
        End Set
    End Property

    Private _fechaEntrega As DateTime = DateTime.MinValue
    Public Property fechaEntrega As DateTime
        Get
            If _fechaEntrega < fechaMinimaEntrega Then
                _fechaEntrega = fechaMinimaEntrega
            End If
            Return _fechaEntrega
        End Get
        Set(ByVal value As DateTime)
            If value < fechaMinimaEntrega Then
                value = fechaMinimaEntrega
            End If
            SetProperty(_fechaEntrega, value)
        End Set
    End Property

    Public ReadOnly Property fechaMinimaEntrega As DateTime
        Get
            Dim fechaMinima As DateTime = IIf(Now.Hour < 11, Today, Today.AddDays(1))
            If Not IsNothing(direccionEntregaSeleccionada) AndAlso Not IsNothing(direccionEntregaSeleccionada.ruta) Then
                If direccionEntregaSeleccionada.ruta <> "FW" AndAlso
                    direccionEntregaSeleccionada.ruta <> "00" AndAlso
                    direccionEntregaSeleccionada.ruta <> "16" AndAlso
                    direccionEntregaSeleccionada.ruta <> "AT" AndAlso
                    direccionEntregaSeleccionada.ruta <> "OT" Then
                    fechaMinima = Today
                End If
            End If
            Return fechaMinima
        End Get
    End Property

    Private _filtroCliente As String
    Public Property filtroCliente As String
        Get
            Return _filtroCliente
        End Get
        Set(ByVal value As String)
            SetProperty(_filtroCliente, value.ToLower)
            If Not IsNothing(listaClientes) Then
                listaClientes = New ObservableCollection(Of ClienteJson)(From l In listaClientesOriginal Where
                    ((l.nombre IsNot Nothing) AndAlso l.nombre.ToLower.Contains(filtroCliente)) OrElse
                    ((l.direccion IsNot Nothing) AndAlso l.direccion.ToLower.Contains(filtroCliente)) OrElse
                    ((l.telefono IsNot Nothing) AndAlso l.telefono.Contains(filtroCliente)) OrElse
                    ((l.poblacion IsNot Nothing) AndAlso l.poblacion.ToLower.Contains(filtroCliente))
                )
            End If
        End Set
    End Property

    'Private Function AplicarFiltro(filtro As String) As ObservableCollection(Of LineaPlantillaJson)
    '    If Not IsNothing(listaProductosFijada) Then
    '        Return New ObservableCollection(Of LineaPlantillaJson)(From l In listaProductosFijada Where
    '                         l IsNot Nothing AndAlso
    '                         (((l.producto IsNot Nothing) AndAlso (l.producto.ToLower.Contains(filtro)))) OrElse
    '                         (((l.texto IsNot Nothing) AndAlso (l.texto.ToLower.Contains(filtro)))) OrElse
    '                         (((l.familia IsNot Nothing) AndAlso (l.familia.ToLower.Contains(filtro)))) OrElse
    '                         (((l.subGrupo IsNot Nothing) AndAlso (l.subGrupo.ToLower.Contains(filtro))))
    '                    )
    '    Else
    '        Return New ObservableCollection(Of LineaPlantillaJson)
    '    End If
    'End Function

    Private _formaPagoSeleccionada As FormaPagoDTO
    Public Property formaPagoSeleccionada() As FormaPagoDTO
        Get
            Return _formaPagoSeleccionada
        End Get
        Set(ByVal value As FormaPagoDTO)
            SetProperty(_formaPagoSeleccionada, value)
            cmdCrearPedido.RaiseCanExecuteChanged()
            RaisePropertyChanged(NameOf(SePuedeFinalizar))
            RaisePropertyChanged(NameOf(EsTarjetaPrepago))
            RaisePropertyChanged(NameOf(MandarCobroTarjeta))
        End Set
    End Property

    Private _formaVentaDirecta As Boolean
    Public Property formaVentaDirecta() As Boolean
        Get
            Return formaVentaSeleccionada.Equals(1)
        End Get
        Set(ByVal value As Boolean)
            formaVentaSeleccionada = 1
        End Set
    End Property

    Private _formaVentaOtras As Boolean
    Public Property formaVentaOtras() As Boolean
        Get
            Return formaVentaSeleccionada.Equals(3)
        End Get
        Set(ByVal value As Boolean)
            formaVentaSeleccionada = 3
        End Set
    End Property

    Private _formaVentaTelefono As Boolean
    Public Property formaVentaTelefono() As Boolean
        Get
            Return formaVentaSeleccionada.Equals(2)
        End Get
        Set(ByVal value As Boolean)
            formaVentaSeleccionada = 2
        End Set
    End Property

    Private _formaVentaSeleccionada As Integer
    Public Property formaVentaSeleccionada() As Integer
        Get
            Return _formaVentaSeleccionada
        End Get
        Set(ByVal value As Integer)
            SetProperty(_formaVentaSeleccionada, value)
            RaisePropertyChanged(NameOf(formaVentaDirecta))
            RaisePropertyChanged(NameOf(formaVentaTelefono))
            RaisePropertyChanged(NameOf(formaVentaOtras))
        End Set
    End Property

    Private _formaVentaOtrasSeleccionada As FormaVentaDTO
    Public Property formaVentaOtrasSeleccionada As FormaVentaDTO
        Get
            Return _formaVentaOtrasSeleccionada
        End Get
        Set(ByVal value As FormaVentaDTO)
            SetProperty(_formaVentaOtrasSeleccionada, value)
        End Set
    End Property

    Public ReadOnly Property hayProductosEnElPedido As Boolean
        Get
            Return Not IsNothing(listaProductosPedido) AndAlso listaProductosPedido.Count > 0
        End Get
    End Property

    Public ReadOnly Property hayUnClienteSeleccionado As Boolean
        Get
            Return Not IsNothing(clienteSeleccionado)
        End Get
    End Property

    Private _listaAlmacenes As ObservableCollection(Of tipoAlmacen)
    Public Property listaAlmacenes As ObservableCollection(Of tipoAlmacen)
        Get
            Return _listaAlmacenes
        End Get
        Set(value As ObservableCollection(Of tipoAlmacen))
            SetProperty(_listaAlmacenes, value)
        End Set
    End Property

    Private _listaClientes As ObservableCollection(Of ClienteJson)
    Public Property listaClientes As ObservableCollection(Of ClienteJson)
        Get
            Return _listaClientes
        End Get
        Set(ByVal value As ObservableCollection(Of ClienteJson))
            SetProperty(_listaClientes, value)
            cmdCargarClientesVendedor.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _listaClientesOriginal As ObservableCollection(Of ClienteJson)
    Public Property listaClientesOriginal As ObservableCollection(Of ClienteJson)
        Get
            Return _listaClientesOriginal
        End Get
        Set(ByVal value As ObservableCollection(Of ClienteJson))
            SetProperty(_listaClientesOriginal, value)
            listaClientes = listaClientesOriginal
        End Set
    End Property

    Private _listaFormasPago As ObservableCollection(Of FormaPagoDTO)
    Public Property listaFormasPago() As ObservableCollection(Of FormaPagoDTO)
        Get
            Return _listaFormasPago
        End Get
        Set(ByVal value As ObservableCollection(Of FormaPagoDTO))
            SetProperty(_listaFormasPago, value)
        End Set
    End Property
    Private _listaFormasVenta As ObservableCollection(Of FormaVentaDTO)
    Public Property listaFormasVenta() As ObservableCollection(Of FormaVentaDTO)
        Get
            Return _listaFormasVenta
        End Get
        Set(ByVal value As ObservableCollection(Of FormaVentaDTO))
            SetProperty(_listaFormasVenta, value)
        End Set
    End Property
    Private _listaPedidosPendientes As List(Of Integer)
    Public Property ListaPedidosPendientes As List(Of Integer)
        Get
            Return _listaPedidosPendientes
        End Get
        Set(value As List(Of Integer))
            SetProperty(_listaPedidosPendientes, value)
            RaisePropertyChanged(NameOf(TienePedidosPendientes))
        End Set
    End Property
    Private _listaPlazosPago As ObservableCollection(Of PlazoPagoDTO)
    Public Property listaPlazosPago() As ObservableCollection(Of PlazoPagoDTO)
        Get
            Return _listaPlazosPago
        End Get
        Set(ByVal value As ObservableCollection(Of PlazoPagoDTO))
            SetProperty(_listaPlazosPago, value)
        End Set
    End Property

    Private _listaFiltrableProductos As ColeccionFiltrable
    Public Property ListaFiltrableProductos As ColeccionFiltrable
        Get
            Return _listaFiltrableProductos
        End Get
        Set(value As ColeccionFiltrable)
            SetProperty(_listaFiltrableProductos, value)
        End Set
    End Property


    'Private _listaProductos As ObservableCollection(Of LineaPlantillaJson)
    'Public Property listaProductos As ObservableCollection(Of LineaPlantillaJson)
    '    Get
    '        Return _listaProductos
    '    End Get
    '    Set(ByVal value As ObservableCollection(Of LineaPlantillaJson))
    '        SetProperty(_listaProductos, value)
    '        RaisePropertyChanged(NameOf(listaProductosPedido))
    '        RaisePropertyChanged(NameOf(baseImponiblePedido))
    '        RaisePropertyChanged(NameOf(totalPedido))
    '        SoloConStockCommand.RaiseCanExecuteChanged()
    '    End Set
    'End Property

    'Private _listaProductosFijada As ObservableCollection(Of LineaPlantillaJson)
    'Public Property listaProductosFijada As ObservableCollection(Of LineaPlantillaJson)
    '    Get
    '        Return _listaProductosFijada
    '    End Get
    '    Set(ByVal value As ObservableCollection(Of LineaPlantillaJson))
    '        SetProperty(_listaProductosFijada, value)
    '        'listaProductos = listaProductosFijada
    '        ListaFiltrable.Lista = New ObservableCollection(Of IFiltrableItem)(value)
    '    End Set
    'End Property

    'Private _listaProductosOriginal As ObservableCollection(Of LineaPlantillaJson)
    'Public Property listaProductosOriginal As ObservableCollection(Of LineaPlantillaJson)
    '    Get
    '        Return _listaProductosOriginal
    '    End Get
    '    Set(ByVal value As ObservableCollection(Of LineaPlantillaJson))
    '        SetProperty(_listaProductosOriginal, value)
    '        ListaFiltrable.ListaFijada = New ObservableCollection(Of IFiltrableItem)(value)
    '    End Set
    'End Property

    Public ReadOnly Property listaProductosPedido() As ObservableCollection(Of LineaPlantillaJson)
        Get
            If Not IsNothing(ListaFiltrableProductos.ListaOriginal) Then
                Dim listaDevuelta As New ObservableCollection(Of LineaPlantillaJson)
                Dim lista = From l In ListaFiltrableProductos.ListaOriginal Where CType(l, LineaPlantillaJson).cantidad > 0 OrElse CType(l, LineaPlantillaJson).cantidadOferta > 0 Order By CType(l, LineaPlantillaJson).fechaInsercion
                For Each item In lista
                    listaDevuelta.Add(item)
                Next
                Return listaDevuelta
            Else
                Return Nothing
            End If
        End Get
    End Property

    Private _listaUltimasVentas As ObservableCollection(Of UltimasVentasProductoClienteDTO)
    Public Property listaUltimasVentas As ObservableCollection(Of UltimasVentasProductoClienteDTO)
        Get
            Return _listaUltimasVentas
        End Get
        Set(value As ObservableCollection(Of UltimasVentasProductoClienteDTO))
            SetProperty(_listaUltimasVentas, value)
        End Set
    End Property

    Private _mandarCobroTarjeta As Boolean
    Public Property MandarCobroTarjeta As Boolean
        Get
            Return _mandarCobroTarjeta AndAlso EsTarjetaPrepago
        End Get
        Set(value As Boolean)
            SetProperty(_mandarCobroTarjeta, value)
            If MandarCobroTarjeta Then
                CargarCorreoYMovilTarjeta.Execute()
            End If
        End Set
    End Property

    Public ReadOnly Property NoHayProductosEnElPedido
        Get
            Return Not hayProductosEnElPedido
        End Get
    End Property

    Private _paginaActual As WizardPage
    Public Property PaginaActual As WizardPage
        Get
            Return _paginaActual
        End Get
        Set(value As WizardPage)
            SetProperty(_paginaActual, value)
        End Set
    End Property

    Private _paginasWizard As List(Of WizardPage) = New List(Of WizardPage)
    Public Property PaginasWizard As List(Of WizardPage)
        Get
            Return _paginasWizard
        End Get
        Set(value As List(Of WizardPage))
            SetProperty(_paginasWizard, value)
        End Set
    End Property
    Private _pedidoPendienteSeleccionado As Integer
    Public Property PedidoPendienteSeleccionado As Integer
        Get
            Return _pedidoPendienteSeleccionado
        End Get
        Set(value As Integer)
            SetProperty(_pedidoPendienteSeleccionado, value)
            NoAmpliarPedidoCommand.RaiseCanExecuteChanged()
        End Set
    End Property
    Private _plazoPagoSeleccionado As PlazoPagoDTO
    Public Property plazoPagoSeleccionado As PlazoPagoDTO
        Get
            Return _plazoPagoSeleccionado
        End Get
        Set(ByVal value As PlazoPagoDTO)
            SetProperty(_plazoPagoSeleccionado, value)
            cmdCrearPedido.RaiseCanExecuteChanged()
            RaisePropertyChanged(NameOf(SePuedeFinalizar))
            RaisePropertyChanged(NameOf(EsTarjetaPrepago))
            RaisePropertyChanged(NameOf(MandarCobroTarjeta))
            If (Not IsNothing(_plazoPagoSeleccionado)) Then
                cmdCalcularSePuedeServirPorGlovo.Execute()
            End If
        End Set
    End Property

    Private _portesGlovo As Decimal
    Public Property PortesGlovo As Decimal
        Get
            Return _portesGlovo
        End Get
        Set(value As Decimal)
            SetProperty(_portesGlovo, value)
        End Set
    End Property

    Private _productoPedidoSeleccionado As LineaPlantillaJson
    Public Property productoPedidoSeleccionado As LineaPlantillaJson
        Get
            Return _productoPedidoSeleccionado
        End Get
        Set(ByVal value As LineaPlantillaJson)
            SetProperty(_productoPedidoSeleccionado, value)
        End Set
    End Property

    'Private _productoSeleccionado As LineaPlantillaJson
    'Public Property productoSeleccionado As LineaPlantillaJson
    '    Get
    '        Return _productoSeleccionado
    '    End Get
    '    Set(ByVal value As LineaPlantillaJson)
    '        SetProperty(_productoSeleccionado, value)
    '        cmdCargarUltimasVentas.Execute(productoSeleccionado)
    '    End Set
    'End Property

    Private _respuestaGlovo As RespuestaAgencia
    Public Property RespuestaGlovo As RespuestaAgencia
        Get
            Return _respuestaGlovo
        End Get
        Set(value As RespuestaAgencia)
            SetProperty(_respuestaGlovo, value)
        End Set
    End Property

    Public ReadOnly Property SePuedeFinalizar As Boolean
        Get
            Return CanCrearPedido()
        End Get
    End Property

    Private _sePuedeServirConGlovo As Boolean = False
    Public Property SePuedeServirConGlovo As Boolean
        Get
            Return _sePuedeServirConGlovo
        End Get
        Set(value As Boolean)
            SetProperty(_sePuedeServirConGlovo, value)
            If Not _sePuedeServirConGlovo Then
                EnviarPorGlovo = False
            End If
        End Set
    End Property

    Public ReadOnly Property textoFacturacionElectronica As String
        Get
            If IsNothing(direccionEntregaSeleccionada) Then
                Return String.Empty
            End If
            If direccionEntregaSeleccionada.tieneFacturacionElectronica Then
                Return "Este contacto tiene la facturación electrónica activada"
            End If
            If direccionEntregaSeleccionada.tieneCorreoElectronico Then
                Return "Este contacto tiene correo electrónico, pero NO tiene la facturación electrónica activada"
            End If
            Return "Recuerde pedir un correo electrónico al cliente para poder activar la facturación electrónica"
        End Get
    End Property
    Public ReadOnly Property TienePedidosPendientes As Boolean
        Get
            Return Not IsNothing(ListaPedidosPendientes) AndAlso ListaPedidosPendientes.Where(Function(f) f <> 0).Any
        End Get
    End Property
    Private _todosLosVendedores As Boolean
    Public Property todosLosVendedores As Boolean
        Get
            Return _todosLosVendedores
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_todosLosVendedores, value)
        End Set
    End Property

    Public ReadOnly Property totalPedido As Decimal
        Get
            If IsNothing(clienteSeleccionado) Then
                Return 0
            End If
            Return IIf(Not IsNothing(clienteSeleccionado.iva), baseImponiblePedido * 1.21, baseImponiblePedido)
        End Get
    End Property

    'Enum PaginasWizard
    '    SeleccionCliente
    '    SeleccionProductos
    '    SeleccionEntrega
    '    Finalizar
    'End Enum

#End Region

#Region "Comandos"

    Private _cmdAbrirPlantillaVenta As DelegateCommand(Of Object)
    Public Property cmdAbrirPlantillaVenta As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirPlantillaVenta
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdAbrirPlantillaVenta, value)
        End Set
    End Property
    Private Function CanAbrirPlantillaVenta(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirPlantillaVenta(arg As Object)
        regionManager.RequestNavigate("MainRegion", "PlantillaVentaView")
        'regionManager.RegisterViewWithRegion("MainRegion", GetType(PlantillaVentaView))
        'Dim region As IRegion = regionManager.Regions("MainRegion")
        'Dim vista = container.Resolve(Of PlantillaVentaView)()
        'region.Add(vista, nombreVista(region, vista.ToString))
        'region.Activate(vista)
    End Sub

    Private _cmdActualizarPrecioProducto As DelegateCommand(Of Object)
    Public Property cmdActualizarPrecioProducto As DelegateCommand(Of Object)
        Get
            Return _cmdActualizarPrecioProducto
        End Get
        Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdActualizarPrecioProducto, value)
        End Set
    End Property
    Private Function CanActualizarPrecioProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnActualizarPrecioProducto(arg As Object)
        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            'estaOcupado = True

            Try
                Dim urlConsulta As String = "PlantillaVentas/CargarPrecio?empresa=" + clienteSeleccionado.empresa
                urlConsulta += "&cliente=" + clienteSeleccionado.cliente
                urlConsulta += "&contacto=" + clienteSeleccionado.contacto
                urlConsulta += "&productoPrecio=" + arg.producto
                urlConsulta += "&cantidad=" + arg.cantidad.ToString
                urlConsulta += "&aplicarDescuento=" + arg.aplicarDescuento.ToString
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Dim datosPrecio = JsonConvert.DeserializeObject(Of PrecioProductoDTO)(cadenaJson)
                    arg.precio = datosPrecio.precio
                    arg.descuentoProducto = datosPrecio.descuento
                    arg.aplicarDescuento = datosPrecio.aplicarDescuento
                    If arg.descuento < arg.descuentoProducto OrElse Not arg.aplicarDescuento Then
                        arg.descuento = IIf(arg.aplicarDescuento, arg.descuentoProducto, 0)
                    End If
                    RaisePropertyChanged(NameOf(baseImponiblePedido))
                Else
                    dialogService.ShowError("Se ha producido un error al cargar el precio y los descuentos especiales")
                End If

                'Carlos 04/07/18: desactivo porque lo controlamos con las ofertas permitidas
                'Await cmdComprobarCondicionesPrecio.Execute(arg)

                RaisePropertyChanged(NameOf(listaProductosPedido))
                'RaisePropertyChanged(NameOf(listaProductos))
                RaisePropertyChanged(NameOf(baseImponiblePedido))
                RaisePropertyChanged(NameOf(totalPedido))
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                'estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdActualizarProductosPedido As DelegateCommand(Of LineaPlantillaJson)
    Public Property cmdActualizarProductosPedido As DelegateCommand(Of LineaPlantillaJson)
        Get
            Return _cmdActualizarProductosPedido
        End Get
        Private Set(value As DelegateCommand(Of LineaPlantillaJson))
            SetProperty(_cmdActualizarProductosPedido, value)
        End Set
    End Property
    Private Function CanActualizarProductosPedido(arg As LineaPlantillaJson) As Boolean
        Return True
    End Function
    Private Sub OnActualizarProductosPedido(arg As LineaPlantillaJson)
        If IsNothing(arg) Then
            Return
        End If

        cmdActualizarPrecioProducto.Execute(arg)

        If arg.cantidadVendida = 0 AndAlso arg.cantidadAbonada = 0 Then
            cmdInsertarProducto.Execute(arg)
        End If

        If (arg.cantidad + arg.cantidadOferta <> 0) AndAlso Not arg.stockActualizado Then
            cmdCargarStockProducto.Execute(arg)
        End If
        RaisePropertyChanged(NameOf(hayProductosEnElPedido))
        RaisePropertyChanged(NameOf(NoHayProductosEnElPedido))
        If Not IsNothing(ListaFiltrableProductos) AndAlso (IsNothing(ListaFiltrableProductos.ElementoSeleccionado) OrElse CType(ListaFiltrableProductos.ElementoSeleccionado, LineaPlantillaJson).producto <> arg.producto) Then
            ListaFiltrableProductos.ElementoSeleccionado = arg
        End If

        'RaisePropertyChanged(NameOf(productoSeleccionado))
        'RaisePropertyChanged(NameOf(listaProductos))
        RaisePropertyChanged(NameOf(listaProductosPedido))
        RaisePropertyChanged(NameOf(baseImponiblePedido))
        RaisePropertyChanged(NameOf(totalPedido))

    End Sub

    'Private _quitarFiltroProductoCommand As DelegateCommand(Of String)
    'Public Property QuitarFiltroProductoCommand As DelegateCommand(Of String)
    '    Get
    '        Return _quitarFiltroProductoCommand
    '    End Get
    '    Set(value As DelegateCommand(Of String))
    '        SetProperty(_quitarFiltroProductoCommand, value)
    '    End Set
    'End Property
    'Private Sub OnQuitarFiltroProducto(filtro As String)
    '    filtro = filtro.ToLower
    '    If ListaFiltrosProducto.Count = 1 OrElse Not listaProductosOriginal.Any Then
    '        ListaFiltrosProducto.Clear()
    '        listaProductosFijada = listaProductosOriginal
    '    Else
    '        ListaFiltrosProducto.Remove(filtro)
    '        listaProductosFijada = listaProductosOriginal
    '        For Each filtroFijado In ListaFiltrosProducto
    '            listaProductosFijada = AplicarFiltro(filtroFijado)
    '        Next
    '    End If
    'End Sub

    Private _soloConStockCommand As DelegateCommand
    Public Property SoloConStockCommand As DelegateCommand
        Get
            Return _soloConStockCommand
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_soloConStockCommand, value)
        End Set
    End Property
    Private Function CanSoloConStock() As Boolean
        Return Not IsNothing(ListaFiltrableProductos) AndAlso Not IsNothing(ListaFiltrableProductos.Lista) AndAlso ListaFiltrableProductos.Lista.Any
    End Function
    Private Sub OnSoloConStock()
        ListaFiltrableProductos.Lista = New ObservableCollection(Of IFiltrableItem)(ListaFiltrableProductos.Lista.Where(Function(l) CType(l, LineaPlantillaJson).cantidadDisponible > 0))
    End Sub

    Private _cmdBuscarEnTodosLosProductos As DelegateCommand(Of String)
    Public Property cmdBuscarEnTodosLosProductos As DelegateCommand(Of String)
        Get
            Return _cmdBuscarEnTodosLosProductos
        End Get
        Private Set(value As DelegateCommand(Of String))
            SetProperty(_cmdBuscarEnTodosLosProductos, value)
        End Set
    End Property
    Private Function CanBuscarEnTodosLosProductos(filtro As String) As Boolean
        Return Not IsNothing(ListaFiltrableProductos) AndAlso Not IsNothing(ListaFiltrableProductos.Filtro) AndAlso ListaFiltrableProductos.Filtro.Length >= 3
    End Function
    Private Async Sub OnBuscarEnTodosLosProductos(filtro As String)
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                Dim url As String = "PlantillaVentas/BuscarProducto?empresa=" + clienteSeleccionado.empresa + "&filtroProducto=" + filtro
                response = Await client.GetAsync(url)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    ListaFiltrableProductos.ListaFijada = New ObservableCollection(Of IFiltrableItem)(JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaJson))(cadenaJson))
                    Dim listaPlantilla = New ObservableCollection(Of LineaPlantillaJson)()
                    For Each item In ListaFiltrableProductos.ListaFijada
                        listaPlantilla.Add(item)
                    Next
                    ListaFiltrableProductos.ListaFijada = New ObservableCollection(Of IFiltrableItem)(Await servicio.PonerStocks(listaPlantilla, almacenSeleccionado.id))
                    Dim productoOriginal As LineaPlantillaJson
                    Dim producto As LineaPlantillaJson
                    For i = 0 To ListaFiltrableProductos.ListaFijada.Count - 1
                        producto = ListaFiltrableProductos.ListaFijada(i)
                        If clienteSeleccionado.cliente = Constantes.Clientes.Especiales.EL_EDEN OrElse clienteSeleccionado.estado = Constantes.Clientes.ESTADO_DISTRIBUIDOR Then
                            producto.aplicarDescuento = True
                            producto.aplicarDescuentoFicha = True
                        End If
                        productoOriginal = ListaFiltrableProductos.ListaOriginal.Where(Function(p) CType(p, LineaPlantillaJson).producto = producto.producto).FirstOrDefault
                        If Not IsNothing(productoOriginal) Then
                            ListaFiltrableProductos.ListaFijada(i) = productoOriginal
                        End If
                    Next
                    estaOcupado = False
                    'ListaFiltrosProducto.Clear()
                    'ListaFiltrosProducto.Add(ListaFiltrable.Filtro)
                Else
                    dialogService.ShowError("Se ha producido un error al cargar los productos")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

        End Using
    End Sub


    Private _cambiarIvaCommand As DelegateCommand
    Public Property CambiarIvaCommand As DelegateCommand
        Get
            Return _cambiarIvaCommand
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cambiarIvaCommand, value)
        End Set
    End Property
    Private Sub OnCambiarIva()
        'this.direccionSeleccionada.iva = this.direccionSeleccionada.iva ? undefined : this.iva;
        clienteSeleccionado.iva = IIf(Not String.IsNullOrWhiteSpace(clienteSeleccionado.iva), Nothing, iva)
        RaisePropertyChanged(NameOf(clienteSeleccionado))
        RaisePropertyChanged(NameOf(totalPedido))
        RaisePropertyChanged(NameOf(SePuedeFinalizar))
    End Sub

    Private _cmdCargarClientesVendedor As DelegateCommand
    Public Property cmdCargarClientesVendedor As DelegateCommand
        Get
            Return _cmdCargarClientesVendedor
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCargarClientesVendedor, value)
        End Set
    End Property
    Private Function CanCargarClientesVendedor() As Boolean
        Return filtroCliente.Length = 0 OrElse IsNothing(listaClientes) OrElse listaClientes.Count = 0
    End Function
    Private Async Sub OnCargarClientesVendedor()

        ' No se puede filtrar por menos de 4 caracteres
        If IsNothing(filtroCliente) OrElse (filtroCliente.Length < 4 AndAlso Not IsNumeric(filtroCliente)) Then
            listaClientes = Nothing
            Return
        End If

        Try
            estaOcupado = True
            vendedor = Await leerParametro("Vendedor")
            Dim parametroClientesTodosVendedores As String = Await leerParametro("PermitirVerClientesTodosLosVendedores")
            todosLosVendedores = IIf(parametroClientesTodosVendedores.Trim = "1", True, False)
            listaClientesOriginal = Await servicio.CargarClientesVendedor(filtroCliente, vendedor, todosLosVendedores)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaOcupado = False
        End Try

    End Sub

    Private _cargarCorreoYMovilTarjeta As DelegateCommand
    Public Property CargarCorreoYMovilTarjeta As DelegateCommand
        Get
            Return _cargarCorreoYMovilTarjeta
        End Get
        Set(value As DelegateCommand)
            SetProperty(_cargarCorreoYMovilTarjeta, value)
        End Set
    End Property
    Private Async Sub OnCargarCorreoYMovilTarjeta()
        Dim telefono As Telefono = New Telefono(clienteSeleccionado.telefono)
        CobroTarjetaMovil = telefono.MovilUnico
        Dim cliente = Await servicio.CargarCliente(clienteSeleccionado.empresa, clienteSeleccionado.cliente, direccionEntregaSeleccionada.contacto)
        Dim personasContacto = New List(Of PersonasContactoCliente)
        For Each persona In cliente.PersonasContacto
            personasContacto.Add(New PersonasContactoCliente With {
                .Cargo = IIf(persona.FacturacionElectronica, 22, 14),
                .CorreoElectrónico = persona.CorreoElectronico
                                 })
        Next
        Dim correo As CorreoCliente = New CorreoCliente(personasContacto)
        CobroTarjetaCorreo = correo.CorreoUnicoFacturaElectronica
        If String.IsNullOrEmpty(CobroTarjetaCorreo) Then
            CobroTarjetaCorreo = correo.CorreoAgencia
        End If
    End Sub

    Private _cmdCargarFormasPago As DelegateCommand(Of Object)
    Public Property cmdCargarFormasPago As DelegateCommand(Of Object)
        Get
            Return _cmdCargarFormasPago
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarFormasPago, value)
        End Set
    End Property
    Private Function CanCargarFormasPago(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarFormasPago(arg As Object)

        If IsNothing(direccionEntregaSeleccionada) OrElse IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                response = Await client.GetAsync("FormasPago?empresa=" + clienteSeleccionado.empresa + "&cliente=" + clienteSeleccionado.cliente)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaFormasPago = JsonConvert.DeserializeObject(Of ObservableCollection(Of FormaPagoDTO))(cadenaJson)
                    formaPagoSeleccionada = listaFormasPago.Where(Function(l) l.formaPago = direccionEntregaSeleccionada.formaPago).SingleOrDefault
                Else
                    dialogService.ShowError("Se ha producido un error al cargar las formas de pago")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdCargarFormasVenta As DelegateCommand(Of Object)
    Public Property cmdCargarFormasVenta As DelegateCommand(Of Object)
        Get
            Return _cmdCargarFormasVenta
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarFormasVenta, value)
        End Set
    End Property
    Private Function CanCargarFormasVenta(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarFormasVenta(arg As Object)

        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        vendedorUsuario = Await leerParametro("Vendedor")
        If vendedorUsuario = clienteSeleccionado.vendedor Then
            formaVentaSeleccionada = 1 ' Directa
        Else
            formaVentaSeleccionada = 2 ' Telefono
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                response = Await client.GetAsync("FormasVenta?empresa=" + clienteSeleccionado.empresa)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaFormasVenta = JsonConvert.DeserializeObject(Of ObservableCollection(Of FormaVentaDTO))(cadenaJson)
                Else
                    dialogService.ShowError("Se ha producido un error al cargar las formas de venta")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdCargarPlazosPago As DelegateCommand(Of Object)
    Public Property cmdCargarPlazosPago As DelegateCommand(Of Object)
        Get
            Return _cmdCargarPlazosPago
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarPlazosPago, value)
        End Set
    End Property
    Private Function CanCargarPlazosPago(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarPlazosPago(arg As Object)

        If IsNothing(direccionEntregaSeleccionada) OrElse IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            estaOcupado = True

            Try
                response = Await client.GetAsync("PlazosPago?empresa=" + clienteSeleccionado.empresa + "&cliente=" + clienteSeleccionado.cliente)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaPlazosPago = JsonConvert.DeserializeObject(Of ObservableCollection(Of PlazoPagoDTO))(cadenaJson)
                    plazoPagoSeleccionado = listaPlazosPago.Where(Function(l) l.plazoPago = direccionEntregaSeleccionada.plazosPago).SingleOrDefault
                Else
                    dialogService.ShowError("Se ha producido un error al cargar los plazos de pago")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cargarProductoCommand As DelegateCommand(Of Object)
    Public Property CargarProductoCommand As DelegateCommand(Of Object)
        Get
            Return _cargarProductoCommand
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cargarProductoCommand, value)
        End Set
    End Property
    Private Function CanCargarProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnCargarProducto(arg As Object)
        If IsNothing(productoPedidoSeleccionado) Then
            Return
        End If
        Dim parameters As NavigationParameters = New NavigationParameters()
        parameters.Add("numeroProductoParameter", productoPedidoSeleccionado.producto)
        regionManager.RequestNavigate("MainRegion", "ProductoView", parameters)
    End Sub

    Private _cmdCargarProductosPlantilla As DelegateCommand
    Public Property cmdCargarProductosPlantilla As DelegateCommand
        Get
            Return _cmdCargarProductosPlantilla
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCargarProductosPlantilla, value)
        End Set
    End Property
    Private Async Sub OnCargarProductosPlantilla()

        Try
            estaOcupado = True
            ListaFiltrableProductos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(Await servicio.CargarProductosPlantilla(clienteSeleccionado))
            Dim listaPlantilla = New ObservableCollection(Of LineaPlantillaJson)
            For Each item In ListaFiltrableProductos.ListaOriginal
                listaPlantilla.Add(item)
            Next
            ListaFiltrableProductos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(Await servicio.PonerStocks(listaPlantilla, almacenSeleccionado.id))
            estaOcupado = False
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaOcupado = False
        End Try

    End Sub

    Private _cmdCargarStockProducto As DelegateCommand(Of Object)
    Public Property cmdCargarStockProducto As DelegateCommand(Of Object)
        Get
            Return _cmdCargarStockProducto
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarStockProducto, value)
        End Set
    End Property
    Private Function CanCargarStockProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarStockProducto(arg As Object)

        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            'estaOcupado = True

            Dim datosStock As StockProductoDTO

            Try
                response = Await client.GetAsync("PlantillaVentas/CargarStocks?empresa=" + clienteSeleccionado.empresa + "&almacen=" + almacenSeleccionado.id + "&productoStock=" + arg.producto)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    datosStock = JsonConvert.DeserializeObject(Of StockProductoDTO)(cadenaJson)
                    arg.stock = datosStock.stock
                    arg.cantidadDisponible = datosStock.cantidadDisponible
                    arg.cantidadPendienteRecibir = datosStock.cantidadPendienteRecibir
                    arg.urlImagen = datosStock.urlImagen
                    arg.stockActualizado = True
                    arg.fechaInsercion = Now
                    RaisePropertyChanged(NameOf(listaProductosPedido))
                    'RaisePropertyChanged(NameOf(listaProductos))
                    'RaisePropertyChanged(NameOf(productoSeleccionado))
                    RaisePropertyChanged(NameOf(baseImponiblePedido))
                Else
                    dialogService.ShowError("Se ha producido un error al cargar el stock del producto")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                'estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdCargarUltimasVentas As DelegateCommand(Of Object)
    Public Property cmdCargarUltimasVentas As DelegateCommand(Of Object)
        Get
            Return _cmdCargarUltimasVentas
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarUltimasVentas, value)
        End Set
    End Property
    Private Function CanCargarUltimasVentas(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarUltimasVentas(arg As Object)

        If IsNothing(clienteSeleccionado) OrElse IsNothing(arg) Then
            Return
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            'estaOcupado = True

            Try
                response = Await client.GetAsync("PlantillaVentas/UltimasVentasProductoCliente?empresa=" + clienteSeleccionado.empresa + "&clienteUltimasVentas=" + clienteSeleccionado.cliente + "&productoUltimasVentas=" + arg.producto)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    listaUltimasVentas = JsonConvert.DeserializeObject(Of ObservableCollection(Of UltimasVentasProductoClienteDTO))(cadenaJson)

                Else
                    dialogService.ShowError("Se ha producido un error al cargar las últimas ventas del producto")
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                'estaOcupado = False
            End Try

        End Using

    End Sub

    Private _cmdComprobarPendientes As DelegateCommand
    Public Property cmdComprobarPendientes As DelegateCommand
        Get
            Return _cmdComprobarPendientes
        End Get
        Set(value As DelegateCommand)
            SetProperty(_cmdComprobarPendientes, value)
        End Set
    End Property
    Private Async Sub OnComprobarPendientes()
        If IsNothing(clienteSeleccionado) Then
            Return
        End If

        Try
            ListaPedidosPendientes = Await servicio.CargarListaPendientes(clienteSeleccionado.empresa, clienteSeleccionado.cliente)

            If TienePedidosPendientes Then
                Dim textoMensaje As String = "Este cliente tiene otros pedidos pendientes." + vbCr + vbCr
                For Each i In ListaPedidosPendientes.Where(Function(f) f <> 0)
                    textoMensaje += i.ToString + vbTab
                Next
                textoMensaje += vbCr + vbCr + "Por favor, revise que sea todo correcto."
                dialogService.ShowNotification("Pendientes", textoMensaje)
            End If
        Catch ex As Exception
            dialogService.ShowError("Se ha producido un error al comprobar los pedidos pendientes del cliente" + vbCrLf + ex.Message)
        End Try
    End Sub

    Private _cmdCrearPedido As DelegateCommand
    Public Property cmdCrearPedido As DelegateCommand
        Get
            Return _cmdCrearPedido
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCrearPedido, value)
        End Set
    End Property
    Private Function CanCrearPedido() As Boolean
        Return Not IsNothing(formaPagoSeleccionada) AndAlso Not IsNothing(plazoPagoSeleccionado) AndAlso
            (Not String.IsNullOrEmpty(clienteSeleccionado.cifNif) OrElse String.IsNullOrEmpty(clienteSeleccionado.iva) OrElse EsPresupuesto)
    End Function
    Private Async Sub OnCrearPedido()

        If IsNothing(formaPagoSeleccionada) OrElse IsNothing(plazoPagoSeleccionado) OrElse IsNothing(clienteSeleccionado) Then
            dialogService.ShowError("Compruebe que tiene seleccionados plazos y forma de pago, por favor.")
            Return
        End If

        delegacionUsuario = Await leerParametro("DelegaciónDefecto")

        If formaVentaSeleccionada = 1 Then
            formaVentaPedido = "DIR"
        ElseIf formaVentaSeleccionada = 2 Then
            formaVentaPedido = "TEL"
        Else
            If IsNothing(formaVentaOtrasSeleccionada) Then
                Return
            End If
            formaVentaPedido = formaVentaOtrasSeleccionada.numero
        End If

        If IsNothing(plazoPagoSeleccionado) AndAlso (IsNothing(direccionEntregaSeleccionada) OrElse IsNothing(direccionEntregaSeleccionada.plazosPago)) Then
            dialogService.ShowError("Este contacto no tiene plazos de pago asignados")
            Return
        End If


        estaOcupado = True
        Dim pedido As PedidoVentaDTO = PrepararPedido()

        Try

            Dim numPedido As String

            If PedidoPendienteSeleccionado = 0 Then
                numPedido = Await servicio.CrearPedido(pedido)
            Else
                Dim pedidoUnido As PedidoVentaDTO = Await servicio.UnirPedidos(clienteSeleccionado.empresa, PedidoPendienteSeleccionado, pedido)
                numPedido = pedidoUnido.numero.ToString
            End If

            If MandarCobroTarjeta Then
                servicio.EnviarCobroTarjeta(CobroTarjetaCorreo, CobroTarjetaMovil, totalPedido, numPedido, clienteSeleccionado.cliente)
            End If

            ' Cerramos la ventana
            Dim view = regionManager.Regions("MainRegion").ActiveViews.FirstOrDefault
            If Not IsNothing(view) Then
                regionManager.Regions("MainRegion").Deactivate(view)
                regionManager.Regions("MainRegion").Remove(view)
            End If

            ' Abrimos el pedido
            PedidoVentaViewModel.CargarPedido(clienteSeleccionado.empresa, numPedido, container)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaOcupado = False
        End Try



    End Sub

    Private _noAmpliarPedidoCommand As DelegateCommand
    Public Property NoAmpliarPedidoCommand As DelegateCommand
        Get
            Return _noAmpliarPedidoCommand
        End Get
        Set(value As DelegateCommand)
            SetProperty(_noAmpliarPedidoCommand, value)
        End Set
    End Property
    Private Function CanNoAmpliarPedido() As Boolean
        Return PedidoPendienteSeleccionado <> 0
    End Function
    Private Sub OnNoAmpliarPedido()
        PedidoPendienteSeleccionado = 0
    End Sub

    Private _copiarClientePortapapelesCommand As DelegateCommand
    Public Property CopiarClientePortapapelesCommand As DelegateCommand
        Get
            Return _copiarClientePortapapelesCommand
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_copiarClientePortapapelesCommand, value)
        End Set
    End Property
    Private Sub OnCopiarClientePortapapeles()
        Dim html As New StringBuilder()
        html.Append(Constantes.Formatos.HTML_CLIENTE_P_TAG)
        html.Append(clienteSeleccionado.ToString.Replace(vbCr, "<br/>"))
        html.Append("</p>")
        ClipboardHelper.CopyToClipboard(html.ToString, clienteSeleccionado.ToString)
        dialogService.ShowNotification("Datos del cliente copiados al portapapeles")
    End Sub






    Private Function PrepararPedido() As PedidoVentaDTO
        If IsNothing(formaPagoSeleccionada) Then
            If IsNothing(listaFormasPago) Then
                formaPagoSeleccionada = New FormaPagoDTO With {.formaPago = "RCB", .cccObligatorio = True}
            Else
                formaPagoSeleccionada = listaFormasPago.First
            End If
        End If

        almacenRutaUsuario = almacenSeleccionado.id

        Dim pedido As New PedidoVentaDTO With {
                        .empresa = clienteSeleccionado.empresa,
                        .cliente = clienteSeleccionado.cliente,
                        .contacto = direccionEntregaSeleccionada.contacto,
                        .EsPresupuesto = EsPresupuesto,
                        .fecha = Today,
                        .formaPago = formaPagoSeleccionada.formaPago,
                        .plazosPago = plazoPagoSeleccionado.plazoPago,
                        .primerVencimiento = Today, 'se calcula en la API
                        .iva = clienteSeleccionado.iva,
                        .vendedor = direccionEntregaSeleccionada.vendedor,
                        .periodoFacturacion = direccionEntregaSeleccionada.periodoFacturacion,
                        .ruta = IIf(EnviarPorGlovo, "GLV", direccionEntregaSeleccionada.ruta),
                        .serie = CalcularSerie(),
                        .ccc = IIf(formaPagoSeleccionada.cccObligatorio, direccionEntregaSeleccionada.ccc, Nothing),
                        .origen = clienteSeleccionado.empresa,
                        .contactoCobro = clienteSeleccionado.contacto, 'calcular
                        .noComisiona = direccionEntregaSeleccionada.noComisiona,
                        .mantenerJunto = direccionEntregaSeleccionada.mantenerJunto,
                        .servirJunto = direccionEntregaSeleccionada.servirJunto,
                        .comentarioPicking = clienteSeleccionado.comentarioPicking,
                        .comentarios = direccionEntregaSeleccionada.comentarioRuta,
                        .usuario = configuracion.usuario
                    }

        Dim lineaPedido, lineaPedidoOferta As LineaPedidoVentaDTO
        Dim ofertaLinea As Integer?

        For Each linea In listaProductosPedido
            If linea.cantidadOferta <> 0 Then
                ofertaLinea = cogerSiguienteOferta()
            Else
                ofertaLinea = Nothing
            End If

            'If linea.descuento = linea.descuentoProducto Then
            '    linea.descuento = 0
            'Else
            '    linea.aplicarDescuento = False
            'End If

            lineaPedido = New LineaPedidoVentaDTO With {
                .estado = IIf(EsPresupuesto, ESTADO_LINEA_PRESUPUESTO, ESTADO_LINEA_CURSO), '¿Pongo 0 para tener que validar?
                .tipoLinea = 1, ' Producto
                .producto = linea.producto,
                .texto = linea.texto,
                .cantidad = linea.cantidad,
                .fechaEntrega = fechaEntrega,
                .precio = linea.precio,
                .descuento = IIf(linea.descuento = linea.descuentoProducto, 0, linea.descuento),
                .descuentoProducto = IIf(linea.descuento = linea.descuentoProducto, linea.descuentoProducto, 0),
                .aplicarDescuento = IIf(linea.descuento = linea.descuentoProducto, linea.aplicarDescuento, False),
                .vistoBueno = 0, 'calcular
                .usuario = configuracion.usuario,
                .almacen = almacenRutaUsuario,
                .iva = linea.iva,
                .delegacion = delegacionUsuario, 'pedir al usuario en alguna parte
                .formaVenta = formaVentaPedido,
                .oferta = ofertaLinea
            }
            If linea.cantidad <> 0 Then
                pedido.LineasPedido.Add(lineaPedido)
            End If

            If linea.cantidadOferta <> 0 Then
                lineaPedidoOferta = lineaPedido.ShallowCopy
                lineaPedidoOferta.cantidad = linea.cantidadOferta
                lineaPedidoOferta.precio = 0
                lineaPedidoOferta.oferta = lineaPedido.oferta
                pedido.LineasPedido.Add(lineaPedidoOferta)
            End If

        Next

        Return pedido
    End Function

    Private Function CalcularSerie() As String
        If clienteSeleccionado.estado = 6 AndAlso listaProductosPedido.All(Function(l) l.familia = Constantes.Familias.UNION_LASER_NOMBRE) Then
            Return Constantes.Series.UNION_LASER
        End If

        If clienteSeleccionado.estado = 6 AndAlso listaProductosPedido.All(Function(l) l.familia = Constantes.Familias.EVA_VISNU_NOMBRE) Then
            Return Constantes.Series.EVA_VISNU
        End If

        Return Constantes.Series.SERIE_DEFECTO
    End Function

    Private _cmdInsertarProducto As DelegateCommand(Of Object)
    Public Property cmdInsertarProducto As DelegateCommand(Of Object)
        Get
            Return _cmdInsertarProducto
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdInsertarProducto, value)
        End Set
    End Property
    Private Function CanInsertarProducto(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnInsertarProducto(arg As Object)
        ' Solo insertamos si es un producto que no está en listaProductosOrigina
        If IsNothing(arg) OrElse Not IsNothing(ListaFiltrableProductos.ListaOriginal.Where(Function(p) CType(p, LineaPlantillaJson).producto = arg.producto).FirstOrDefault) Then
            Return
        End If
        'arg.cantidadVendida = arg.cantidad + arg.cantidadOferta
        ListaFiltrableProductos.ListaOriginal.Add(arg)
        'listaProductos = listaProductosOriginal
        'filtroProducto = ""
        RaisePropertyChanged(NameOf(baseImponiblePedido))
        'RaisePropertyChanged(NameOf(listaProductos))
        'RaisePropertyChanged(NameOf(listaProductosOriginal))
    End Sub

    Private _cmdCalcularSePuedeServirPorGlovo As DelegateCommand
    Public Property cmdCalcularSePuedeServirPorGlovo As DelegateCommand
        Get
            Return _cmdCalcularSePuedeServirPorGlovo
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCalcularSePuedeServirPorGlovo, value)
        End Set
    End Property
    Private Async Sub OnCalcularSePuedeServirPorGlovo()
        Using client As New HttpClient
            estaOcupado = True

            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Dim pedido As PedidoVentaDTO = PrepararPedido()

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json")

            Try
                response = Await client.PostAsync("PedidosVenta/SePuedeServirPorAgencia", content)


                If response.IsSuccessStatusCode Then
                    Dim respuestaString As String = Await response.Content.ReadAsStringAsync()
                    RespuestaGlovo = JsonConvert.DeserializeObject(Of RespuestaAgencia)(respuestaString)
                    If Not IsNothing(RespuestaGlovo) Then
                        SePuedeServirConGlovo = True
                        DireccionGoogleMaps = RespuestaGlovo.DireccionFormateada
                        PortesGlovo = RespuestaGlovo.Coste
                    Else
                        SePuedeServirConGlovo = False
                        DireccionGoogleMaps = ""
                        PortesGlovo = 0
                    End If
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("exceptionMessage")
                    If String.IsNullOrEmpty(contenido) Then
                        contenido = detallesError("ExceptionMessage")
                    End If
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("exceptionMessage")
                        If String.IsNullOrEmpty(contenido2) Then
                            contenido2 = detallesError("ExceptionMessage")
                        End If
                        contenido = contenido + vbCr + contenido2
                    End While

                    dialogService.ShowError(contenido)
                End If
            Catch ex As Exception
                dialogService.ShowError(ex.Message)
            Finally
                estaOcupado = False
            End Try

        End Using
    End Sub

#End Region

#Region "Funciones Auxiliares"

    Private Function cogerSiguienteOferta() As Integer
        ultimaOferta += 1
        Return ultimaOferta
    End Function

    Private Async Function leerParametro(v As String) As Task(Of String)
        Dim empresa As String = "1"
        If Not IsNothing(clienteSeleccionado) Then
            empresa = clienteSeleccionado.empresa
        End If

        Return Await configuracion.leerParametro(empresa, v)
    End Function

    Private Function nombreVista(region As Region, nombre As String) As String
        Dim contador As Integer = 2
        Dim repetir As Boolean = True
        Dim nombreAmpliado As String = nombre
        While repetir
            repetir = False
            For Each view In region.Views
                If Not IsNothing(region.GetView(nombreAmpliado)) Then
                    nombreAmpliado = nombre + contador.ToString
                    contador = contador + 1
                    repetir = True
                    Exit For
                End If
            Next
        End While
        Return nombreAmpliado
    End Function

    Public Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo

    End Sub

    Public Function IsNavigationTarget(navigationContext As NavigationContext) As Boolean Implements INavigationAware.IsNavigationTarget
        Return False
    End Function

    Public Sub OnNavigatedFrom(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedFrom

    End Sub


#End Region

    Public Structure tipoAlmacen
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

    Public Class RespuestaAgencia
        Public Property DireccionFormateada As String
        Public Property Longitud As Double
        Public Property Latitud As Double
        Public Property Coste As Decimal
    End Class
End Class

