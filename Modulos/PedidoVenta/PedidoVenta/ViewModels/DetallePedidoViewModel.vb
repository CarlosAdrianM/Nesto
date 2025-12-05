Imports System.Collections.ObjectModel
Imports System.ComponentModel.DataAnnotations
Imports System.Globalization
Imports System.Text
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Models
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Mvvm
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports Unity
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
    Private ReadOnly container As IUnityContainer

    Private ivaOriginal As String

    Private Const IVA_POR_DEFECTO = "G21"
    Private Const EMPRESA_POR_DEFECTO = "1"

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IPedidoVentaService, eventAggregator As IEventAggregator, dialogService As IDialogService, container As IUnityContainer)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.eventAggregator = eventAggregator
        Me.dialogService = dialogService
        Me.container = container

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
        AbrirFacturarRutasCommand = New DelegateCommand(AddressOf OnAbrirFacturarRutas, AddressOf CanAbrirFacturarRutas)

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
                ' Carlos 20/11/24: Logs deshabilitados - Ahora el SelectorCCC maneja el CCC
                ' LOG: Detectar cambios de dirección de entrega
                'Dim cccAnterior As String = If(IsNothing(pedido), "NULL", If(String.IsNullOrEmpty(pedido.ccc), "EMPTY", pedido.ccc))
                'Dim cccNuevo As String = If(IsNothing(value), "NULL", If(String.IsNullOrEmpty(value.ccc), "EMPTY", value.ccc))
                'Dim numeroPedido As Integer = If(IsNothing(pedido), -1, pedido.numero)
                'Dim contactoNuevo As String = If(IsNothing(value), "NULL", value.contacto)
                '
                'Debug.WriteLine($"[CCC] DireccionEntregaSeleccionada cambiada:")
                'Debug.WriteLine($"      Pedido #{numeroPedido}, EstaCreandoPedido={EstaCreandoPedido}")
                'Debug.WriteLine($"      Contacto nuevo: {contactoNuevo}")
                'Debug.WriteLine($"      CCC anterior del pedido: {cccAnterior}")
                'Debug.WriteLine($"      CCC de la dirección nueva: {cccNuevo}")

                ' Copiar datos de facturación desde la dirección de entrega seleccionada
                ' IMPORTANTE: El CCC está asociado a la dirección de entrega, NO al cliente
                ' Razón: Cada dirección puede tener su propio CCC para facturación
                ' Carlos 20/11/24: Mantenemos la copia de datos EXCEPTO el CCC (lo maneja SelectorCCC)
                If EstaCreandoPedido AndAlso Not IsNothing(pedido) Then
                    'Debug.WriteLine($"      → COPIANDO datos (pedido nuevo)")
                    pedido.formaPago = value.formaPago
                    pedido.plazosPago = value.plazosPago
                    pedido.iva = value.iva
                    pedido.vendedor = value.vendedor
                    pedido.ruta = value.ruta
                    pedido.periodoFacturacion = value.periodoFacturacion
                    ' Carlos 20/11/24: NO copiar CCC aquí - lo maneja SelectorCCC automáticamente
                    ' pedido.ccc = value.ccc
                    'Debug.WriteLine($"      → CCC copiado: {If(String.IsNullOrEmpty(pedido.ccc), "EMPTY", pedido.ccc)}")
                    'Else
                    'Debug.WriteLine($"      → NO copiando (pedido existente o null)")
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
                    ' Carlos 20/11/24: DESHABILITADO - Ahora el SelectorCCC maneja esto automáticamente
                    ' CargarCCCDisponibles()
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

    ' Carlos 20/11/24: CÓDIGO VIEJO DE CCC - DESHABILITADO
    ' Ahora el control SelectorCCC maneja el CCC automáticamente con su propio servicio
    ' Este código compite con SelectorCCC y causa problemas de sincronización
    '
    '' Propiedades para el selector de CCC
    'Private _estaCargandoCCC As Boolean = False
    'Private _cccDisponibles As ObservableCollection(Of CCCDisponible)
    'Public Property CCCDisponibles As ObservableCollection(Of CCCDisponible)
    '    Get
    '        Return _cccDisponibles
    '    End Get
    '    Set(value As ObservableCollection(Of CCCDisponible))
    '        Dim unused = SetProperty(_cccDisponibles, value)
    '    End Set
    'End Property
    '
    'Private _cccSeleccionado As CCCDisponible
    'Public Property CCCSeleccionado As CCCDisponible
    '    Get
    '        Return _cccSeleccionado
    '    End Get
    '    Set(value As CCCDisponible)
    '        If SetProperty(_cccSeleccionado, value) Then
    '            ' Solo actualizar el CCC del pedido si es un cambio MANUAL del usuario
    '            ' (no cuando estamos cargando automáticamente)
    '            ' Carlos 20/11/24: Agregada validación adicional para evitar falsos positivos
    '            ' - Verificar que NO estamos en proceso de carga automática (_estaCargandoCCC)
    '            ' - Verificar que el valor del CCC realmente cambió (no solo la referencia del objeto)
    '            If Not _estaCargandoCCC AndAlso Not IsNothing(pedido) AndAlso Not IsNothing(value) Then
    '                Dim cccNuevo As String = If(String.IsNullOrEmpty(value.CCC), "", value.CCC?.Trim())
    '                Dim cccAnterior As String = If(String.IsNullOrEmpty(pedido.ccc), "", pedido.ccc?.Trim())
    '
    '                ' Solo logear y actualizar si el CCC realmente cambió de valor
    '                If cccAnterior <> cccNuevo Then
    '                    pedido.ccc = value.CCC
    '                    Debug.WriteLine($"[CCC] ✋ Usuario cambió CCC manualmente:")
    '                    Debug.WriteLine($"      Pedido #{pedido.numero}")
    '                    Debug.WriteLine($"      CCC anterior: {If(String.IsNullOrEmpty(cccAnterior), "EMPTY", cccAnterior)}")
    '                    Debug.WriteLine($"      CCC nuevo: {If(String.IsNullOrEmpty(cccNuevo), "EMPTY", cccNuevo)}")
    '                    Debug.WriteLine($"      Contacto seleccionado: {value.Contacto}")
    '                Else
    '                    ' El valor no cambió, solo actualizar silenciosamente
    '                    pedido.ccc = value.CCC
    '                End If
    '            End If
    '        End If
    '    End Set
    'End Property

    Public ReadOnly Property EstaCreandoPedido As Boolean
        Get
            Return Not IsNothing(pedido) AndAlso pedido.numero = 0
        End Get
    End Property

    ' Carlos 04/12/25: Snapshot del pedido guardado para detectar cambios sin guardar (Issue #254)
    Private _snapshotPedidoGuardado As PedidoVentaDTO

    ''' <summary>
    ''' Indica si el pedido tiene cambios sin guardar.
    ''' True si: es un pedido nuevo (numero=0) O si los datos actuales difieren del último guardado.
    ''' </summary>
    Public ReadOnly Property TieneCambiosSinGuardar As Boolean
        Get
            If IsNothing(pedido) Then Return False
            ' Pedido nuevo sin guardar
            If EstaCreandoPedido Then Return True
            ' Pedido existente: comparar con snapshot
            If IsNothing(_snapshotPedidoGuardado) Then Return False
            Return Not pedido.Model.Equals(_snapshotPedidoGuardado)
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
                RemoveHandler _pedido.PeriodoFacturacionCambiado, AddressOf OnPeriodoFacturacionCambiado
            End If
            Dim unused = SetProperty(_pedido, value)
            If Not IsNothing(_pedido) Then
                AddHandler _pedido.IvaCambiado, AddressOf OnIvaCambiado
                AddHandler _pedido.PeriodoFacturacionCambiado, AddressOf OnPeriodoFacturacionCambiado
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
            RaisePropertyChanged(NameOf(EsSerieCursos))
            InicializarFormaVentaParaLineas()
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

