﻿Imports System.Collections.ObjectModel
Imports System.ComponentModel.DataAnnotations
Imports System.Globalization
Imports System.Text
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Models
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Events
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Mvvm
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports VendedorGrupoProductoDTO = Nesto.Models.VendedorGrupoProductoDTO

Public Class DetallePedidoViewModel
    Inherits BindableBase
    Implements INavigationAware

    Private estaActualizarFechaActivo As Boolean = True
    Private ReadOnly regionManager As IRegionManager
    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IPedidoVentaService
    Private ReadOnly eventAggregator As IEventAggregator
    Private ReadOnly dialogService As IDialogService

    Private ivaOriginal As String

    Private Const IVA_POR_DEFECTO = "G21"
    Private Const EMPRESA_POR_DEFECTO = "1"

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IPedidoVentaService, eventAggregator As IEventAggregator, dialogService As IDialogService)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.eventAggregator = eventAggregator
        Me.dialogService = dialogService

        cmdAbrirPicking = New DelegateCommand(AddressOf OnAbrirPicking)
        AceptarPresupuestoCommand = New DelegateCommand(AddressOf OnAceptarPresupuesto, AddressOf CanAceptarPresupuesto)
        DescargarPresupuestoCommand = New DelegateCommand(AddressOf OnDescargarPresupuesto, AddressOf CanDescargarPresupuesto)
        cmdActualizarTotales = New DelegateCommand(AddressOf OnActualizarTotales)
        cmdCambiarFechaEntrega = New DelegateCommand(AddressOf OnCambiarFechaEntrega)
        cmdCambiarIva = New DelegateCommand(AddressOf OnCambiarIva)
        cmdCargarPedido = New DelegateCommand(Of ResumenPedido)(AddressOf OnCargarPedido)
        CargarProductoCommand = New DelegateCommand(Of LineaPedidoVentaDTO)(AddressOf OnCargarProducto)
        cmdCeldaModificada = New DelegateCommand(Of DataGridCellEditEndingEventArgs)(AddressOf OnCeldaModificada)
        cmdModificarPedido = New DelegateCommand(AddressOf OnModificarPedido)
        cmdPonerDescuentoPedido = New DelegateCommand(AddressOf OnPonerDescuentoPedido, AddressOf CanPonerDescuentoPedido)
        AbrirEnlaceSeguimientoCommand = New DelegateCommand(Of String)(AddressOf OnAbrirEnlaceSeguimientoCommand)
        EnviarCobroTarjetaCommand = New DelegateCommand(AddressOf OnEnviarCobroTarjeta, AddressOf CanEnviarCobroTarjeta)
        CopiarAlPortapapelesCommand = New DelegateCommand(AddressOf OnCopiarAlPortapapeles)
        CrearAlbaranVentaCommand = New DelegateCommand(AddressOf OnCrearAlbaranVenta, AddressOf CanCrearAlbaranVenta)
        CrearFacturaVentaCommand = New DelegateCommand(AddressOf OnCrearFacturaVenta, AddressOf CanCrearFacturaVenta)
        CrearAlbaranYFacturaVentaCommand = New DelegateCommand(AddressOf OnCrearAlbaranYFacturaVenta, AddressOf CanCrearAlbaranYFacturaVenta)
        ImprimirFacturaCommand = New DelegateCommand(AddressOf OnImprimirFactura, AddressOf CanImprimirFactura)
        ImprimirAlbaranCommand = New DelegateCommand(AddressOf OnImprimirAlbaran, AddressOf CanImprimirAlbaran)
        CopiarEnlaceCommand = New DelegateCommand(Of String)(AddressOf OnCopiarEnlace)

        EsGrupoQuePuedeFacturar = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) OrElse configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)

        Dim unused1 = eventAggregator.GetEvent(Of ProductoSeleccionadoEvent).Subscribe(AddressOf InsertarProducto)
        Dim unused = eventAggregator.GetEvent(Of SacarPickingEvent).Subscribe(AddressOf ActualizarLookup)
        Dim unused2 = eventAggregator.GetEvent(Of PedidoCreadoEvent).Subscribe(AddressOf OnPedidoCreadoEnDetalle)
    End Sub

    Private Sub ActualizarLookup()
        If Not IsNothing(pedido) Then
            eventAggregator.GetEvent(Of PedidoModificadoEvent).Publish(pedido.Model)
        End If
    End Sub
    Private Async Sub InsertarProducto(productoSeleccionado As String)
        If Not IsNothing(lineaActual) Then
            lineaActual.Producto = productoSeleccionado
            Await CargarDatosProducto(productoSeleccionado, lineaActual.Cantidad)
        End If
    End Sub

