Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Regions
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.Contracts
Imports System.Text
Imports ControlesUsuario.Models
Imports VendedorGrupoProductoDTO = Nesto.Models.VendedorGrupoProductoDTO
Imports Nesto.Models
Imports System.Collections.ObjectModel

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

        EsGrupoQuePuedeFacturar = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)

        eventAggregator.GetEvent(Of ProductoSeleccionadoEvent).Subscribe(AddressOf InsertarProducto)
        eventAggregator.GetEvent(Of SacarPickingEvent).Subscribe(AddressOf ActualizarLookup)
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
    Private _celdaActual As DataGridCellInfo
    Public Property celdaActual As DataGridCellInfo
        Get
            Return _celdaActual
        End Get
        Set(value As DataGridCellInfo)
            SetProperty(_celdaActual, value)
        End Set
    End Property

    Private _cobroTarjetaCorreo As String
    Public Property CobroTarjetaCorreo As String
        Get
            Return _cobroTarjetaCorreo
        End Get
        Set(value As String)
            SetProperty(_cobroTarjetaCorreo, value)
            EnviarCobroTarjetaCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _cobroTarjetaImporte As Decimal
    Public Property CobroTarjetaImporte As Decimal
        Get
            Return _cobroTarjetaImporte
        End Get
        Set(value As Decimal)
            SetProperty(_cobroTarjetaImporte, value)
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
            SetProperty(_cobroTarjetaMovil, value)
            EnviarCobroTarjetaCommand.RaiseCanExecuteChanged()
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
                SetProperty(_descuentoPedido, value)
                cmdPonerDescuentoPedido.Execute()
            End If
        End Set
    End Property

    Public Property DireccionEntregaSeleccionada As DireccionesEntregaCliente

    Private _esGrupoAlmacen As Boolean

    Public Property EsGrupoQuePuedeFacturar As Boolean
        Get
            Return _esGrupoAlmacen
        End Get
        Set(value As Boolean)
            SetProperty(_esGrupoAlmacen, value)
        End Set
    End Property

    Private _estaBloqueado As Boolean
    Public Property estaBloqueado() As Boolean
        Get
            Return _estaBloqueado
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaBloqueado, value)
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
                cmdCambiarFechaEntrega.Execute()
                RaisePropertyChanged(NameOf(pedido))
            End If
        End Set
    End Property

    Private _lineaActual As LineaPedidoVentaWrapper
    Public Property lineaActual As LineaPedidoVentaWrapper
        Get
            Return _lineaActual
        End Get
        Set(value As LineaPedidoVentaWrapper)
            SetProperty(_lineaActual, value)
            RaisePropertyChanged(NameOf(pedido))
        End Set
    End Property

    Private _listaEnlacesSeguimiento As List(Of EnvioAgenciaDTO)
    Public Property ListaEnlacesSeguimiento As List(Of EnvioAgenciaDTO)
        Get
            Return _listaEnlacesSeguimiento
        End Get
        Set(value As List(Of EnvioAgenciaDTO))
            SetProperty(_listaEnlacesSeguimiento, value)
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
            SetProperty(_pedido, value)
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
            AceptarPresupuestoCommand.RaiseCanExecuteChanged()
            DescargarPresupuestoCommand.RaiseCanExecuteChanged()
            CrearAlbaranVentaCommand.RaiseCanExecuteChanged()
            CrearFacturaVentaCommand.RaiseCanExecuteChanged()
            CrearAlbaranYFacturaVentaCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _textoBusyIndicator As String
    Public Property textoBusyIndicator As String
        Get
            Return _textoBusyIndicator
        End Get
        Set(value As String)
            SetProperty(_textoBusyIndicator, value)
        End Set
    End Property
    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            SetProperty(_titulo, value)
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
            If IsNothing(pedido.VendedoresGrupoProducto) Then
                pedido.VendedoresGrupoProducto = New ObservableCollection(Of VendedorGrupoProductoDTO)
            End If
            If IsNothing(pedido.VendedoresGrupoProducto.Count = 0) Then
                pedido.VendedoresGrupoProducto.Add(vendedorPorGrupo)
            End If
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
            SetProperty(_cmdAbrirPicking, value)
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
            SetProperty(_aceptarPresupuestoCommand, value)
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
            SetProperty(_cmdActualizarTotales, value)
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
            SetProperty(_cmdCambiarFechaEntrega, value)
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
            SetProperty(_cmdCambiarIva, value)
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
            SetProperty(_cmdCargarPedido, value)
        End Set
    End Property
    Private Async Sub OnCargarPedido(resumen As ResumenPedido)
        Try
            If Not IsNothing(resumen) AndAlso Not IsNothing(resumen.numero) Then
                Me.Titulo = "Pedido Venta (" + resumen.numero.ToString + ")"
                Dim pedidoDTO As PedidoVentaDTO = Await servicio.cargarPedido(resumen.empresa, resumen.numero)
                pedido = New PedidoVentaWrapper(pedidoDTO)
                ListaEnlacesSeguimiento = Await servicio.CargarEnlacesSeguimiento(resumen.empresa, resumen.numero)
                If Not IsNothing(pedido) Then
                    ivaOriginal = IIf(IsNothing(pedido.iva), IVA_POR_DEFECTO, pedido.iva)
                    CobroTarjetaImporte = pedido.Total
                End If
            Else
                Me.Titulo = "Lista de Pedidos"
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
            SetProperty(_cargarProductoCommand, value)
        End Set
    End Property
    Private Sub OnCargarProducto(linea As LineaPedidoVentaDTO)
        Dim parameters As NavigationParameters = New NavigationParameters()
        parameters.Add("numeroProductoParameter", linea.Producto)
        regionManager.RequestNavigate("MainRegion", "ProductoView", parameters)
    End Sub

    Private _cmdCeldaModificada As DelegateCommand(Of DataGridCellEditEndingEventArgs)
    Public Property cmdCeldaModificada As DelegateCommand(Of DataGridCellEditEndingEventArgs)
        Get
            Return _cmdCeldaModificada
        End Get
        Private Set(value As DelegateCommand(Of DataGridCellEditEndingEventArgs))
            SetProperty(_cmdCeldaModificada, value)
        End Set
    End Property
    Private Async Sub OnCeldaModificada(eventArgs As DataGridCellEditEndingEventArgs)
        Dim lineaEditada As LineaPedidoVentaWrapper = CType(eventArgs.Row.DataContext, LineaPedidoVentaWrapper)
        If Not lineaEditada.Equals(lineaActual) Then
            lineaActual = lineaEditada
        End If
        If IsNothing(lineaActual.almacen) Then
            lineaActual.almacen = pedido.Lineas.FirstOrDefault()?.almacen
        End If
        If IsNothing(lineaActual.fechaEntrega) OrElse lineaActual.fechaEntrega = DateTime.MinValue Then
            lineaActual.fechaEntrega = fechaEntrega
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
            Await CargarDatosProducto(lineaActual.Producto, CType(textoNuevo, Short))
        End If
        If esTextBox AndAlso eventArgs.Column.Header = "Precio" OrElse eventArgs.Column.Header = "Descuento" Then
            Dim textBox As TextBox = eventArgs.EditingElement
            ' Windows debería hacer que el teclado numérico escribiese coma en vez de punto
            ' pero como no lo hace, lo cambiamos nosotros
            textBox.Text = Replace(textBox.Text, ".", ",")
            Dim style As NumberStyles = NumberStyles.Number Or NumberStyles.AllowCurrencySymbol
            Dim culture As CultureInfo = CultureInfo.CurrentCulture

            If eventArgs.Column.Header = "Precio" Then
                If Not Double.TryParse(textBox.Text, style, CType(culture, IFormatProvider), (lineaActual.PrecioUnitario)) Then
                    Return
                End If
            Else
                If Not Double.TryParse(textBox.Text, style, CType(culture, IFormatProvider), (lineaActual.DescuentoLinea)) Then
                    Return
                Else
                    lineaActual.DescuentoLinea = lineaActual.DescuentoLinea / 100
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
            lineaCambio.PrecioUnitario = producto.precio
            lineaCambio.texto = producto.nombre
            lineaCambio.AplicarDescuento = producto.aplicarDescuento
            lineaCambio.DescuentoProducto = producto.descuento
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
            SetProperty(_copiarAlPortapapelesCommand, value)
        End Set
    End Property
    Private Sub OnCopiarAlPortapapeles()
        Dim html As New StringBuilder()
        html.Append(Constantes.Formatos.HTML_CLIENTE_P_TAG)
        html.Append(ToString.Replace(vbCr, "<br/>"))
        html.Append("</p>")
        ClipboardHelper.CopyToClipboard(html.ToString, ToString)
        dialogService.ShowNotification("Datos del pedido copiados al portapapeles")
    End Sub

    Private _crearAlbaranVentaCommand As DelegateCommand
    Public Property CrearAlbaranVentaCommand As DelegateCommand
        Get
            Return _crearAlbaranVentaCommand
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_crearAlbaranVentaCommand, value)
        End Set
    End Property
    Private Function CanCrearAlbaranVenta() As Boolean
        Return Not IsNothing(pedido) AndAlso Not IsNothing(pedido.Lineas) AndAlso pedido.Lineas.Any(Function(l) l.estado < Constantes.LineasPedido.ESTADO_ALBARAN AndAlso l.estado >= Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE)
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
            SetProperty(_crearFacturaVentaCommand, value)
        End Set
    End Property
    Private Function CanCrearFacturaVenta() As Boolean
        Return Not IsNothing(pedido) AndAlso Not IsNothing(pedido.Lineas) AndAlso pedido.Lineas.Any(Function(l) l.estado = Constantes.LineasPedido.ESTADO_ALBARAN)
    End Function

    Private Async Sub OnCrearFacturaVenta()
        If Not dialogService.ShowConfirmationAnswer("Crear factura", "¿Desea crear la factura del pedido?") Then
            Return
        End If
        Try
            Dim factura As String = Await servicio.CrearFacturaVenta(pedido.empresa.ToString, pedido.numero.ToString)
            cmdCargarPedido.Execute(New ResumenPedido With {.empresa = pedido.empresa, .numero = pedido.numero})
            dialogService.ShowNotification($"Factura {factura} creada correctamente")
            Await ImprimirFactura(factura)
        Catch ex As Exception
            dialogService.ShowError("No se ha podido crear la factura")
        Finally
            CrearFacturaVentaCommand.RaiseCanExecuteChanged()
            CrearAlbaranYFacturaVentaCommand.RaiseCanExecuteChanged()
        End Try
    End Sub

    Private Async Function ImprimirFactura(factura As String) As Task
        If Not dialogService.ShowConfirmationAnswer("Abrir factura", "¿Desea abrir el documento de la factura?") Then
            Return
        End If
        Dim pathFactura = Await servicio.DescargarFactura(pedido.empresa, factura, pedido.cliente)
        Process.Start(New ProcessStartInfo(pathFactura) With {
            .UseShellExecute = True
        })
    End Function

    Private _crearAlbaranYFacturaVentaCommand As DelegateCommand
    Public Property CrearAlbaranYFacturaVentaCommand As DelegateCommand
        Get
            Return _crearAlbaranYFacturaVentaCommand
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_crearAlbaranYFacturaVentaCommand, value)
        End Set
    End Property
    Private Function CanCrearAlbaranYFacturaVenta() As Boolean
        Return Not IsNothing(pedido) AndAlso Not IsNothing(pedido.Lineas) AndAlso pedido.Lineas.Any(Function(l) l.estado < Constantes.LineasPedido.ESTADO_FACTURA AndAlso l.estado >= Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE)
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
            SetProperty(_descargarPresupuestoCommand, value)
        End Set
    End Property
    Private Function CanDescargarPresupuesto() As Boolean
        Return (Not IsNothing(pedido)) 'AndAlso pedido.EsPresupuesto
    End Function
    Private Async Sub OnDescargarPresupuesto()
        textoBusyIndicator = "Generando proforma..."
        estaBloqueado = True

        Try
            Dim path As String = Await servicio.DescargarFactura(pedido.empresa, pedido.numero.ToString, pedido.cliente)
            Dim pathDirectorio As String = path.Substring(0, path.LastIndexOf("\"))

            ' Abrimos la carpeta de descargas
            Process.Start(New ProcessStartInfo(pathDirectorio) With {
                .UseShellExecute = True
            })
        Catch ex As Exception
            Dim mensajeError As String
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
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
            SetProperty(_enviarCobroTarjetaCommand, value)
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
            SetProperty(_cmdModificarPedido, value)
        End Set
    End Property
    Private Async Sub OnModificarPedido()
        textoBusyIndicator = "Modificando pedido..."
        estaBloqueado = True

        ' Quitamos el vendedor por grupo ficticio que se creó al cargar el pedido (si sigue existiendo)
        If Not IsNothing(pedido.VendedoresGrupoProducto) Then
            Dim vendedorPorGrupo As VendedorGrupoProductoDTO = pedido.VendedoresGrupoProducto.FirstOrDefault
            If Not IsNothing(vendedorPorGrupo) AndAlso vendedorPorGrupo.vendedor = String.Empty Then
                pedido.VendedoresGrupoProducto.Remove(vendedorPorGrupo)
            End If
        End If

        ' Modificamos el usuario del pedido
        pedido.Model.Usuario = configuracion.usuario


        Try
            Await Task.Run(Sub()
                               Try
                                   servicio.modificarPedido(pedido.Model)
                               Catch ex As Exception
                                   Throw New Exception(ex.Message)
                               End Try
                           End Sub)
            dialogService.ShowNotification("Pedido Modificado", "Pedido " + pedido.numero.ToString + " modificado correctamente")
            eventAggregator.GetEvent(Of PedidoModificadoEvent).Publish(pedido.Model)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaBloqueado = False
        End Try
    End Sub

    Private _cmdPonerDescuentoPedido As DelegateCommand
    Public Property cmdPonerDescuentoPedido As DelegateCommand
        Get
            Return _cmdPonerDescuentoPedido
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdPonerDescuentoPedido, value)
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
        Process.Start(New ProcessStartInfo(enlace) With {
            .UseShellExecute = True
        })
    End Sub

#End Region



    Public Overloads Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo
        Dim resumen = navigationContext.Parameters("resumenPedidoParameter")
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