#Region "Selector Forma de Venta (Issue #252)"

    ''' <summary>
    ''' Indica si el pedido es de la serie CV (Cursos) para mostrar el selector de forma de venta.
    ''' </summary>
    Public ReadOnly Property EsSerieCursos As Boolean
        Get
            Return Not IsNothing(pedido) AndAlso
                   Not String.IsNullOrEmpty(pedido.serie) AndAlso
                   pedido.serie.Trim().Equals(Constantes.Series.SERIE_CURSOS, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Private _formaVentaSeleccionadaParaLineas As String
    ''' <summary>
    ''' Forma de venta seleccionada para aplicar a todas las líneas del pedido.
    ''' </summary>
    Public Property FormaVentaSeleccionadaParaLineas As String
        Get
            Return _formaVentaSeleccionadaParaLineas
        End Get
        Set(value As String)
            If SetProperty(_formaVentaSeleccionadaParaLineas, value) AndAlso Not String.IsNullOrEmpty(value) Then
                AplicarFormaVentaALineas(value)
            End If
        End Set
    End Property

    ''' <summary>
    ''' Aplica la forma de venta seleccionada a todas las líneas del pedido.
    ''' </summary>
    Private Sub AplicarFormaVentaALineas(formaVenta As String)
        If IsNothing(pedido) OrElse IsNothing(pedido.Model.Lineas) Then
            Return
        End If

        For Each linea In pedido.Model.Lineas
            linea.formaVenta = formaVenta
        Next

        RaisePropertyChanged(NameOf(pedido))
    End Sub

    ''' <summary>
    ''' Inicializa la forma de venta para líneas basándose en la primera línea del pedido.
    ''' </summary>
    Private Sub InicializarFormaVentaParaLineas()
        If Not EsSerieCursos Then
            Return
        End If

        If IsNothing(pedido) OrElse IsNothing(pedido.Model.Lineas) OrElse Not pedido.Model.Lineas.Any() Then
            Return
        End If

        ' Obtener la forma de venta de la primera línea
        Dim primeraLinea = pedido.Model.Lineas.FirstOrDefault()
        If primeraLinea IsNot Nothing AndAlso Not String.IsNullOrEmpty(primeraLinea.formaVenta) Then
            _formaVentaSeleccionadaParaLineas = primeraLinea.formaVenta.Trim()
            RaisePropertyChanged(NameOf(FormaVentaSeleccionadaParaLineas))
        End If
    End Sub