#Region "Propiedades"

    Private _almacenUsuario As String
    Public Property AlmacenUsuario As String
        Get
            Return _almacenUsuario
        End Get
        Set(value As String)
            If SetProperty(_almacenUsuario, value) Then
                CrearAlbaranVentaCommand.RaiseCanExecuteChanged()
                CrearFacturaVentaCommand.RaiseCanExecuteChanged()
                CrearAlbaranYFacturaVentaCommand.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _celdaActual As DataGridCellInfo
    Public Property celdaActual As DataGridCellInfo
        Get
            Return _celdaActual
        End Get
        Set(value As DataGridCellInfo)
            Dim unused = SetProperty(_celdaActual, value)
        End Set
    End Property

    Private _cobroTarjetaCorreo As String
    Public Property CobroTarjetaCorreo As String
        Get
            Return _cobroTarjetaCorreo
        End Get
        Set(value As String)
            Dim unused = SetProperty(_cobroTarjetaCorreo, value)
            EnviarCobroTarjetaCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _cobroTarjetaImporte As Decimal
    Public Property CobroTarjetaImporte As Decimal
        Get
            Return _cobroTarjetaImporte
        End Get
        Set(value As Decimal)
            Dim unused = SetProperty(_cobroTarjetaImporte, value)
            EnviarCobroTarjetaCommand.RaiseCanExecuteChanged()
            If IsNothing(pedido.Efectos) Then
                pedido.Efectos = New ListaEfectos(value, pedido.formaPago, pedido.ccc)
            Else
                pedido.Efectos.ImporteTotal = value
            End If
        End Set
    End Property

    Private _cobroTarjetaMovil As String
    Public Property CobroTarjetaMovil As String
        Get
            Return _cobroTarjetaMovil
        End Get
        Set(value As String)
            Dim unused = SetProperty(_cobroTarjetaMovil, value)
            EnviarCobroTarjetaCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _delegacionUsuario As String
    Public Property DelegacionUsuario As String
        Get
            Return _delegacionUsuario
        End Get
        Set(value As String)
            Dim unused = SetProperty(_delegacionUsuario, value)
        End Set
    End Property

    Private _descuentoPedido As Decimal
    Public Property descuentoPedido As Decimal
        Get
            Return _descuentoPedido
        End Get
        Set(value As Decimal)
            If _descuentoPedido <> value Then
                If Not dialogService.ShowConfirmationAnswer("Descuento Pedido", "¿Desea aplicar el descuento a todas las líneas?") Then
                    Return
                End If
                Dim unused = SetProperty(_descuentoPedido, value)
                cmdPonerDescuentoPedido.Execute()
            End If
        End Set
    End Property

    Private _direccionEntregaSeleccionada As DireccionesEntregaCliente
    Public Property DireccionEntregaSeleccionada As DireccionesEntregaCliente
        Get
            Return _direccionEntregaSeleccionada
        End Get
        Set(value As DireccionesEntregaCliente)
            If SetProperty(_direccionEntregaSeleccionada, value) Then
                If EstaCreandoPedido AndAlso Not IsNothing(pedido) Then
                    pedido.formaPago = value.formaPago
                    pedido.plazosPago = value.plazosPago
                    pedido.iva = value.iva
                    pedido.vendedor = value.vendedor
                    pedido.ruta = value.ruta
                    pedido.periodoFacturacion = value.periodoFacturacion
                End If
            End If
        End Set
    End Property

    Private _clienteCompleto As ControlesUsuario.Models.ClienteDTO
    Public Property ClienteCompleto As ControlesUsuario.Models.ClienteDTO
        Get
            Return _clienteCompleto
        End Get
        Set(value As ControlesUsuario.Models.ClienteDTO)
            If SetProperty(_clienteCompleto, value) Then
                ' Actualizar origen y contactoCobro cuando cambia el cliente
                If Not IsNothing(pedido) AndAlso Not IsNothing(value) Then
                    pedido.Model.origen = value.empresa
                    pedido.Model.contactoCobro = value.contacto
                End If
            End If
        End Set
    End Property

    Private _esGrupoAlmacen As Boolean

    Public Property EsGrupoQuePuedeFacturar As Boolean
        Get
            Return _esGrupoAlmacen
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_esGrupoAlmacen, value)
        End Set
    End Property

    Private _estaBloqueado As Boolean
    Public Property estaBloqueado() As Boolean
        Get
            Return _estaBloqueado
        End Get
        Set(ByVal value As Boolean)
            Dim unused = SetProperty(_estaBloqueado, value)
        End Set
    End Property

    Public ReadOnly Property EstaCreandoPedido As Boolean
        Get
            Return Not IsNothing(pedido) AndAlso pedido.numero = 0
        End Get
    End Property

    Private _fechaEntrega As Date
    Public Property fechaEntrega As Date
        Get
            Return _fechaEntrega
        End Get
        Set(value As Date)
            Dim unused = SetProperty(_fechaEntrega, value)
            If estaActualizarFechaActivo Then
                cmdCambiarFechaEntrega.Execute()
                RaisePropertyChanged(NameOf(pedido))
            End If
        End Set
    End Property

    Private _formaVentaUsuario As String
    Public Property FormaVentaUsuario As String
        Get
            Return _formaVentaUsuario
        End Get
        Set(value As String)
            Dim unused = SetProperty(_formaVentaUsuario, value)
        End Set
    End Property

    Private _papelConMembrete As Boolean = False
    Public Property PapelConMembrete As Boolean
        Get
            Return _papelConMembrete
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_papelConMembrete, value)
        End Set
    End Property

    Private _lineaActual As LineaPedidoVentaWrapper
    Public Property lineaActual As LineaPedidoVentaWrapper
        Get
            Return _lineaActual
        End Get
        Set(value As LineaPedidoVentaWrapper)
            Dim unused = SetProperty(_lineaActual, value)
            RaisePropertyChanged(NameOf(pedido))
            ImprimirFacturaCommand.RaiseCanExecuteChanged()
            ImprimirAlbaranCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _listaEnlacesSeguimiento As List(Of EnvioAgenciaDTO)
    Public Property ListaEnlacesSeguimiento As List(Of EnvioAgenciaDTO)
        Get
            Return _listaEnlacesSeguimiento
        End Get
        Set(value As List(Of EnvioAgenciaDTO))
            Dim unused = SetProperty(_listaEnlacesSeguimiento, value)
        End Set
    End Property

    Public ReadOnly Property mostrarAceptarPresupuesto()
        Get
            Return (Not IsNothing(pedido)) AndAlso pedido.EsPresupuesto
        End Get
    End Property

    Private _pedido As PedidoVentaWrapper
    Public Property pedido As PedidoVentaWrapper
        Get
            Return _pedido
        End Get
        Set(ByVal value As PedidoVentaWrapper)
            If IsNothing(value) OrElse _pedido?.Equals(value) Then
                Return
            End If
            If Not IsNothing(_pedido) Then
                RemoveHandler _pedido.IvaCambiado, AddressOf OnIvaCambiado
            End If
            Dim unused = SetProperty(_pedido, value)
            If Not IsNothing(_pedido) Then
                AddHandler _pedido.IvaCambiado, AddressOf OnIvaCambiado
            End If
            eventAggregator.GetEvent(Of PedidoModificadoEvent).Publish(pedido.Model)
            estaActualizarFechaActivo = False
            Dim linea As LineaPedidoVentaDTO = pedido.Model.Lineas.FirstOrDefault(Function(l) l.estado >= -1 And l.estado <= 1)
            If Not IsNothing(linea) AndAlso Not IsNothing(linea.fechaEntrega) Then
                fechaEntrega = linea.fechaEntrega
            End If
            estaActualizarFechaActivo = True
            vendedorPorGrupo = pedido.VendedoresGrupoProducto?.FirstOrDefault
            If IsNothing(vendedorPorGrupo) Then
                vendedorPorGrupo = New VendedorGrupoProductoDTO With {
                    .vendedor = Nothing,
                    .grupoProducto = "PEL" 'lo ponemos así porque el programa está puesto solo para peluquería. Sería fácil modificarlo para más grupos
                }
                pedido.VendedoresGrupoProducto.Add(vendedorPorGrupo)
            End If
            RaisePropertyChanged(NameOf(mostrarAceptarPresupuesto))
            RaisePropertyChanged(NameOf(EstaCreandoPedido))
            RaisePropertyChanged(NameOf(TextoBotonGuardar))
            AceptarPresupuestoCommand.RaiseCanExecuteChanged()
            DescargarPresupuestoCommand.RaiseCanExecuteChanged()
            CrearAlbaranVentaCommand.RaiseCanExecuteChanged()
            CrearFacturaVentaCommand.RaiseCanExecuteChanged()
            CrearAlbaranYFacturaVentaCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _plazoPagoCompleto As PlazosPago
    Public Property PlazoPagoCompleto As PlazosPago
        Get
            Return _plazoPagoCompleto
        End Get
        Set(value As PlazosPago)
            If SetProperty(_plazoPagoCompleto, value) Then
                If IsNothing(PlazoPagoCompleto) Then
                    Return
                End If
                pedido.primerVencimiento = pedido.fecha.Value
                If PlazoPagoCompleto.diasPrimerPlazo <> 0 Then
                    pedido.primerVencimiento = pedido.primerVencimiento.Value.AddDays(PlazoPagoCompleto.diasPrimerPlazo)
                End If
                If PlazoPagoCompleto.mesesPrimerPlazo <> 0 Then
                    pedido.primerVencimiento = pedido.primerVencimiento.Value.AddMonths(PlazoPagoCompleto.mesesPrimerPlazo)
                End If
            End If
        End Set
    End Property

    Public ReadOnly Property TextoBotonGuardar As String
        Get
            Return If(EstaCreandoPedido, "Crear Pedido", "Modificar Pedido")
        End Get
    End Property

    Private _textoBusyIndicator As String
    Public Property textoBusyIndicator As String
        Get
            Return _textoBusyIndicator
        End Get
        Set(value As String)
            Dim unused = SetProperty(_textoBusyIndicator, value)
        End Set
    End Property
    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            Dim unused = SetProperty(_titulo, value)
        End Set
    End Property
    Private _vendedorPorGrupo As VendedorGrupoProductoDTO
    Public Property vendedorPorGrupo As VendedorGrupoProductoDTO
        Get
            Return _vendedorPorGrupo
        End Get
        Set(value As VendedorGrupoProductoDTO)
            Dim unused = SetProperty(_vendedorPorGrupo, value)
            If IsNothing(pedido) Then
                Return
            End If
            If IsNothing(pedido.VendedoresGrupoProducto) Then
                pedido.VendedoresGrupoProducto = New ObservableCollection(Of VendedorGrupoProductoDTO)
            End If
            If IsNothing(pedido.VendedoresGrupoProducto.Count = 0) Then
                pedido.VendedoresGrupoProducto.Add(vendedorPorGrupo)
            End If
        End Set
    End Property

    Private _serieFacturacionDefecto As String
    Public Property SerieFacturacionDefecto As String
        Get
            Return _serieFacturacionDefecto
        End Get
        Set(value As String)
            Dim unused = SetProperty(_serieFacturacionDefecto, value)
        End Set
    End Property

    Private _vistoBuenoVentas As String
    Public Property VistoBuenoVentas As String
        Get
            Return _vistoBuenoVentas
        End Get
        Set(value As String)
            Dim unused = SetProperty(_vistoBuenoVentas, value)
        End Set
    End Property

