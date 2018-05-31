Imports System.Globalization
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Practices.Prism.PubSubEvents
Imports Microsoft.Practices.Prism.Regions
Imports Nesto.Contratos
Imports Nesto.Models.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

Public Class DetallePedidoViewModel
    Inherits ViewModelBase
    Implements INavigationAware

    Private estaActualizarFechaActivo As Boolean = True
    Private ReadOnly regionManager As IRegionManager
    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IPedidoVentaService
    Private ReadOnly eventAggregator As IEventAggregator

    Private ivaOriginal As String

    Private Const IVA_POR_DEFECTO = "G21"
    Private Const EMPRESA_POR_DEFECTO = "1"

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IPedidoVentaService, eventAggregator As IEventAggregator)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.eventAggregator = eventAggregator

        cmdAbrirPicking = New DelegateCommand(Of Object)(AddressOf OnAbrirPicking, AddressOf CanAbrirPicking)
        cmdActualizarTotales = New DelegateCommand(Of Object)(AddressOf OnActualizarTotales, AddressOf CanActualizarTotales)
        cmdCambiarFechaEntrega = New DelegateCommand(Of Object)(AddressOf OnCambiarFechaEntrega, AddressOf CanCambiarFechaEntrega)
        cmdCambiarIva = New DelegateCommand(Of Object)(AddressOf OnCambiarIva, AddressOf CanCambiarIva)
        cmdCargarPedido = New DelegateCommand(Of Object)(AddressOf OnCargarPedido, AddressOf CanCargarPedido)
        CargarProductoCommand = New DelegateCommand(Of Object)(AddressOf OnCargarProducto, AddressOf CanCargarProducto)
        cmdCeldaModificada = New DelegateCommand(Of Object)(AddressOf OnCeldaModificada, AddressOf CanCeldaModificada)
        cmdModificarPedido = New DelegateCommand(Of Object)(AddressOf OnModificarPedido, AddressOf CanModificarPedido)
        cmdPonerDescuentoPedido = New DelegateCommand(Of Object)(AddressOf OnPonerDescuentoPedido, AddressOf CanPonerDescuentoPedido)
        cmdSacarPicking = New DelegateCommand(Of Object)(AddressOf OnSacarPicking, AddressOf CanSacarPicking)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)
        PickingPopup = New InteractionRequest(Of INotification)
    End Sub


#Region "Propiedades de Prism"
    Private _NotificationRequest As InteractionRequest(Of INotification)
    Public Property NotificationRequest As InteractionRequest(Of INotification)
        Get
            Return _NotificationRequest
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _NotificationRequest = value
        End Set
    End Property

    Private _ConfirmationRequest As InteractionRequest(Of IConfirmation)
    Public Property ConfirmationRequest As InteractionRequest(Of IConfirmation)
        Get
            Return _ConfirmationRequest
        End Get
        Private Set(value As InteractionRequest(Of IConfirmation))
            _ConfirmationRequest = value
        End Set
    End Property

    Private resultMessage As String
    Public Property InteractionResultMessage As String
        Get
            Return Me.resultMessage
        End Get
        Set(value As String)
            Me.resultMessage = value
            Me.OnPropertyChanged("InteractionResultMessage")
        End Set
    End Property

    Private _PickingPopup As InteractionRequest(Of INotification)
    Public Property PickingPopup As InteractionRequest(Of INotification)
        Get
            Return _PickingPopup
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _PickingPopup = value
        End Set
    End Property



#End Region