#End Region

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
                    ' Carlos 04/12/25: Guardar snapshot para detectar cambios sin guardar (Issue #254)
                    _snapshotPedidoGuardado = pedidoDTO.CrearSnapshot()
                    ListaEnlacesSeguimiento = Await servicio.CargarEnlacesSeguimiento(resumen.empresa, resumen.numero)
                    If Not IsNothing(pedido) Then
                        ivaOriginal = IIf(IsNothing(pedido.iva), IVA_POR_DEFECTO, pedido.iva)
                        CobroTarjetaImporte = pedido.Total
                        ' Carlos 20/11/24: DESHABILITADO - Ahora el SelectorCCC maneja esto automáticamente
                        ' CargarCCCDisponibles()
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

    ' Carlos 20/11/24: MÉTODO VIEJO DE CCC - DESHABILITADO
    ' Ahora el control SelectorCCC maneja el CCC automáticamente
    '
    '''' <summary>
    '''' Carga el CCC del contacto actual del pedido
    '''' IMPORTANTE: Solo muestra el CCC del contacto específico para evitar errores de PK
    '''' </summary>
    'Private Async Sub CargarCCCDisponibles()
    '    If IsNothing(pedido) OrElse String.IsNullOrWhiteSpace(pedido.empresa) OrElse String.IsNullOrWhiteSpace(pedido.cliente) Then
    '        Debug.WriteLine("[CCC] No se pueden cargar CCC: pedido, empresa o cliente son null")
    '        Return
    '    End If
    '
    '    Try
    '        Using client As New System.Net.Http.HttpClient()
    '            client.BaseAddress = New Uri(configuracion.servidorAPI)
    '            Dim urlConsulta As String = $"PlantillaVentas/DireccionesEntrega?empresa={pedido.empresa}&clienteDirecciones={pedido.cliente}"
    '
    '            Debug.WriteLine($"[CCC] Cargando CCC disponibles desde API: {urlConsulta}")
    '            Dim response = Await client.GetAsync(urlConsulta)
    '
    '            If response.IsSuccessStatusCode Then
    '                Dim resultado As String = Await response.Content.ReadAsStringAsync()
    '                Dim direcciones = Newtonsoft.Json.JsonConvert.DeserializeObject(Of ObservableCollection(Of ControlesUsuario.Models.DireccionesEntregaCliente))(resultado)
    '
    '                ' FILTRAR solo la dirección del contacto actual del pedido
    '                Dim contactoActual As String = If(String.IsNullOrWhiteSpace(pedido.contacto), "0", pedido.contacto.Trim())
    '                Dim direccionContacto = direcciones.FirstOrDefault(Function(d) d.contacto?.Trim() = contactoActual)
    '
    '                ' Crear lista de CCC disponibles (solo 1 elemento: el del contacto actual)
    '                Dim listaCC As New ObservableCollection(Of CCCDisponible)
    '
    '                If Not IsNothing(direccionContacto) Then
    '                    Dim cccItem As New CCCDisponible(
    '                        If(String.IsNullOrWhiteSpace(direccionContacto.ccc), "", direccionContacto.ccc),
    '                        direccionContacto.contacto,
    '                        If(String.IsNullOrWhiteSpace(direccionContacto.nombre), "Sin nombre", direccionContacto.nombre)
    '                    )
    '                    listaCC.Add(cccItem)
    '                Else
    '                    Debug.WriteLine($"[CCC] ADVERTENCIA: No se encontró dirección para contacto '{contactoActual}'")
    '                End If
    '
    '                CCCDisponibles = listaCC
    '                Debug.WriteLine($"[CCC] Cargado CCC del contacto {contactoActual}")
    '
    '                ' Seleccionar automáticamente el CCC actual del pedido
    '                ' Carlos 20/11/24: Establecer flag para prevenir falsos positivos de "cambio manual"
    '                _estaCargandoCCC = True
    '                Try
    '                    If Not String.IsNullOrWhiteSpace(pedido.ccc) Then
    '                        Dim cccActual = CCCDisponibles.FirstOrDefault(Function(c) c.CCC?.Trim() = pedido.ccc?.Trim())
    '                        If Not IsNothing(cccActual) Then
    '                            CCCSeleccionado = cccActual
    '                            Debug.WriteLine($"[CCC] Auto-seleccionado CCC del pedido: {cccActual.Descripcion}")
    '                        Else
    '                            Debug.WriteLine($"[CCC] ADVERTENCIA: CCC del pedido '{pedido.ccc}' no encontrado en las direcciones disponibles")
    '                        End If
    '                    ElseIf CCCDisponibles.Count > 0 Then
    '                        ' Si no hay CCC en el pedido, seleccionar el primero (contacto 0 generalmente)
    '                        CCCSeleccionado = CCCDisponibles.FirstOrDefault()
    '                        Debug.WriteLine($"[CCC] Auto-seleccionado primer CCC disponible: {CCCSeleccionado.Descripcion}")
    '                    End If
    '                Finally
    '                    _estaCargandoCCC = False
    '                End Try
    '            Else
    '                Debug.WriteLine($"[CCC] ERROR: API retornó {response.StatusCode}")
    '            End If
    '        End Using
    '    Catch ex As Exception
    '        Debug.WriteLine($"[CCC] ERROR al cargar CCC disponibles: {ex.Message}")
    '    End Try
    'End Sub

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
        ' Carlos 04/12/25: Verificar cambios sin guardar antes de crear albarán (Issue #254)
        If Not Await VerificarYGuardarCambiosPendientes() Then
            Return
        End If

        If Not dialogService.ShowConfirmationAnswer("Crear albarán", "¿Desea crear el albarán del pedido?") Then
            Return
        End If
        Try
            Dim albaran As Integer = Await servicio.CrearAlbaranVenta(pedido.empresa.ToString, pedido.numero.ToString)
            dialogService.ShowNotification($"Albarán {albaran} creado correctamente")

            ' Carlos 05/12/24: Recargar pedido para que las líneas muestren estado 2 (albarán)
            ' y se habilite el botón de crear factura
            cmdCargarPedido.Execute(New ResumenPedido With {.empresa = pedido.empresa, .numero = pedido.numero})
        Catch ex As Exception
            dialogService.ShowError($"No se ha podido crear el albarán: {ex.Message}")
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
        ' La factura es un documento administrativo, no requiere estar en el almacén específico
        ' Solo verificamos que el usuario tenga permisos para facturar
        Return Not IsNothing(pedido) AndAlso Not IsNothing(pedido.Lineas) AndAlso
            pedido.Lineas.Any(Function(l) l.estado = Constantes.LineasPedido.ESTADO_ALBARAN) AndAlso
            EsGrupoQuePuedeFacturar
    End Function

    Private Async Sub OnCrearFacturaVenta()
        ' Carlos 04/12/25: Verificar cambios sin guardar antes de crear factura (Issue #254)
        If Not Await VerificarYGuardarCambiosPendientes() Then
            Return
        End If

        If Not dialogService.ShowConfirmationAnswer("Crear factura", "¿Desea crear la factura del pedido?") Then
            Return
        End If
        Try
            Dim resultado As CrearFacturaResponseDTO = Await servicio.CrearFacturaVenta(pedido.empresa.ToString, pedido.numero.ToString)
            'If resultado.NumeroFactura = Constantes.PeriodosFacturacion.FIN_DE_MES Then
            '    Throw New Exception("No se pudo crear factura porque el cliente es de fin de mes")
            'End If

            ' IMPORTANTE: Usar la empresa del resultado, no la original
            ' Si hubo traspaso a empresa espejo, resultado.Empresa contendrá la empresa correcta
            cmdCargarPedido.Execute(New ResumenPedido With {.empresa = resultado.Empresa, .numero = pedido.numero})

            dialogService.ShowNotification($"Factura {resultado.NumeroFactura} creada correctamente")
            Await ImprimirFactura(resultado.NumeroFactura)
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
        ' Reutilizamos la lógica: necesitamos poder crear albarán (líneas pendientes en nuestro almacén)
        ' Y que el pedido sea facturable (no FIN_DE_MES y usuario con permisos)
        Return CanCrearAlbaranVenta() AndAlso
               Not IsNothing(pedido) AndAlso
               pedido.periodoFacturacion <> Constantes.PeriodosFacturacion.FIN_DE_MES AndAlso
               EsGrupoQuePuedeFacturar
    End Function

    Private Async Sub OnCrearAlbaranYFacturaVenta()
        ' Carlos 04/12/25: Verificar cambios sin guardar antes de crear albarán y factura (Issue #254)
        If Not Await VerificarYGuardarCambiosPendientes() Then
            Return
        End If

        If Not dialogService.ShowConfirmationAnswer("Crear albarán y factura", "¿Desea crear la factura del pedido directamente?") Then
            Return
        End If
        Try
            Dim albaran As Integer = Await servicio.CrearAlbaranVenta(pedido.empresa.ToString, pedido.numero.ToString)
            Dim resultado As CrearFacturaResponseDTO = Await servicio.CrearFacturaVenta(pedido.empresa.ToString, pedido.numero.ToString)

            ' IMPORTANTE: Usar la empresa del resultado, no la original
            ' Si hubo traspaso a empresa espejo, resultado.Empresa contendrá la empresa correcta
            cmdCargarPedido.Execute(New ResumenPedido With {.empresa = resultado.Empresa, .numero = pedido.numero})

            dialogService.ShowNotification($"Albarán {albaran} y factura {resultado.NumeroFactura} creados correctamente")
            Await ImprimirFactura(resultado.NumeroFactura)
        Catch ex As Exception
            dialogService.ShowError($"No se ha podido crear el albarán o la factura: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Carlos 04/12/25: Verifica si hay cambios sin guardar y pregunta al usuario si desea guardarlos (Issue #254).
    ''' Retorna True si se puede continuar (no hay cambios, o se guardaron correctamente, o el usuario canceló).
    ''' Retorna False si hay cambios y el usuario no quiso guardar o hubo error al guardar.
    ''' </summary>
    Private Async Function VerificarYGuardarCambiosPendientes() As Task(Of Boolean)
        If Not TieneCambiosSinGuardar Then
            Return True ' No hay cambios, continuar
        End If

        ' Hay cambios sin guardar
        Dim mensaje As String
        If EstaCreandoPedido Then
            mensaje = "El pedido aún no ha sido guardado. Debe guardar el pedido antes de crear albarán o factura." & vbCrLf & vbCrLf &
                     "¿Desea guardar el pedido ahora?"
        Else
            mensaje = "El pedido tiene cambios sin guardar. Si continúa sin guardar, el albarán/factura se creará con los datos anteriores." & vbCrLf & vbCrLf &
                     "¿Desea guardar los cambios antes de continuar?"
        End If

        Dim confirmar As Boolean = Await dialogService.ShowConfirmationAsync("Cambios sin guardar", mensaje)

        If Not confirmar Then
            Return False ' Usuario no quiso guardar, cancelar operación
        End If

        ' Intentar guardar el pedido
        Try
            textoBusyIndicator = "Guardando pedido..."
            estaBloqueado = True

            If EstaCreandoPedido Then
                Dim numeroPedidoCreado As Integer = Await servicio.CrearPedido(pedido.Model)
                pedido.numero = numeroPedidoCreado
                RaisePropertyChanged(NameOf(EstaCreandoPedido))
                RaisePropertyChanged(NameOf(TextoBotonGuardar))
                Titulo = $"Pedido Venta ({pedido.numero})"
            Else
                Await servicio.modificarPedido(pedido.Model)
            End If

            ' Actualizar snapshot
            _snapshotPedidoGuardado = pedido.Model.CrearSnapshot()
            Return True ' Guardado exitoso, continuar

        Catch ex As Exception
            dialogService.ShowError($"Error al guardar el pedido: {ex.Message}" & vbCrLf & vbCrLf &
                                   "No se puede crear el albarán/factura sin guardar primero.")
            Return False ' Error al guardar, cancelar operación
        Finally
            estaBloqueado = False
        End Try
    End Function

    Private _abrirFacturarRutasCommand As DelegateCommand
    Public Property AbrirFacturarRutasCommand As DelegateCommand
        Get
            Return _abrirFacturarRutasCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_abrirFacturarRutasCommand, value)
        End Set
    End Property
    Private Sub OnAbrirFacturarRutas()
        ' Abrir el diálogo de Facturar Rutas usando Prism DialogService (modal)
        System.Diagnostics.Debug.WriteLine("=== OnAbrirFacturarRutas - Llamando dialogService.ShowDialog ===")
        dialogService.ShowDialog("FacturarRutasPopup", Nothing, Sub(result)
                                                                    System.Diagnostics.Debug.WriteLine($"=== Diálogo cerrado. Result: {result.Result} ===")
                                                                End Sub)
    End Sub
    Private Function CanAbrirFacturarRutas() As Boolean
        ' Carlos 12/01/25: Solo el grupo Dirección puede acceder a facturar rutas
        Return configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) OrElse configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
    End Function

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
                    ' Carlos 12/01/25: Verificar si puede crear sin pasar validación
                    ' - Dirección o Almacén pueden crear sin importar almacenes
                    ' - Tiendas puede crear solo si TODAS las líneas están en su almacén
                    Dim puedeCrearSinPasarValidacion As Boolean =
                    configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
                    configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) OrElse
                    (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS) AndAlso
                     pedido.Lineas.All(Function(l) l.Almacen = AlmacenUsuario))

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
                ' Carlos 04/12/25: Actualizar snapshot después de crear (Issue #254)
                _snapshotPedidoGuardado = pedido.Model.CrearSnapshot()
            Else
                Try
                    Await servicio.modificarPedido(pedido.Model)
                Catch ex As ValidationException
                    crearModificarEx = ex
                    ' Carlos 12/01/25: Verificar si puede modificar sin pasar validación
                    ' - Dirección o Almacén pueden modificar sin importar almacenes
                    ' - Tiendas puede modificar solo si TODAS las líneas están en su almacén
                    Dim puedeModificarSinPasarValidacion As Boolean =
                    configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
                    configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) OrElse
                    (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS) AndAlso
                     pedido.Lineas.All(Function(l) l.Almacen = AlmacenUsuario))

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
                ' Carlos 04/12/25: Actualizar snapshot después de guardar (Issue #254)
                _snapshotPedidoGuardado = pedido.Model.CrearSnapshot()
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

                ' Carlos 02/12/25: Actualizar PorcentajeIva en líneas existentes cuando cambia el IVA de cabecera (Issue #246)
                If pedido.Model.ParametrosIva IsNot Nothing AndAlso pedido.Model.ParametrosIva.Any() Then
                    For Each linea In pedido.Lineas.Where(Function(l) Not String.IsNullOrEmpty(l.iva))
                        Dim parametro = pedido.Model.ParametrosIva.SingleOrDefault(Function(p) p.CodigoIvaProducto.Equals(linea.iva.Trim(), StringComparison.OrdinalIgnoreCase))
                        If parametro IsNot Nothing Then
                            linea.Model.PorcentajeIva = parametro.PorcentajeIvaProducto
                            linea.Model.PorcentajeRecargoEquivalencia = parametro.PorcentajeIvaRecargoEquivalencia
                        End If
                    Next
                End If
            End If
        Catch ex As Exception
            dialogService.ShowError("Error al cargar los parámetros de IVA: " + ex.Message)
        End Try
    End Sub

    ' Carlos 26/11/24: Actualizar comandos de facturación cuando cambia el periodo
    Private Sub OnPeriodoFacturacionCambiado(nuevoPeriodo As String)
        CrearFacturaVentaCommand.RaiseCanExecuteChanged()
        CrearAlbaranYFacturaVentaCommand.RaiseCanExecuteChanged()
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

''' <summary>
''' Representa una cuenta corriente disponible para el cliente/contacto
''' </summary>
' Carlos 20/11/24: CLASE VIEJA DE CCC - DESHABILITADA
' Ahora el control SelectorCCC usa su propio modelo (CCCItem en ControlesUsuario.Models)
'
'Public Class CCCDisponible
'    Public Property CCC As String
'    Public Property Descripcion As String
'    Public Property Contacto As String
'    Public Property NombreContacto As String
'
'    Public Sub New(ccc As String, contacto As String, nombreContacto As String)
'        Me.CCC = If(String.IsNullOrWhiteSpace(ccc), "", ccc.Trim())
'        Me.Contacto = contacto
'        Me.NombreContacto = nombreContacto
'
'        ' Generar descripción amigable
'        If String.IsNullOrWhiteSpace(Me.CCC) Then
'            Descripcion = $"Contacto {contacto}: Sin CCC"
'        Else
'            Dim cccCorto = If(Me.CCC.Length > 20, Me.CCC.Substring(0, 20) & "...", Me.CCC)
'            Descripcion = $"Contacto {contacto} ({nombreContacto}): ...{Me.CCC.Substring(Math.Max(0, Me.CCC.Length - 8))}"
'        End If
'    End Sub
'End Class