#End Region

#Region "Comandos"
    Private _cmdAbrirPicking As DelegateCommand
    Public Property cmdAbrirPicking As DelegateCommand
        Get
            Return _cmdAbrirPicking
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdAbrirPicking, value)
        End Set
    End Property
    Private Sub OnAbrirPicking()
        dialogService.ShowDialog("PickingPopupView", New DialogParameters From {
            {"pedidoPicking", pedido.Model}
        }, Nothing)
    End Sub

    Private _aceptarPresupuestoCommand As DelegateCommand
    Public Property AceptarPresupuestoCommand As DelegateCommand
        Get
            Return _aceptarPresupuestoCommand
        End Get
        Set(value As DelegateCommand)
            Dim unused = SetProperty(_aceptarPresupuestoCommand, value)
        End Set
    End Property
    Private Function CanAceptarPresupuesto() As Boolean
        Return (Not IsNothing(pedido)) AndAlso pedido.EsPresupuesto
    End Function
    Private Sub OnAceptarPresupuesto()
        pedido.EsPresupuesto = False
        cmdModificarPedido.Execute()
    End Sub

    Private _cmdActualizarTotales As DelegateCommand
    Public Property cmdActualizarTotales As DelegateCommand
        Get
            Return _cmdActualizarTotales
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdActualizarTotales, value)
        End Set
    End Property
    Private Sub OnActualizarTotales()
        RaisePropertyChanged(NameOf(pedido))
        CobroTarjetaImporte = pedido.Total
    End Sub

    Private _cmdCambiarFechaEntrega As DelegateCommand
    Public Property cmdCambiarFechaEntrega As DelegateCommand
        Get
            Return _cmdCambiarFechaEntrega
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdCambiarFechaEntrega, value)
        End Set
    End Property
    Private Sub OnCambiarFechaEntrega()
        For Each linea In pedido.Lineas
            linea.fechaEntrega = fechaEntrega
        Next

        RaisePropertyChanged(NameOf(pedido))
    End Sub

    Private _cmdCambiarIva As DelegateCommand
    Public Property cmdCambiarIva As DelegateCommand
        Get
            Return _cmdCambiarIva
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdCambiarIva, value)
        End Set
    End Property
    Private Sub OnCambiarIva()
        If IsNothing(pedido) Then
            Return
        End If
        pedido.iva = IIf(IsNothing(pedido.iva), ivaOriginal, Nothing)
        RaisePropertyChanged(NameOf(pedido))
    End Sub

    Private _cmdCargarPedido As DelegateCommand(Of ResumenPedido)
    Public Property cmdCargarPedido As DelegateCommand(Of ResumenPedido)
        Get
            Return _cmdCargarPedido
        End Get
        Private Set(value As DelegateCommand(Of ResumenPedido))
            Dim unused = SetProperty(_cmdCargarPedido, value)
        End Set
    End Property
    Private Async Sub OnCargarPedido(resumen As ResumenPedido)
        Try
            If Not IsNothing(resumen) Then
                ' CASO 1: Pedido nuevo (numero = 0)
                If resumen.numero = 0 Then
                    Titulo = "Nuevo Pedido"
                    pedido = Await CrearPedidoVentaWrapperNuevo(resumen.empresa)
                    fechaEntrega = Date.Today
                    ListaEnlacesSeguimiento = New List(Of EnvioAgenciaDTO)()
                    If Not IsNothing(pedido) Then
                        ivaOriginal = IVA_POR_DEFECTO
                        CobroTarjetaImporte = 0
                    End If

                    ' CASO 2: Pedido existente
                ElseIf resumen.numero > 0 Then
                    Titulo = "Pedido Venta (" + resumen.numero.ToString + ")"
                    Dim pedidoDTO As PedidoVentaDTO = Await servicio.cargarPedido(resumen.empresa, resumen.numero)
                    pedido = New PedidoVentaWrapper(pedidoDTO)
                    ListaEnlacesSeguimiento = Await servicio.CargarEnlacesSeguimiento(resumen.empresa, resumen.numero)
                    If Not IsNothing(pedido) Then
                        ivaOriginal = IIf(IsNothing(pedido.iva), IVA_POR_DEFECTO, pedido.iva)
                        CobroTarjetaImporte = pedido.Total
                    End If
                End If
            Else
                Titulo = "Lista de Pedidos"
            End If
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        End Try
    End Sub

    Private _cargarProductoCommand As DelegateCommand(Of LineaPedidoVentaDTO)
    Public Property CargarProductoCommand As DelegateCommand(Of LineaPedidoVentaDTO)
        Get
            Return _cargarProductoCommand
        End Get
        Private Set(value As DelegateCommand(Of LineaPedidoVentaDTO))
            Dim unused = SetProperty(_cargarProductoCommand, value)
        End Set
    End Property
    Private Sub OnCargarProducto(linea As LineaPedidoVentaDTO)
        Dim parameters As New NavigationParameters From {
            {"numeroProductoParameter", linea.Producto}
        }
        regionManager.RequestNavigate("MainRegion", "ProductoView", parameters)
    End Sub

    Private _cmdCeldaModificada As DelegateCommand(Of DataGridCellEditEndingEventArgs)
    Public Property cmdCeldaModificada As DelegateCommand(Of DataGridCellEditEndingEventArgs)
        Get
            Return _cmdCeldaModificada
        End Get
        Private Set(value As DelegateCommand(Of DataGridCellEditEndingEventArgs))
            Dim unused = SetProperty(_cmdCeldaModificada, value)
        End Set
    End Property
    Private Async Sub OnCeldaModificada(eventArgs As DataGridCellEditEndingEventArgs)
        Dim lineaEditada As LineaPedidoVentaWrapper = CType(eventArgs.Row.DataContext, LineaPedidoVentaWrapper)
        If Not lineaEditada.Equals(lineaActual) Then
            lineaActual = lineaEditada
        End If
        If IsNothing(lineaActual.Almacen) Then
            lineaActual.Almacen = pedido.Lineas.FirstOrDefault()?.Almacen
        End If
        If IsNothing(lineaActual.Almacen) Then
            lineaActual.Almacen = AlmacenUsuario
        End If
        If IsNothing(lineaActual.formaVenta) Then
            lineaActual.formaVenta = pedido.Lineas.FirstOrDefault()?.formaVenta
        End If
        If IsNothing(lineaActual.formaVenta) Then
            lineaActual.formaVenta = FormaVentaUsuario
        End If
        If IsNothing(lineaActual.delegacion) Then
            lineaActual.delegacion = pedido.Lineas.FirstOrDefault()?.delegacion
        End If
        If IsNothing(lineaActual.delegacion) Then
            lineaActual.delegacion = DelegacionUsuario
        End If
        If IsNothing(lineaActual.fechaEntrega) OrElse lineaActual.fechaEntrega = Date.MinValue Then
            lineaActual.fechaEntrega = fechaEntrega
        End If
        If lineaActual.id = 0 AndAlso Not String.IsNullOrEmpty(VistoBuenoVentas) Then
            lineaActual.vistoBueno = VistoBuenoVentas = "1" OrElse VistoBuenoVentas.ToLower() = "true"
        End If
        Dim textoNuevo As String = String.Empty
        Dim esTextBox As Boolean = False
        If TypeOf eventArgs.EditingElement Is TextBox Then
            Dim textBox As TextBox = DirectCast(eventArgs.EditingElement, TextBox)
            textoNuevo = textBox.Text
            esTextBox = True
        End If

        If esTextBox AndAlso eventArgs.Column.Header = "Producto" AndAlso Not IsNothing(lineaActual) AndAlso textoNuevo <> lineaActual.Producto Then
            Await CargarDatosProducto(textoNuevo, lineaActual.Cantidad)
        End If
        If esTextBox AndAlso eventArgs.Column.Header = "Cantidad" AndAlso Not IsNothing(lineaActual) AndAlso CType(textoNuevo, Short) <> lineaActual.Cantidad Then
            Await CargarDatosProducto(lineaActual.Producto, textoNuevo)
        End If
        If (esTextBox AndAlso eventArgs.Column.Header = "Precio") OrElse eventArgs.Column.Header = "Descuento" Then
            Dim textBox As TextBox = eventArgs.EditingElement
            ' Windows debería hacer que el teclado numérico escribiese coma en vez de punto
            ' pero como no lo hace, lo cambiamos nosotros
            textBox.Text = Replace(textBox.Text, ".", ",")
            Dim style As NumberStyles = NumberStyles.Number Or NumberStyles.AllowCurrencySymbol
            Dim culture As CultureInfo = CultureInfo.CurrentCulture

            If eventArgs.Column.Header = "Precio" Then
                If Not Double.TryParse(textBox.Text, style, culture, (lineaActual.PrecioUnitario)) Then
                    Return
                End If
            Else
                Dim valorDescuento As Double

                If Not Double.TryParse(textBox.Text, style, culture, valorDescuento) Then
                    Return
                Else
                    lineaActual.DescuentoLinea = valorDescuento / 100
                End If
            End If
        End If
        If eventArgs.Column.Header = "Aplicar Dto." Then
            cmdActualizarTotales.Execute() ' ¿por qué no llamar directamente a raisepropertychanged?
        End If
    End Sub

    Private Async Function CargarDatosProducto(numeroProducto As String, cantidad As Short) As Task
        Dim lineaCambio As LineaPedidoVentaWrapper = lineaActual 'para que se mantenga fija aunque cambie la linea actual durante el asíncrono
        Dim producto As Producto = Await servicio.cargarProducto(pedido.empresa, numeroProducto, pedido.cliente, pedido.contacto, cantidad)
        If Not IsNothing(producto) Then
            If lineaCambio.Producto <> producto.producto Then
                lineaCambio.Producto = producto.producto
            End If
            lineaCambio.PrecioUnitario = producto.precio
            lineaCambio.texto = producto.nombre
            lineaCambio.AplicarDescuento = producto.aplicarDescuento
            lineaCambio.DescuentoProducto = producto.descuento
            lineaCambio.iva = producto.iva
            If IsNothing(lineaCambio.Usuario) Then
                lineaCambio.Usuario = configuracion.usuario
            End If
        End If
        If pedido.EsPresupuesto Then
            lineaCambio.estado = -3
        End If
    End Function

    Private _copiarAlPortapapelesCommand As DelegateCommand
    Public Property CopiarAlPortapapelesCommand As DelegateCommand
        Get
            Return _copiarAlPortapapelesCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_copiarAlPortapapelesCommand, value)
        End Set
    End Property
    Private Sub OnCopiarAlPortapapeles()
        Dim html As New StringBuilder()
        Dim unused2 = html.Append(Constantes.Formatos.HTML_CLIENTE_P_TAG)
        Dim unused1 = html.Append(ToString.Replace(vbCr, "<br/>"))
        Dim unused = html.Append("</p>")
        ClipboardHelper.CopyToClipboard(html.ToString, ToString)
        dialogService.ShowNotification("Datos del pedido copiados al portapapeles")
    End Sub

    Private _copiarEnlaceCommand As DelegateCommand(Of String)
    Public Property CopiarEnlaceCommand As DelegateCommand(Of String)
        Get
            Return _copiarEnlaceCommand
        End Get
        Private Set(value As DelegateCommand(Of String))
            Dim unused = SetProperty(_copiarEnlaceCommand, value)
        End Set
    End Property
    Private Sub OnCopiarEnlace(url As String)
        Try
            If Not String.IsNullOrEmpty(url) Then
                Clipboard.SetText(url)
                Dim unused1 = MessageBox.Show("Enlace copiado al portapapeles", "Información", MessageBoxButton.OK, MessageBoxImage.Information)
            End If
        Catch ex As Exception
            Dim unused = MessageBox.Show($"Error al copiar enlace: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub


    Private _crearAlbaranVentaCommand As DelegateCommand
    Public Property CrearAlbaranVentaCommand As DelegateCommand
        Get
            Return _crearAlbaranVentaCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_crearAlbaranVentaCommand, value)
        End Set
    End Property
    Private Function CanCrearAlbaranVenta() As Boolean
        Return Not IsNothing(pedido) AndAlso Not IsNothing(pedido.Lineas) AndAlso
            pedido.Lineas.Any(Function(l) l.estado < Constantes.LineasPedido.ESTADO_ALBARAN AndAlso l.estado >= Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE) AndAlso
            pedido.Lineas.Any(Function(l) l.Almacen = AlmacenUsuario)
    End Function

    Private Async Sub OnCrearAlbaranVenta()
        If Not dialogService.ShowConfirmationAnswer("Crear albarán", "¿Desea crear el albarán del pedido?") Then
            Return
        End If
        Try
            Dim albaran As Integer = Await servicio.CrearAlbaranVenta(pedido.empresa.ToString, pedido.numero.ToString)
            dialogService.ShowNotification($"Albarán {albaran} creado correctamente")
        Catch ex As Exception
            dialogService.ShowError("No se ha podido crear el albarán")
        Finally
            CrearAlbaranVentaCommand.RaiseCanExecuteChanged()
            CrearFacturaVentaCommand.RaiseCanExecuteChanged()
            CrearAlbaranYFacturaVentaCommand.RaiseCanExecuteChanged()
        End Try
    End Sub

    Private _crearFacturaVentaCommand As DelegateCommand
    Public Property CrearFacturaVentaCommand As DelegateCommand
        Get
            Return _crearFacturaVentaCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_crearFacturaVentaCommand, value)
        End Set
    End Property
    Private Function CanCrearFacturaVenta() As Boolean
        Return Not IsNothing(pedido) AndAlso pedido.periodoFacturacion <> Constantes.PeriodosFacturacion.FIN_DE_MES AndAlso Not IsNothing(pedido.Lineas) AndAlso
            pedido.Lineas.Any(Function(l) l.estado = Constantes.LineasPedido.ESTADO_ALBARAN) AndAlso
            pedido.Lineas.Any(Function(l) l.Almacen = AlmacenUsuario)
    End Function

    Private Async Sub OnCrearFacturaVenta()
        If Not dialogService.ShowConfirmationAnswer("Crear factura", "¿Desea crear la factura del pedido?") Then
            Return
        End If
        Try
            Dim factura As String = Await servicio.CrearFacturaVenta(pedido.empresa.ToString, pedido.numero.ToString)
            If factura = Constantes.PeriodosFacturacion.FIN_DE_MES Then
                Throw New Exception("No se pudo crear factura porque el cliente es de fin de mes")
            End If
            cmdCargarPedido.Execute(New ResumenPedido With {.empresa = pedido.empresa, .numero = pedido.numero})
            dialogService.ShowNotification($"Factura {factura} creada correctamente")
            Await ImprimirFactura(factura)
        Catch ex As Exception
            dialogService.ShowError($"No se ha podido crear la factura:\n {ex.Message}")
        Finally
            CrearFacturaVentaCommand.RaiseCanExecuteChanged()
            CrearAlbaranYFacturaVentaCommand.RaiseCanExecuteChanged()
        End Try
    End Sub
    Private _imprimirFacturaCommand As DelegateCommand
    Public Property ImprimirFacturaCommand As DelegateCommand
        Get
            Return _imprimirFacturaCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_imprimirFacturaCommand, value)
        End Set
    End Property
    Private Function CanImprimirFactura() As Boolean
        Return Not IsNothing(pedido) AndAlso
            Not IsNothing(lineaActual) AndAlso
            lineaActual.estaFacturada AndAlso
            Not String.IsNullOrEmpty(lineaActual.Factura)
    End Function
    Private Async Sub OnImprimirFactura()
        Await ImprimirFactura(lineaActual.Factura)
    End Sub
    Private Async Function ImprimirFactura(factura As String) As Task
        If Not dialogService.ShowConfirmationAnswer("Abrir factura", "¿Desea abrir el documento de la factura?") Then
            Return
        End If
        Try
            Dim pathFactura = Await servicio.DescargarFactura(pedido.empresa, factura, pedido.cliente, PapelConMembrete)
            Dim unused = Process.Start(New ProcessStartInfo(pathFactura) With {
                .UseShellExecute = True
            })
        Catch ex As Exception
            Dim mensajeError = If(IsNothing(ex.InnerException), ex.Message, ex.InnerException.Message)
            dialogService.ShowError($"Error al abrir la factura: {mensajeError}")
        End Try
    End Function

    Private _imprimirAlbaranCommand As DelegateCommand
    Public Property ImprimirAlbaranCommand As DelegateCommand
        Get
            Return _imprimirAlbaranCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_imprimirAlbaranCommand, value)
        End Set
    End Property
    Private Function CanImprimirAlbaran() As Boolean
        Return Not IsNothing(pedido) AndAlso
            Not IsNothing(lineaActual) AndAlso
            lineaActual.estaAlbaraneada AndAlso
            lineaActual.Albaran.HasValue AndAlso
            lineaActual.Albaran.Value > 0
    End Function
    Private Async Sub OnImprimirAlbaran()
        Await ImprimirAlbaran(lineaActual.Albaran.Value)
    End Sub
    Private Async Function ImprimirAlbaran(numeroAlbaran As Integer) As Task
        If Not dialogService.ShowConfirmationAnswer("Abrir albarán", "¿Desea abrir el documento del albarán?") Then
            Return
        End If
        Try
            Dim pathAlbaran = Await servicio.DescargarAlbaran(pedido.empresa, numeroAlbaran, pedido.cliente, PapelConMembrete)
            Dim unused = Process.Start(New ProcessStartInfo(pathAlbaran) With {
                .UseShellExecute = True
            })
        Catch ex As Exception
            Dim mensajeError = If(IsNothing(ex.InnerException), ex.Message, ex.InnerException.Message)
            dialogService.ShowError($"Error al abrir el albarán: {mensajeError}")
        End Try
    End Function

    Private _crearAlbaranYFacturaVentaCommand As DelegateCommand
    Public Property CrearAlbaranYFacturaVentaCommand As DelegateCommand
        Get
            Return _crearAlbaranYFacturaVentaCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_crearAlbaranYFacturaVentaCommand, value)
        End Set
    End Property
    Private Function CanCrearAlbaranYFacturaVenta() As Boolean
        Return Not IsNothing(pedido) AndAlso pedido.periodoFacturacion <> Constantes.PeriodosFacturacion.FIN_DE_MES AndAlso Not IsNothing(pedido.Lineas) AndAlso
            pedido.Lineas.Any(Function(l) l.estado < Constantes.LineasPedido.ESTADO_FACTURA AndAlso l.estado >= Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE) AndAlso
            pedido.Lineas.Any(Function(l) l.Almacen = AlmacenUsuario)
    End Function

    Private Async Sub OnCrearAlbaranYFacturaVenta()
        If Not dialogService.ShowConfirmationAnswer("Crear albarán y factura", "¿Desea crear la factura del pedido directamente?") Then
            Return
        End If
        Try
            Dim albaran As Integer = Await servicio.CrearAlbaranVenta(pedido.empresa.ToString, pedido.numero.ToString)
            Dim factura As String = Await servicio.CrearFacturaVenta(pedido.empresa.ToString, pedido.numero.ToString)
            cmdCargarPedido.Execute(New ResumenPedido With {.empresa = pedido.empresa, .numero = pedido.numero})
            dialogService.ShowNotification($"Albarán {albaran} y factura {factura} creados correctamente")
            Await ImprimirFactura(factura)
        Catch ex As Exception
            dialogService.ShowError("No se ha podido crear el albarán o la factura")
        End Try
    End Sub


    Private _descargarPresupuestoCommand As DelegateCommand
    Public Property DescargarPresupuestoCommand As DelegateCommand
        Get
            Return _descargarPresupuestoCommand
        End Get
        Set(value As DelegateCommand)
            Dim unused = SetProperty(_descargarPresupuestoCommand, value)
        End Set
    End Property
    Private Function CanDescargarPresupuesto() As Boolean
        Return Not IsNothing(pedido) 'AndAlso pedido.EsPresupuesto
    End Function
    Private Async Sub OnDescargarPresupuesto()
        textoBusyIndicator = "Generando proforma..."
        estaBloqueado = True

        Try
            Dim path As String = Await servicio.DescargarFactura(pedido.empresa, pedido.numero.ToString, pedido.cliente, PapelConMembrete)
            Dim pathDirectorio As String = path.Substring(0, path.LastIndexOf("\"))

            ' Abrimos la carpeta de descargas
            Dim unused = Process.Start(New ProcessStartInfo(pathDirectorio) With {
                .UseShellExecute = True
            })
        Catch ex As Exception
            Dim mensajeError = If(IsNothing(ex.InnerException), ex.Message, ex.InnerException.Message)
            dialogService.ShowError(mensajeError)
        Finally
            estaBloqueado = False
            textoBusyIndicator = String.Empty
        End Try
    End Sub

    Private _enviarCobroTarjetaCommand As DelegateCommand
    Public Property EnviarCobroTarjetaCommand As DelegateCommand
        Get
            Return _enviarCobroTarjetaCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_enviarCobroTarjetaCommand, value)
        End Set
    End Property
    Private Function CanEnviarCobroTarjeta() As Boolean
        Return (Not String.IsNullOrEmpty(CobroTarjetaCorreo) OrElse Not String.IsNullOrEmpty(CobroTarjetaMovil)) AndAlso CobroTarjetaImporte > 0
    End Function

    Private Sub OnEnviarCobroTarjeta()
        If Not dialogService.ShowConfirmationAnswer("Cobro tarjeta", "¿Desea enviar el enlace de cobro con tarjeta?") Then
            Return
        End If
        servicio.EnviarCobroTarjeta(CobroTarjetaCorreo, CobroTarjetaMovil, CobroTarjetaImporte, pedido.numero.ToString, pedido.cliente)
    End Sub

    Private _cmdModificarPedido As DelegateCommand
    Public Property cmdModificarPedido As DelegateCommand
        Get
            Return _cmdModificarPedido
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdModificarPedido, value)
        End Set
    End Property
    Private Async Sub OnModificarPedido()
        textoBusyIndicator = "Modificando pedido..."
        estaBloqueado = True

        ' Quitamos el vendedor por grupo ficticio que se creó al cargar el pedido (si sigue existiendo)
        If Not IsNothing(pedido.VendedoresGrupoProducto) Then
            Dim vendedorPorGrupo As VendedorGrupoProductoDTO = pedido.VendedoresGrupoProducto.FirstOrDefault
            If Not IsNothing(vendedorPorGrupo) AndAlso vendedorPorGrupo.vendedor = String.Empty Then
                Dim unused = pedido.VendedoresGrupoProducto.Remove(vendedorPorGrupo)
            End If
        End If

        ' Modificamos el usuario del pedido
        pedido.Model.Usuario = configuracion.usuario


        Try
            Dim esPedidoNuevo As Boolean = pedido.numero = 0
            Dim crearModificarEx As Exception = Nothing

            If esPedidoNuevo Then
                Try
                    Dim numeroPedidoCreado As Integer = Await servicio.CrearPedido(pedido.Model)
                    pedido.numero = numeroPedidoCreado
                Catch ex As ValidationException
                    crearModificarEx = ex
                    ' Verificar si puede crear sin pasar validación
                    Dim puedeCrearSinPasarValidacion As Boolean =
                    (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
                     configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) OrElse
                     configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)) AndAlso
                    pedido.Lineas.Any(Function(l) l.Almacen = AlmacenUsuario)

                    If Not puedeCrearSinPasarValidacion Then
                        Throw crearModificarEx
                    End If
                End Try

                If crearModificarEx IsNot Nothing Then
                    ' Preguntar al usuario si desea forzar la creación
                    Dim mensaje As String = crearModificarEx.Message & vbCrLf & "¿Desea crearlo de todos modos?"
                    Dim confirmar As Boolean = Await dialogService.ShowConfirmationAsync("Pedido no válido", mensaje)

                    If confirmar Then
                        pedido.Model.CreadoSinPasarValidacion = True
                        Dim numeroPedidoCreado As Integer = Await servicio.CrearPedido(pedido.Model)
                        pedido.numero = numeroPedidoCreado
                    Else
                        Throw crearModificarEx
                    End If
                End If
                RaisePropertyChanged(NameOf(EstaCreandoPedido))
                RaisePropertyChanged(NameOf(TextoBotonGuardar))

                ' Publicar evento con datos completos del cliente
                ' Calcular tieneProductos desde el wrapper (no desde el modelo)
                Dim tieneProductos As Boolean = False
                If Not IsNothing(pedido.Lineas) AndAlso pedido.Lineas.Count > 0 Then
                    ' Contar líneas con tipoLinea = 1
                    tieneProductos = pedido.Lineas.Any(Function(l) Not IsNothing(l.tipoLinea) AndAlso l.tipoLinea.HasValue AndAlso l.tipoLinea.Value = 1)
                End If

                Dim eventArgs As New PedidoCreadoEventArgs With {
                    .Pedido = pedido.Model,
                    .NombreCliente = If(DireccionEntregaSeleccionada?.nombre, String.Empty),
                    .DireccionCliente = If(DireccionEntregaSeleccionada?.direccion, String.Empty),
                    .CodigoPostal = If(DireccionEntregaSeleccionada?.codigoPostal, String.Empty),
                    .Poblacion = If(DireccionEntregaSeleccionada?.poblacion, String.Empty),
                    .Provincia = If(DireccionEntregaSeleccionada?.provincia, String.Empty),
                    .TieneProductos = tieneProductos
                }
                eventAggregator.GetEvent(Of PedidoCreadoEvent).Publish(eventArgs)
                Titulo = $"Pedido Venta ({pedido.numero})"
            Else
                Try
                    Await servicio.modificarPedido(pedido.Model)
                Catch ex As ValidationException
                    crearModificarEx = ex
                    ' Verificar si puede modificar sin pasar validación
                    Dim puedeModificarSinPasarValidacion As Boolean =
                    (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
                     configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) OrElse
                     configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)) AndAlso
                    pedido.Lineas.Any(Function(l) l.Almacen = AlmacenUsuario)

                    If Not puedeModificarSinPasarValidacion Then
                        Throw crearModificarEx
                    End If
                End Try

                If crearModificarEx IsNot Nothing Then
                    ' Preguntar al usuario si desea forzar la modificación
                    Dim mensaje As String = crearModificarEx.Message & vbCrLf & "¿Desea modificarlo de todos modos?"
                    Dim confirmar As Boolean = Await dialogService.ShowConfirmationAsync("Pedido no válido", mensaje)

                    If confirmar Then
                        pedido.Model.CreadoSinPasarValidacion = True
                        Await servicio.modificarPedido(pedido.Model)
                    Else
                        Throw crearModificarEx
                    End If
                End If

                dialogService.ShowNotification("Pedido Modificado", "Pedido " + pedido.numero.ToString + " modificado correctamente")
                eventAggregator.GetEvent(Of PedidoModificadoEvent).Publish(pedido.Model)
            End If
        Catch ex As ValidationException
            dialogService.ShowError("Error de validación:" + vbCrLf + ex.Message)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaBloqueado = False
            CrearAlbaranVentaCommand.RaiseCanExecuteChanged()
            CrearFacturaVentaCommand.RaiseCanExecuteChanged()
            CrearAlbaranYFacturaVentaCommand.RaiseCanExecuteChanged()
        End Try
    End Sub

    Private _cmdPonerDescuentoPedido As DelegateCommand
    Public Property cmdPonerDescuentoPedido As DelegateCommand
        Get
            Return _cmdPonerDescuentoPedido
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdPonerDescuentoPedido, value)
        End Set
    End Property
    Private Function CanPonerDescuentoPedido() As Boolean
        Return Not IsNothing(pedido) AndAlso Not IsNothing(pedido.Lineas)
    End Function
    Private Sub OnPonerDescuentoPedido()
        For Each linea In pedido.Lineas.Where(Function(l) l.AplicarDescuento AndAlso l.estado >= -1 AndAlso l.estado <= 1 AndAlso Not l.picking > 0)
            linea.DescuentoLinea = descuentoPedido
        Next
        RaisePropertyChanged(NameOf(pedido))
    End Sub

    Public Property AbrirEnlaceSeguimientoCommand As DelegateCommand(Of String)
    Private Sub OnAbrirEnlaceSeguimientoCommand(enlace As String)
        Dim unused = Process.Start(New ProcessStartInfo(enlace) With {
            .UseShellExecute = True
        })
    End Sub