#Region "Propiedades"
    Private _celdaActual As DataGridCellInfo
    Public Property celdaActual As DataGridCellInfo
        Get
            Return _celdaActual
        End Get
        Set(value As DataGridCellInfo)
            SetProperty(_celdaActual, value)
        End Set
    End Property

    Private _descuentoPedido As Decimal
    Public Property descuentoPedido As Decimal
        Get
            Return _descuentoPedido
        End Get
        Set(value As Decimal)
            If _descuentoPedido <> value Then
                Me.ConfirmationRequest.Raise(
                    New Confirmation() With {
                        .Content = "¿Desea aplicar el descuento a todas las líneas?", .Title = "Descuento Pedido"
            },
            Sub(c)
                InteractionResultMessage = If(c.Confirmed, "OK", "KO")
            End Sub
        )

                If InteractionResultMessage = "KO" Then
                    Return
                End If
                SetProperty(_descuentoPedido, value)
                cmdPonerDescuentoPedido.Execute(Nothing)
            End If
        End Set
    End Property

    Private _esPickingCliente As Boolean
    Public Property esPickingCliente As Boolean
        Get
            Return _esPickingCliente
        End Get
        Set(value As Boolean)
            SetProperty(_esPickingCliente, value)
        End Set
    End Property

    Private _esPickingPedido As Boolean = True
    Public Property esPickingPedido As Boolean
        Get
            Return _esPickingPedido
        End Get
        Set(value As Boolean)
            SetProperty(_esPickingPedido, value)
        End Set
    End Property

    Private _esPickingRutas As Boolean
    Public Property esPickingRutas As Boolean
        Get
            Return _esPickingRutas
        End Get
        Set(value As Boolean)
            SetProperty(_esPickingRutas, value)
        End Set
    End Property

    Private _estaSacandoPicking As Boolean
    Public Property estaSacandoPicking() As Boolean
        Get
            Return _estaSacandoPicking
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaSacandoPicking, value)
        End Set
    End Property

    Private _fechaEntrega As Date
    Public Property fechaEntrega As Date
        Get
            Return _fechaEntrega
        End Get
        Set(value As Date)
            SetProperty(_fechaEntrega, value)
            If estaActualizarFechaActivo Then
                cmdCambiarFechaEntrega.Execute(Nothing)
                OnPropertyChanged("pedido")
            End If
        End Set
    End Property

    Private _lineaActual As LineaPedidoVentaDTO
    Public Property lineaActual As LineaPedidoVentaDTO
        Get
            Return _lineaActual
        End Get
        Set(value As LineaPedidoVentaDTO)
            SetProperty(_lineaActual, value)
            OnPropertyChanged("pedido")
        End Set
    End Property

    Private _numeroPedidoPicking As Integer
    Public Property numeroPedidoPicking As Integer
        Get
            Return _numeroPedidoPicking
        End Get
        Set(value As Integer)
            SetProperty(_numeroPedidoPicking, value)
        End Set
    End Property

    Private _numeroClientePicking As String
    Public Property numeroClientePicking() As String
        Get
            Return _numeroClientePicking
        End Get
        Set(ByVal value As String)
            SetProperty(_numeroClientePicking, value)
        End Set
    End Property

    Private _pedido As PedidoVentaDTO
    Public Property pedido As PedidoVentaDTO
        Get
            Return _pedido
        End Get
        Set(ByVal value As PedidoVentaDTO)
            SetProperty(_pedido, value)
            If IsNothing(pedido) Then
                Return
            End If
            estaActualizarFechaActivo = False
            Dim linea As LineaPedidoVentaDTO = pedido.LineasPedido.FirstOrDefault(Function(l) l.estado >= -1 And l.estado <= 1)
            If Not IsNothing(linea) AndAlso Not IsNothing(linea.fechaEntrega) Then
                fechaEntrega = linea.fechaEntrega
            End If
            estaActualizarFechaActivo = True
            vendedorPorGrupo = pedido.VendedoresGrupoProducto.FirstOrDefault
            If IsNothing(vendedorPorGrupo) Then
                vendedorPorGrupo = New VendedorGrupoProductoDTO With {
                    .vendedor = Nothing,
                    .grupoProducto = "PEL" 'lo ponemos así porque el programa está puesto solo para peluquería. Sería fácil modificarlo para más grupos
                }
                pedido.VendedoresGrupoProducto.Add(vendedorPorGrupo)
            End If
        End Set
    End Property

    Private _vendedorPorGrupo As VendedorGrupoProductoDTO
    Public Property vendedorPorGrupo As VendedorGrupoProductoDTO
        Get
            Return _vendedorPorGrupo
        End Get
        Set(value As VendedorGrupoProductoDTO)
            SetProperty(_vendedorPorGrupo, value)
            If IsNothing(pedido) Then
                Return
            End If
            If IsNothing(pedido.VendedoresGrupoProducto) OrElse IsNothing(pedido.VendedoresGrupoProducto.Count = 0) Then
                pedido.VendedoresGrupoProducto.Add(vendedorPorGrupo)
            End If
        End Set
    End Property