#End Region

#Region "Funciones Auxiliares"
    Private Async Function CrearPedidoVentaWrapperNuevo(empresa As String) As Task(Of PedidoVentaWrapper)
        ' Crear un PedidoVentaDTO base para pedido nuevo
        Dim pedidoNuevo As New PedidoVentaDTO With {
            .empresa = empresa,
            .numero = 0, ' Indica que es nuevo
            .fecha = Date.Today,
            .vendedor = String.Empty,
            .ruta = String.Empty,
            .periodoFacturacion = String.Empty,
            .serie = SerieFacturacionDefecto,
            .iva = IVA_POR_DEFECTO,
            .contacto = String.Empty,
            .cliente = String.Empty,
            .origen = String.Empty,
            .contactoCobro = String.Empty,
            .Usuario = configuracion.usuario,
            .Lineas = New ObservableCollection(Of LineaPedidoVentaDTO),
            .VendedoresGrupoProducto = New ObservableCollection(Of VendedorGrupoProductoDTO),
            .EsPresupuesto = False
        }

        Return New PedidoVentaWrapper(pedidoNuevo)
    End Function

    Private Async Sub OnIvaCambiado(nuevoIva As String)
        Try
            If Not String.IsNullOrEmpty(nuevoIva) AndAlso Not IsNothing(pedido) Then
                pedido.Model.ParametrosIva = Await servicio.CargarParametrosIva(pedido.empresa, nuevoIva)
            End If
        Catch ex As Exception
            dialogService.ShowError("Error al cargar los parámetros de IVA: " + ex.Message)
        End Try
    End Sub

    Private Sub OnPedidoCreadoEnDetalle(eventArgs As PedidoCreadoEventArgs)
        ' Actualizar el pedido actual si coincide
        If Not IsNothing(pedido) AndAlso pedido.numero = 0 AndAlso eventArgs.Pedido.empresa = pedido.empresa Then
            pedido = New PedidoVentaWrapper(eventArgs.Pedido)
            Titulo = $"Pedido Venta ({eventArgs.Pedido.numero})"
        End If
    End Sub
#End Region


    Public Overloads Async Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo
        Dim resumen = navigationContext.Parameters("resumenPedidoParameter")

        ' Cargar parámetros de usuario
        AlmacenUsuario = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta)
        FormaVentaUsuario = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.FormaVentaDefecto)
        DelegacionUsuario = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto)
        SerieFacturacionDefecto = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.SerieFacturacionDefecto)
        VistoBuenoVentas = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.VistoBuenoVentas)

        cmdCargarPedido.Execute(resumen)
    End Sub

    Public Function IsNavigationTarget(navigationContext As NavigationContext) As Boolean Implements INavigationAware.IsNavigationTarget

    End Function

    Public Sub OnNavigatedFrom(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedFrom

    End Sub

    Public Overrides Function ToString() As String
        Dim pedidoString As String = $"Pedido {pedido.numero}" + vbCr + $"Cliente {pedido.cliente}/{pedido.contacto}"
        If Not IsNothing(DireccionEntregaSeleccionada) Then
            pedidoString += vbCr + DireccionEntregaSeleccionada.nombre
            pedidoString += vbCr + DireccionEntregaSeleccionada.direccion
            pedidoString += vbCr + $"{DireccionEntregaSeleccionada.codigoPostal} {DireccionEntregaSeleccionada.poblacion} ({DireccionEntregaSeleccionada.provincia})"
        End If
        pedidoString += vbCr + $"Vendedor estética: {pedido.vendedor}"
        If Not IsNothing(vendedorPorGrupo) AndAlso pedido.vendedor <> vendedorPorGrupo.vendedor AndAlso Not String.IsNullOrWhiteSpace(vendedorPorGrupo.vendedor) Then
            pedidoString += vbCr + $"Vendedor peluquería: {vendedorPorGrupo.vendedor}"
        End If
        Return pedidoString
    End Function


End Class