#End Region

#Region "Comandos"
    Private _cmdAbrirPicking As DelegateCommand(Of Object)
    Public Property cmdAbrirPicking As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirPicking
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdAbrirPicking, value)
        End Set
    End Property
    Private Function CanAbrirPicking(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirPicking(arg As Object)
        If Not IsNothing(pedido) Then
            numeroPedidoPicking = pedido.numero
            numeroClientePicking = pedido.cliente
        Else
            numeroPedidoPicking = 0
        End If
        PickingPopup.Raise(New Notification() With {
                            .Title = "Picking",
                            .Content = Me
                        })
    End Sub

    Private _cmdActualizarTotales As DelegateCommand(Of Object)
    Public Property cmdActualizarTotales As DelegateCommand(Of Object)
        Get
            Return _cmdActualizarTotales
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdActualizarTotales, value)
        End Set
    End Property
    Private Function CanActualizarTotales(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnActualizarTotales(arg As Object)
        OnPropertyChanged("pedido")
    End Sub

    Private _cmdCambiarFechaEntrega As DelegateCommand(Of Object)
    Public Property cmdCambiarFechaEntrega As DelegateCommand(Of Object)
        Get
            Return _cmdCambiarFechaEntrega
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCambiarFechaEntrega, value)
        End Set
    End Property
    Private Function CanCambiarFechaEntrega(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnCambiarFechaEntrega(arg As Object)
        'pedido.LineasPedido.ToList.ForEach(Function(l)
        '                                       l.fechaEntrega = fechaEntrega
        '                                       Return l
        '                                   End Function)
        For Each linea In pedido.LineasPedido
            linea.fechaEntrega = fechaEntrega
        Next

        OnPropertyChanged("pedido")
    End Sub

    Private _cmdCambiarIva As DelegateCommand(Of Object)
    Public Property cmdCambiarIva As DelegateCommand(Of Object)
        Get
            Return _cmdCambiarIva
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCambiarIva, value)
        End Set
    End Property
    Private Function CanCambiarIva(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnCambiarIva(arg As Object)
        If IsNothing(pedido) Then
            Return
        End If
        pedido.iva = IIf(IsNothing(pedido.iva), ivaOriginal, Nothing)
        OnPropertyChanged("pedido")
    End Sub

    Private _cmdCargarPedido As DelegateCommand(Of Object)
    Public Property cmdCargarPedido As DelegateCommand(Of Object)
        Get
            Return _cmdCargarPedido
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCargarPedido, value)
        End Set
    End Property
    Private Function CanCargarPedido(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCargarPedido(arg As Object)
        If Not IsNothing(arg) AndAlso Not IsNothing(arg.numero) Then
            Me.Titulo = "Pedido Venta (" + arg.numero.ToString + ")"
            pedido = Await servicio.cargarPedido(arg.empresa, arg.numero)
            If Not IsNothing(pedido) Then
                ivaOriginal = IIf(IsNothing(pedido.iva), IVA_POR_DEFECTO, pedido.iva)
            End If
        Else
            Me.Titulo = "Lista de Pedidos"
        End If
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
        Dim parameters As NavigationParameters = New NavigationParameters()
        parameters.Add("numeroProductoParameter", lineaActual.producto)
        regionManager.RequestNavigate("MainRegion", "ProductoView", parameters)
    End Sub

    Private _cmdCeldaModificada As DelegateCommand(Of Object)
    Public Property cmdCeldaModificada As DelegateCommand(Of Object)
        Get
            Return _cmdCeldaModificada
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdCeldaModificada, value)
        End Set
    End Property
    Private Function CanCeldaModificada(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnCeldaModificada(arg As Object)
        arg = CType(arg, DataGridCellEditEndingEventArgs)
        If IsNothing(lineaActual.fechaEntrega) OrElse lineaActual.fechaEntrega = DateTime.MinValue Then
            lineaActual.fechaEntrega = fechaEntrega
        End If
        If arg.Column.Header = "Producto" AndAlso Not IsNothing(lineaActual) AndAlso arg.EditingElement.Text <> lineaActual.producto Then
            Dim lineaCambio As LineaPedidoVentaDTO = lineaActual 'para que se mantenga fija aunque cambie la linea actual durante el asíncrono
            Dim producto As Producto = Await servicio.cargarProducto(pedido.empresa, arg.EditingElement.Text, pedido.cliente, pedido.contacto, lineaActual.cantidad)
            If Not IsNothing(producto) Then
                lineaCambio.precio = producto.precio
                lineaCambio.texto = producto.nombre
                lineaCambio.aplicarDescuento = producto.aplicarDescuento
                lineaCambio.descuentoProducto = producto.descuento
                If IsNothing(lineaCambio.usuario) Then
                    lineaCambio.usuario = System.Environment.UserDomainName + "\" + System.Environment.UserName
                End If
            End If
        End If
        If arg.Column.Header = "Precio" OrElse arg.Column.Header = "Descuento" Then
            Dim textBox As TextBox = arg.EditingElement
            ' Windows debería hacer que el teclado numérico escribiese coma en vez de punto
            ' pero como no lo hace, lo cambiamos nosotros
            textBox.Text = Replace(textBox.Text, ".", ",")
            Dim style As NumberStyles = NumberStyles.Number Or NumberStyles.AllowCurrencySymbol
            Dim culture As CultureInfo = CultureInfo.CurrentCulture

            If arg.Column.Header = "Precio" Then
                If Not Double.TryParse(textBox.Text, style, culture, lineaActual.precio) Then
                    Return
                End If
            Else
                If Not Double.TryParse(textBox.Text, style, culture, lineaActual.descuento) Then
                    Return
                Else
                    lineaActual.descuento = lineaActual.descuento / 100
                End If
            End If
        End If
        If arg.Column.Header = "Aplicar Dto." Then
            Await cmdActualizarTotales.Execute(Nothing)
        End If
    End Sub

    Private _cmdModificarPedido As DelegateCommand(Of Object)
    Public Property cmdModificarPedido As DelegateCommand(Of Object)
        Get
            Return _cmdModificarPedido
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdModificarPedido, value)
        End Set
    End Property
    Private Function CanModificarPedido(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnModificarPedido(arg As Object)
        ' Quitamos el vendedor por grupo ficticio que se creó al cargar el pedido (si sigue existiendo)
        If Not IsNothing(pedido.VendedoresGrupoProducto) Then
            Dim vendedorPorGrupo As VendedorGrupoProductoDTO = pedido.VendedoresGrupoProducto.FirstOrDefault
            If Not IsNothing(vendedorPorGrupo) AndAlso vendedorPorGrupo.vendedor = "" Then
                pedido.VendedoresGrupoProducto.Remove(vendedorPorGrupo)
            End If
        End If

        ' Modificamos el usuario del pedido
        pedido.usuario = System.Environment.UserDomainName + "\" + System.Environment.UserName


        Try
            Await Task.Run(Sub()
                               Try
                                   servicio.modificarPedido(pedido)
                               Catch ex As Exception
                                   Throw New Exception(ex.Message)
                               End Try
                           End Sub)
            NotificationRequest.Raise(New Notification() With {
                           .Title = "Pedido Modificado",
                           .Content = "Pedido " + pedido.numero.ToString + " modificado correctamente"
                       })
            eventAggregator.GetEvent(Of PedidoModificadoEvent).Publish(pedido)
        Catch ex As Exception
            NotificationRequest.Raise(New Notification() With {
                        .Title = "Error en pedido " + pedido.numero.ToString,
                        .Content = ex.Message
                    })

        End Try
    End Sub

    Private _cmdPonerDescuentoPedido As DelegateCommand(Of Object)
    Public Property cmdPonerDescuentoPedido As DelegateCommand(Of Object)
        Get
            Return _cmdPonerDescuentoPedido
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdPonerDescuentoPedido, value)
        End Set
    End Property
    Private Function CanPonerDescuentoPedido(arg As Object) As Boolean
        Return Not IsNothing(pedido) AndAlso Not IsNothing(pedido.LineasPedido)
    End Function
    Private Sub OnPonerDescuentoPedido(arg As Object)
        For Each linea In pedido.LineasPedido.Where(Function(l) l.aplicarDescuento AndAlso l.estado >= -1 AndAlso l.estado <= 1 AndAlso Not l.picking > 0)
            linea.descuento = descuentoPedido
        Next
        OnPropertyChanged("pedido")
    End Sub

    Private _cmdSacarPicking As DelegateCommand(Of Object)
    Public Property cmdSacarPicking As DelegateCommand(Of Object)
        Get
            Return _cmdSacarPicking
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdSacarPicking, value)
        End Set
    End Property
    Private Function CanSacarPicking(arg As Object) As Boolean
        Return True
    End Function
    Private Async Sub OnSacarPicking(arg As Object)
        Dim pedidoPicking As PedidoVentaDTO = arg
        Try
            estaSacandoPicking = True
            Await Task.Run(Sub()
                               Try
                                   If esPickingPedido Then
                                       Dim empresaPicking As String
                                       If Not IsNothing(pedidoPicking) Then
                                           empresaPicking = pedidoPicking.empresa
                                       Else
                                           empresaPicking = EMPRESA_POR_DEFECTO
                                       End If
                                       servicio.sacarPickingPedido(empresaPicking, numeroPedidoPicking)
                                   ElseIf esPickingCliente Then
                                       servicio.sacarPickingPedido(numeroClientePicking)
                                   ElseIf esPickingRutas Then
                                       servicio.sacarPickingPedido()
                                   Else
                                       Throw New Exception("No hay ningún tipo de picking seleccionado")
                                   End If
                               Catch ex As Exception
                                   Throw ex
                               End Try
                           End Sub)

            Dim textoMensaje As String
            If esPickingPedido Then
                textoMensaje = "Se ha asignado el picking correctamente al pedido " + numeroPedidoPicking.ToString
            ElseIf esPickingCliente Then
                textoMensaje = "Se ha asignado el picking correctamente al cliente " + numeroClientePicking
            ElseIf esPickingRutas Then
                textoMensaje = "Se ha asignado el picking correctamente a las rutas"
            Else
                Throw New Exception("Tiene que haber algún tipo de picking seleccionado")
            End If
            NotificationRequest.Raise(New Notification() With {
                        .Title = "Picking",
                        .Content = textoMensaje
                    })
            eventAggregator.GetEvent(Of SacarPickingEvent).Publish(1)
        Catch ex As Exception
            Dim tituloError As String
            If esPickingPedido Then
                tituloError = "Error Picking pedido " + numeroPedidoPicking.ToString
            ElseIf esPickingCliente Then
                tituloError = "Error Picking cliente " + numeroClientePicking
            ElseIf esPickingRutas Then
                tituloError = "Error Picking Rutas"
            Else
                tituloError = "Error Picking sin tipo"
            End If
            Dim textoError As String
            If IsNothing(ex.InnerException) Then
                textoError = ex.Message
            Else
                textoError = ex.Message + vbCr + ex.InnerException.Message
            End If
            NotificationRequest.Raise(New Notification() With {
                        .Title = tituloError,
                        .Content = textoError
                    })
        Finally
            estaSacandoPicking = False
        End Try
    End Sub



#End Region

    Public Overloads Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo
        Dim numero = navigationContext.Parameters("numeroPedidoParameter")
        cmdCargarPedido.Execute(numero)
    End Sub

End Class
