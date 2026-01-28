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
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.Services
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Mvvm
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports System.Windows
Imports System.Windows.Media
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
    ''' <summary>
    ''' Valor especial para indicar que hay diferentes valores en las líneas.
    ''' Carlos 09/12/25: Usado por selectores de FormaVenta y Almacén.
    ''' </summary>
    Public Const VALOR_VARIOS = "VARIOS"

#Region "Issue #258: Tipos de línea para ComboBox"
    ''' <summary>
    ''' Clase para los items del ComboBox de TipoLinea.
    ''' </summary>
    Public Class TipoLineaItem
        Public Property Valor As Byte
        Public Property Descripcion As String

        Public Overrides Function ToString() As String
            Return Descripcion
        End Function
    End Class

    ''' <summary>
    ''' Lista de tipos de línea válidos para el ComboBox.
    ''' Issue #258: Evita que el usuario ponga valores inválidos como 33.
    ''' </summary>
    Public Shared ReadOnly TiposLinea As New List(Of TipoLineaItem) From {
        New TipoLineaItem With {.Valor = 0, .Descripcion = "0 - Texto"},
        New TipoLineaItem With {.Valor = 1, .Descripcion = "1 - Producto"},
        New TipoLineaItem With {.Valor = 2, .Descripcion = "2 - Cuenta"},
        New TipoLineaItem With {.Valor = 3, .Descripcion = "3 - Inmovilizado"}
    }
#End Region

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IPedidoVentaService, eventAggregator As IEventAggregator, dialogService As IDialogService, container As IUnityContainer)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.eventAggregator = eventAggregator
        Me.dialogService = dialogService
        Me.container = container

        cmdAbrirPicking = New DelegateCommand(AddressOf OnAbrirPicking)
        AceptarPresupuestoCommand = New DelegateCommand(AddressOf OnAceptarPresupuesto, AddressOf CanAceptarPresupuesto)
        PasarAPresupuestoCommand = New DelegateCommand(AddressOf OnPasarAPresupuesto, AddressOf CanPasarAPresupuesto)
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
        ImprimirFacturaDirectoCommand = New DelegateCommand(AddressOf OnImprimirFacturaDirecto, AddressOf CanImprimirFactura)
        ImprimirAlbaranCommand = New DelegateCommand(AddressOf OnImprimirAlbaran, AddressOf CanImprimirAlbaran)
        ImprimirAlbaranDirectoCommand = New DelegateCommand(AddressOf OnImprimirAlbaranDirecto, AddressOf CanImprimirAlbaran)
        CopiarEnlaceCommand = New DelegateCommand(Of String)(AddressOf OnCopiarEnlace)
        AbrirFacturarRutasCommand = New DelegateCommand(AddressOf OnAbrirFacturarRutas, AddressOf CanAbrirFacturarRutas)
        CopiarFacturaCommand = New DelegateCommand(AddressOf OnCopiarFactura, AddressOf CanCopiarFactura)

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

#Region "Issue #51: Fechas de entrega individuales por línea"
    ''' <summary>
    ''' Indica si se usan fechas de entrega individuales por línea en lugar de una fecha global.
    ''' Se activa automáticamente para la serie CV (Cursos).
    ''' </summary>
    Private _usarFechasIndividuales As Boolean = False
    Public Property UsarFechasIndividuales As Boolean
        Get
            Return _usarFechasIndividuales
        End Get
        Set(value As Boolean)
            If SetProperty(_usarFechasIndividuales, value) Then
                RaisePropertyChanged(NameOf(MostrarFechaEntregaGlobal))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Indica si se debe mostrar el DatePicker de fecha de entrega global.
    ''' Se oculta cuando UsarFechasIndividuales está activo.
    ''' </summary>
    Public ReadOnly Property MostrarFechaEntregaGlobal As Boolean
        Get
            Return Not UsarFechasIndividuales
        End Get
    End Property

    ''' <summary>
    ''' Comprueba si las líneas del pedido tienen fechas de entrega diferentes.
    ''' Se usa para activar automáticamente el modo de fechas individuales al cargar un pedido.
    ''' </summary>
    Private Function TieneFechasEntregaDiferentes() As Boolean
        If IsNothing(pedido) OrElse IsNothing(pedido.Lineas) OrElse pedido.Lineas.Count < 2 Then
            Return False
        End If

        Dim lineasConFecha = pedido.Lineas.Where(Function(l) l.fechaEntrega <> Date.MinValue).ToList()
        If lineasConFecha.Count < 2 Then
            Return False
        End If

        Dim primeraFecha = lineasConFecha.First().fechaEntrega.Date
        Return lineasConFecha.Any(Function(l) l.fechaEntrega.Date <> primeraFecha)
    End Function
#End Region

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
            If SetProperty(_papelConMembrete, value) Then
                ' Guardar preferencia del usuario (sync porque estamos en un setter)
                configuracion.GuardarParametroSync(
                    Constantes.Empresas.EMPRESA_DEFECTO,
                    Parametros.Claves.PedidoVentaPapelMembrete,
                    value.ToString())
            End If
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
            ImprimirFacturaDirectoCommand.RaiseCanExecuteChanged()
            ImprimirAlbaranCommand.RaiseCanExecuteChanged()
            ImprimirAlbaranDirectoCommand.RaiseCanExecuteChanged()
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
                RemoveHandler _pedido.PropertyChanged, AddressOf OnPedidoPropertyChanged ' Carlos 09/12/25: Issue #245
            End If
            Dim unused = SetProperty(_pedido, value)
            If Not IsNothing(_pedido) Then
                AddHandler _pedido.IvaCambiado, AddressOf OnIvaCambiado
                AddHandler _pedido.PeriodoFacturacionCambiado, AddressOf OnPeriodoFacturacionCambiado
                AddHandler _pedido.PropertyChanged, AddressOf OnPedidoPropertyChanged ' Carlos 09/12/25: Issue #245
            End If
            eventAggregator.GetEvent(Of PedidoModificadoEvent).Publish(pedido.Model)
            estaActualizarFechaActivo = False
            Dim linea As LineaPedidoVentaDTO = pedido.Model.Lineas.FirstOrDefault(Function(l) l.estado >= -1 And l.estado <= 1)
            If Not IsNothing(linea) AndAlso Not IsNothing(linea.fechaEntrega) Then
                fechaEntrega = linea.fechaEntrega
            End If
            ' Issue #51: Detectar si hay fechas de entrega diferentes en las líneas
            UsarFechasIndividuales = TieneFechasEntregaDiferentes() OrElse EsSerieCursos
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
            RaisePropertyChanged(NameOf(HayLineasEditables))
            RaisePropertyChanged(NameOf(PuedeEditarSelectoresLinea))
            InicializarFormaVentaParaLineas()
            InicializarAlmacenParaLineas() ' Carlos 09/12/25: Issue #253/#52
            AceptarPresupuestoCommand.RaiseCanExecuteChanged()
            PasarAPresupuestoCommand.RaiseCanExecuteChanged()
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
            ' Carlos 15/12/25: Guardar si es primera asignación (carga inicial) para no recalcular vencimiento
            Dim esPrimeraAsignacion = IsNothing(_plazoPagoCompleto)
            If SetProperty(_plazoPagoCompleto, value) Then
                If IsNothing(PlazoPagoCompleto) Then
                    Return
                End If
                ' Carlos 15/12/25: Solo recalcular vencimiento si:
                ' - Es pedido nuevo (EstaCreandoPedido), O
                ' - El usuario cambió el plazo manualmente (no es primera carga)
                ' Esto evita sobrescribir el vencimiento guardado al cargar pedidos existentes (Issue #254)
                If EstaCreandoPedido OrElse Not esPrimeraAsignacion Then
                    pedido.primerVencimiento = pedido.fecha.Value
                    If PlazoPagoCompleto.diasPrimerPlazo <> 0 Then
                        pedido.primerVencimiento = pedido.primerVencimiento.Value.AddDays(PlazoPagoCompleto.diasPrimerPlazo)
                    End If
                    If PlazoPagoCompleto.mesesPrimerPlazo <> 0 Then
                        pedido.primerVencimiento = pedido.primerVencimiento.Value.AddMonths(PlazoPagoCompleto.mesesPrimerPlazo)
                    End If
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

    ''' <summary>
    ''' Indica si hay líneas editables (no albaraneadas ni facturadas) en el pedido.
    ''' Carlos 09/12/25: Usado para deshabilitar selectores cuando todo está facturado.
    ''' </summary>
    Public ReadOnly Property HayLineasEditables As Boolean
        Get
            Return Not IsNothing(pedido) AndAlso
                   Not IsNothing(pedido.Model.Lineas) AndAlso
                   pedido.Model.Lineas.Any(Function(l) Not l.estaAlbaraneada)
        End Get
    End Property

    ''' <summary>
    ''' Indica si los selectores de FormaVenta y Almacén deben estar habilitados.
    ''' Carlos 09/12/25: Combina EsSerieCursos Y HayLineasEditables.
    ''' Los selectores se habilitan solo si:
    ''' - Es serie CV (Cursos)
    ''' - Hay al menos una línea que no esté albaraneada/facturada
    ''' </summary>
    Public ReadOnly Property PuedeEditarSelectoresLinea As Boolean
        Get
            Return EsSerieCursos AndAlso HayLineasEditables
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
            If SetProperty(_formaVentaSeleccionadaParaLineas, value) AndAlso Not String.IsNullOrEmpty(value) AndAlso value <> VALOR_VARIOS Then
                AplicarFormaVentaALineas(value)
            End If
        End Set
    End Property

    ''' <summary>
    ''' Aplica la forma de venta seleccionada a todas las líneas editables del pedido.
    ''' Carlos 09/12/25: Bug fix - No modifica líneas albaraneadas o facturadas (estado >= 2).
    ''' </summary>
    Private Sub AplicarFormaVentaALineas(formaVenta As String)
        If IsNothing(pedido) OrElse IsNothing(pedido.Model.Lineas) Then
            Return
        End If

        For Each linea In pedido.Model.Lineas
            ' Solo modificar líneas que no estén albaraneadas ni facturadas
            If Not linea.estaAlbaraneada Then
                linea.formaVenta = formaVenta
            End If
        Next

        RaisePropertyChanged(NameOf(pedido))
    End Sub

    ''' <summary>
    ''' Inicializa la forma de venta para líneas basándose en las líneas del pedido.
    ''' Si todas las líneas tienen la misma forma de venta, la selecciona.
    ''' Si hay diferentes, muestra "VARIOS".
    ''' Carlos 09/12/25: Mejorado para detectar valores diferentes y usar valor por defecto.
    ''' Carlos 09/12/25: Siempre inicializa para mostrar valores (selector visible siempre, editable solo en CV).
    ''' </summary>
    Private Sub InicializarFormaVentaParaLineas()
        Debug.WriteLine($"[DetallePedidoVM] InicializarFormaVentaParaLineas - EsSerieCursos={EsSerieCursos}, Serie={pedido?.serie}")

        If IsNothing(pedido) OrElse IsNothing(pedido.Model.Lineas) OrElse Not pedido.Model.Lineas.Any() Then
            Debug.WriteLine($"[DetallePedidoVM] InicializarFormaVentaParaLineas - Sin líneas, usando defecto: '{FormaVentaUsuario}'")
            _formaVentaSeleccionadaParaLineas = FormaVentaUsuario
            RaisePropertyChanged(NameOf(FormaVentaSeleccionadaParaLineas))
            Return
        End If

        ' Obtener todas las formas de venta distintas (no nulas ni vacías)
        Dim formasVentaDistintas = pedido.Model.Lineas _
            .Where(Function(l) Not String.IsNullOrWhiteSpace(l.formaVenta)) _
            .Select(Function(l) l.formaVenta.Trim()) _
            .Distinct() _
            .ToList()

        Debug.WriteLine($"[DetallePedidoVM] InicializarFormaVentaParaLineas - {formasVentaDistintas.Count} formas distintas: {String.Join(", ", formasVentaDistintas)}")

        If formasVentaDistintas.Count = 0 Then
            ' No hay formas de venta definidas en las líneas - usar valor por defecto
            _formaVentaSeleccionadaParaLineas = FormaVentaUsuario
        ElseIf formasVentaDistintas.Count = 1 Then
            ' Todas las líneas tienen la misma forma de venta
            _formaVentaSeleccionadaParaLineas = formasVentaDistintas.First()
        Else
            ' Hay diferentes formas de venta - mostrar valor especial
            _formaVentaSeleccionadaParaLineas = VALOR_VARIOS
        End If

        Debug.WriteLine($"[DetallePedidoVM] InicializarFormaVentaParaLineas - Resultado: '{_formaVentaSeleccionadaParaLineas}'")
        RaisePropertyChanged(NameOf(FormaVentaSeleccionadaParaLineas))
    End Sub

#End Region

#Region "Selector Almacén (Issue #253/#52)"

    Private _almacenSeleccionadoParaLineas As String
    ''' <summary>
    ''' Almacén seleccionado para aplicar a las líneas con productos ficticios.
    ''' Carlos 09/12/25: Issue #253/#52
    ''' </summary>
    Public Property AlmacenSeleccionadoParaLineas As String
        Get
            Return _almacenSeleccionadoParaLineas
        End Get
        Set(value As String)
            If SetProperty(_almacenSeleccionadoParaLineas, value) AndAlso Not String.IsNullOrEmpty(value) AndAlso value <> VALOR_VARIOS Then
                AplicarAlmacenALineas(value)
            End If
        End Set
    End Property

    ''' <summary>
    ''' Aplica el almacén seleccionado a todas las líneas editables del pedido.
    ''' Carlos 09/12/25: Issue #253/#52 - Cambiado para aplicar a TODAS las líneas, no solo ficticias.
    ''' Carlos 09/12/25: Bug fix - No modifica líneas albaraneadas o facturadas (estado >= 2).
    ''' </summary>
    Private Sub AplicarAlmacenALineas(almacen As String)
        If IsNothing(pedido) OrElse IsNothing(pedido.Model.Lineas) Then
            Return
        End If

        For Each linea In pedido.Model.Lineas
            ' Solo modificar líneas que no estén albaraneadas ni facturadas
            If Not linea.estaAlbaraneada Then
                linea.almacen = almacen
            End If
        Next

        RaisePropertyChanged(NameOf(pedido))
    End Sub

    ''' <summary>
    ''' Inicializa el almacén basándose en las líneas del pedido.
    ''' Si todas las líneas tienen el mismo almacén, lo selecciona.
    ''' Si hay diferentes, muestra "VARIOS".
    ''' Carlos 09/12/25: Issue #253/#52 - Mejorado para detectar valores diferentes y usar valor por defecto.
    ''' Carlos 09/12/25: Siempre inicializa para mostrar valores (selector visible siempre, editable solo en CV).
    ''' NOTA: Considera TODAS las líneas, no solo las ficticias, para consistencia con FormaVenta.
    ''' </summary>
    Private Sub InicializarAlmacenParaLineas()
        Debug.WriteLine($"[DetallePedidoVM] InicializarAlmacenParaLineas - EsSerieCursos={EsSerieCursos}, Serie={pedido?.serie}")

        If IsNothing(pedido) OrElse IsNothing(pedido.Model.Lineas) OrElse Not pedido.Model.Lineas.Any() Then
            Debug.WriteLine($"[DetallePedidoVM] InicializarAlmacenParaLineas - Sin líneas, usando defecto: '{AlmacenUsuario}'")
            _almacenSeleccionadoParaLineas = AlmacenUsuario
            RaisePropertyChanged(NameOf(AlmacenSeleccionadoParaLineas))
            Return
        End If

        ' Obtener todos los almacenes distintos de TODAS las líneas (no nulos ni vacíos)
        Dim almacenesDistintos = pedido.Model.Lineas _
            .Where(Function(l) Not String.IsNullOrWhiteSpace(l.almacen)) _
            .Select(Function(l) l.almacen.Trim()) _
            .Distinct() _
            .ToList()

        Debug.WriteLine($"[DetallePedidoVM] InicializarAlmacenParaLineas - {almacenesDistintos.Count} almacenes distintos: {String.Join(", ", almacenesDistintos)}")

        If almacenesDistintos.Count = 0 Then
            ' No hay almacenes definidos en las líneas - usar valor por defecto
            _almacenSeleccionadoParaLineas = AlmacenUsuario
        ElseIf almacenesDistintos.Count = 1 Then
            ' Todas las líneas tienen el mismo almacén
            _almacenSeleccionadoParaLineas = almacenesDistintos.First()
        Else
            ' Hay diferentes almacenes - mostrar valor especial
            _almacenSeleccionadoParaLineas = VALOR_VARIOS
        End If

        Debug.WriteLine($"[DetallePedidoVM] InicializarAlmacenParaLineas - Resultado: '{_almacenSeleccionadoParaLineas}'")
        RaisePropertyChanged(NameOf(AlmacenSeleccionadoParaLineas))
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

    Private _pasarAPresupuestoCommand As DelegateCommand
    Public Property PasarAPresupuestoCommand As DelegateCommand
        Get
            Return _pasarAPresupuestoCommand
        End Get
        Set(value As DelegateCommand)
            Dim unused = SetProperty(_pasarAPresupuestoCommand, value)
        End Set
    End Property
    ''' <summary>
    ''' Indica si se puede pasar el pedido a presupuesto.
    ''' Es posible cuando el pedido no es presupuesto y tiene líneas en estado -1 (pendiente) o 1 (en curso)
    ''' que además no tengan picking asignado.
    ''' </summary>
    Private Function CanPasarAPresupuesto() As Boolean
        Return (Not IsNothing(pedido)) AndAlso
               (Not pedido.EsPresupuesto) AndAlso
               Not IsNothing(pedido.Lineas) AndAlso
               pedido.Lineas.Any(Function(l) (l.estado = Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE OrElse
                                              l.estado = Constantes.LineasPedido.ESTADO_LINEA_EN_CURSO) AndAlso
                                              l.picking = 0)
    End Function
    ''' <summary>
    ''' Pasa las líneas pendientes o en curso (sin picking) a estado presupuesto (-3).
    ''' Las líneas con picking o en otros estados (albarán, factura) no se modifican.
    ''' </summary>
    Private Sub OnPasarAPresupuesto()
        If Not dialogService.ShowConfirmationAnswer("Pasar a Presupuesto", "¿Desea pasar las líneas pendientes a presupuesto?") Then
            Return
        End If

        ' Cambiar estado de las líneas que están en -1 (pendiente) o 1 (en curso) y sin picking
        For Each linea In pedido.Lineas.Where(Function(l) (l.estado = Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE OrElse
                                                           l.estado = Constantes.LineasPedido.ESTADO_LINEA_EN_CURSO) AndAlso
                                                           l.picking = 0)
            linea.estado = Constantes.LineasPedido.ESTADO_LINEA_PRESUPUESTO
        Next

        ' Guardar los cambios
        cmdModificarPedido.Execute()

        ' Actualizar visibilidad de botones
        RaisePropertyChanged(NameOf(mostrarAceptarPresupuesto))
        AceptarPresupuestoCommand.RaiseCanExecuteChanged()
        PasarAPresupuestoCommand.RaiseCanExecuteChanged()
        CrearAlbaranVentaCommand.RaiseCanExecuteChanged()
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
    ''' <summary>
    ''' Propaga la fecha de entrega global a todas las líneas del pedido.
    ''' Issue #51: Solo propaga si no se están usando fechas individuales por línea.
    ''' </summary>
    Private Sub OnCambiarFechaEntrega()
        ' Issue #51: No propagar si se usan fechas individuales
        If UsarFechasIndividuales Then
            Return
        End If

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
            ' Carlos 09/12/25: Issue #253/#52 - Usar valor del selector para serie CV
            lineaActual.Almacen = If(EsSerieCursos AndAlso Not String.IsNullOrEmpty(AlmacenSeleccionadoParaLineas) AndAlso AlmacenSeleccionadoParaLineas <> VALOR_VARIOS,
                                     AlmacenSeleccionadoParaLineas,
                                     AlmacenUsuario)
        End If
        If IsNothing(lineaActual.formaVenta) Then
            lineaActual.formaVenta = pedido.Lineas.FirstOrDefault()?.formaVenta
        End If
        If IsNothing(lineaActual.formaVenta) Then
            ' Carlos 09/12/25: Issue #253/#52 - Usar valor del selector para serie CV
            lineaActual.formaVenta = If(EsSerieCursos AndAlso Not String.IsNullOrEmpty(FormaVentaSeleccionadaParaLineas) AndAlso FormaVentaSeleccionadaParaLineas <> VALOR_VARIOS,
                                        FormaVentaSeleccionadaParaLineas,
                                        FormaVentaUsuario)
        End If
        If IsNothing(lineaActual.delegacion) Then
            lineaActual.delegacion = pedido.Lineas.FirstOrDefault()?.delegacion
        End If
        If IsNothing(lineaActual.delegacion) Then
            lineaActual.delegacion = DelegacionUsuario
        End If
        ' Issue #51: Asignar fecha de entrega a nueva línea
        If IsNothing(lineaActual.fechaEntrega) OrElse lineaActual.fechaEntrega = Date.MinValue Then
            If UsarFechasIndividuales Then
                ' Copiar fecha de la última línea con fecha válida
                Dim ultimaLineaConFecha = pedido.Lineas.LastOrDefault(Function(l) l.fechaEntrega <> Date.MinValue AndAlso l IsNot lineaActual)
                lineaActual.fechaEntrega = If(ultimaLineaConFecha IsNot Nothing, ultimaLineaConFecha.fechaEntrega, fechaEntrega)
            Else
                ' Usar fecha global
                lineaActual.fechaEntrega = fechaEntrega
            End If
            ' Asegurar que nunca quede en MinValue
            If lineaActual.fechaEntrega = Date.MinValue Then
                lineaActual.fechaEntrega = Date.Today
            End If
        End If
        If lineaActual.id = 0 AndAlso Not String.IsNullOrEmpty(VistoBuenoVentas) Then
            lineaActual.vistoBueno = VistoBuenoVentas = "1" OrElse VistoBuenoVentas.ToLower() = "true"
        End If
        Dim textoNuevo As String = String.Empty
        Dim esTextBox As Boolean = False
        ' Issue #258: Soporta tanto DataGridTextColumn (TextBox directo) como DataGridTemplateColumn (TextBox dentro de ContentPresenter)
        Dim textBoxEncontrado As TextBox = TryCast(eventArgs.EditingElement, TextBox)
        If textBoxEncontrado Is Nothing Then
            textBoxEncontrado = FindVisualChild(Of TextBox)(eventArgs.EditingElement)
        End If
        If textBoxEncontrado IsNot Nothing Then
            textoNuevo = textBoxEncontrado.Text
            esTextBox = True
        End If

        ' Issue #258: Solo buscar producto si TipoLinea=1 (producto) - las cuentas contables se manejan en CuentaContableBehavior
        If esTextBox AndAlso eventArgs.Column.Header = "Producto" AndAlso Not IsNothing(lineaActual) AndAlso textoNuevo <> lineaActual.Producto Then
            If lineaActual.tipoLinea.HasValue AndAlso lineaActual.tipoLinea.Value = 1 Then
                Await CargarDatosProducto(textoNuevo, lineaActual.Cantidad)
            End If
        End If
        ' Issue #258: Verificar que textoNuevo sea un número válido antes de convertir
        ' Solo recalcular por cantidad si TipoLinea=1 (producto)
        Dim cantidadNueva As Short = 0
        If esTextBox AndAlso eventArgs.Column.Header = "Cantidad" AndAlso Not IsNothing(lineaActual) AndAlso Short.TryParse(textoNuevo, cantidadNueva) AndAlso cantidadNueva <> lineaActual.Cantidad Then
            If lineaActual.tipoLinea.HasValue AndAlso lineaActual.tipoLinea.Value = 1 Then
                Await CargarDatosProducto(lineaActual.Producto, cantidadNueva)
            End If
        End If
        ' Issue #266: Solo manejar Precio manualmente. Descuento lo maneja PercentageConverter
        If esTextBox AndAlso eventArgs.Column.Header = "Precio" Then
            If textBoxEncontrado IsNot Nothing Then
                ' Windows debería hacer que el teclado numérico escribiese coma en vez de punto
                ' pero como no lo hace, lo cambiamos nosotros
                textBoxEncontrado.Text = Replace(textBoxEncontrado.Text, ".", ",")
                Dim style As NumberStyles = NumberStyles.Number Or NumberStyles.AllowCurrencySymbol
                Dim culture As CultureInfo = CultureInfo.CurrentCulture

                If Not Double.TryParse(textBoxEncontrado.Text, style, culture, (lineaActual.PrecioUnitario)) Then
                    Return
                End If
            End If
        End If
        If eventArgs.Column.Header = "Aplicar Dto." Then
            cmdActualizarTotales.Execute() ' ¿por qué no llamar directamente a raisepropertychanged?
        End If
    End Sub

    Private Async Function CargarDatosProducto(numeroProducto As String, cantidad As Short) As Task
        ' Issue #258: Ignorar si el número de producto está vacío
        If String.IsNullOrWhiteSpace(numeroProducto) Then
            Return
        End If

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
        Else
            ' Issue #258: Mostrar error cuando no se encuentra el producto
            dialogService.ShowError($"El producto '{numeroProducto}' no existe")
            lineaCambio.Producto = String.Empty
            lineaCambio.texto = String.Empty
            lineaCambio.PrecioUnitario = 0
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

#Region "Issue #269: Impresión directa a impresora con selección de bandeja"
    Private _imprimirFacturaDirectoCommand As DelegateCommand
    Public Property ImprimirFacturaDirectoCommand As DelegateCommand
        Get
            Return _imprimirFacturaDirectoCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_imprimirFacturaDirectoCommand, value)
        End Set
    End Property

    Private Async Sub OnImprimirFacturaDirecto()
        Await ImprimirFacturaDirecto(lineaActual.Factura)
    End Sub

    ''' <summary>
    ''' Imprime la factura directamente a la impresora, seleccionando la bandeja según PapelConMembrete.
    ''' Issue #269: Soluciona el problema de que los lectores de PDF no respetan la bandeja.
    ''' </summary>
    Private Async Function ImprimirFacturaDirecto(factura As String) As Task
        If Not dialogService.ShowConfirmationAnswer("Imprimir factura", "¿Desea imprimir la factura directamente en la impresora?") Then
            Return
        End If
        Try
            ' Obtener bytes del PDF desde la API
            Dim bytesPdf = Await servicio.CargarFactura(pedido.empresa, factura, PapelConMembrete)

            If bytesPdf Is Nothing OrElse bytesPdf.Length = 0 Then
                dialogService.ShowError("No se pudo obtener el PDF de la factura")
                Return
            End If

            ' Determinar bandeja según PapelConMembrete
            ' Upper = bandeja con papel preimpreso (membrete)
            ' Lower = bandeja con papel blanco normal
            Dim bandeja = If(PapelConMembrete, TipoBandejaImpresion.Upper, TipoBandejaImpresion.Lower)

            ' Crear datos de impresión
            Dim datosImpresion As New DocumentoParaImprimir With {
                .BytesPDF = bytesPdf,
                .NumeroCopias = 1,
                .TipoBandeja = bandeja
            }

            ' Crear factura DTO para el servicio de impresión
            Dim facturaDTO As New FacturaCreadaDTO With {
                .NumeroFactura = factura,
                .DatosImpresion = datosImpresion
            }

            ' Imprimir usando el servicio existente
            Dim servicioImpresion As New ServicioImpresionDocumentos()
            Dim resultado = Await servicioImpresion.ImprimirFacturas({facturaDTO})

            If resultado.TieneErrores Then
                dialogService.ShowError($"Error al imprimir: {resultado.Errores.First().MensajeError}")
            Else
                dialogService.ShowNotification($"Factura {factura} enviada a la impresora")
            End If

        Catch ex As Exception
            Dim mensajeError = If(IsNothing(ex.InnerException), ex.Message, ex.InnerException.Message)
            dialogService.ShowError($"Error al imprimir la factura: {mensajeError}")
        End Try
    End Function

    Private _imprimirAlbaranDirectoCommand As DelegateCommand
    Public Property ImprimirAlbaranDirectoCommand As DelegateCommand
        Get
            Return _imprimirAlbaranDirectoCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_imprimirAlbaranDirectoCommand, value)
        End Set
    End Property

    Private Async Sub OnImprimirAlbaranDirecto()
        Await ImprimirAlbaranDirecto(lineaActual.Albaran.Value)
    End Sub

    ''' <summary>
    ''' Imprime el albarán directamente a la impresora, seleccionando la bandeja según PapelConMembrete.
    ''' Issue #269: Soluciona el problema de que los lectores de PDF no respetan la bandeja.
    ''' </summary>
    Private Async Function ImprimirAlbaranDirecto(numeroAlbaran As Integer) As Task
        If Not dialogService.ShowConfirmationAnswer("Imprimir albarán", "¿Desea imprimir el albarán directamente en la impresora?") Then
            Return
        End If
        Try
            ' Obtener bytes del PDF desde la API
            Dim bytesPdf = Await servicio.CargarAlbaran(pedido.empresa, numeroAlbaran, PapelConMembrete)

            If bytesPdf Is Nothing OrElse bytesPdf.Length = 0 Then
                dialogService.ShowError("No se pudo obtener el PDF del albarán")
                Return
            End If

            ' Determinar bandeja según PapelConMembrete
            Dim bandeja = If(PapelConMembrete, TipoBandejaImpresion.Upper, TipoBandejaImpresion.Lower)

            ' Crear datos de impresión
            Dim datosImpresion As New DocumentoParaImprimir With {
                .BytesPDF = bytesPdf,
                .NumeroCopias = 1,
                .TipoBandeja = bandeja
            }

            ' Crear albarán DTO para el servicio de impresión
            Dim albaranDTO As New AlbaranCreadoDTO With {
                .NumeroAlbaran = numeroAlbaran,
                .DatosImpresion = datosImpresion
            }

            ' Imprimir usando el servicio existente
            Dim servicioImpresion As New ServicioImpresionDocumentos()
            Dim resultado = Await servicioImpresion.ImprimirAlbaranes({albaranDTO})

            If resultado.TieneErrores Then
                dialogService.ShowError($"Error al imprimir: {resultado.Errores.First().MensajeError}")
            Else
                dialogService.ShowNotification($"Albarán {numeroAlbaran} enviado a la impresora")
            End If

        Catch ex As Exception
            Dim mensajeError = If(IsNothing(ex.InnerException), ex.Message, ex.InnerException.Message)
            dialogService.ShowError($"Error al imprimir el albarán: {mensajeError}")
        End Try
    End Function
#End Region

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

        ' Issue #258: Eliminar líneas de producto (TipoLinea=1) sin referencia antes de guardar
        Dim lineasInvalidas = pedido.Model.Lineas.Where(
            Function(l) l.TipoLinea = 1 AndAlso String.IsNullOrWhiteSpace(l.Producto)
        ).ToList()
        For Each lineaInvalida In lineasInvalidas
            Dim unused = pedido.Model.Lineas.Remove(lineaInvalida)
            Dim wrapperInvalido = pedido.Lineas.FirstOrDefault(Function(w) w.Model Is lineaInvalida)
            If wrapperInvalido IsNot Nothing Then
                Dim unused2 = pedido.Lineas.Remove(wrapperInvalido)
            End If
        Next

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

#Region "Copiar Factura (Issue #85)"
    Private _copiarFacturaCommand As DelegateCommand
    Public Property CopiarFacturaCommand As DelegateCommand
        Get
            Return _copiarFacturaCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_copiarFacturaCommand, value)
        End Set
    End Property

    Private Sub OnCopiarFactura()
        ' Abrir el diálogo de Copiar Factura / Crear Rectificativa usando Prism DialogService
        Dim parameters As New DialogParameters()

        ' Si tenemos un pedido cargado, pasar su empresa y cliente como valores iniciales
        If Not IsNothing(pedido) Then
            parameters.Add("empresa", pedido.empresa)
            parameters.Add("cliente", pedido.cliente)
        End If

        ' Si hay una linea seleccionada con factura, pasar el numero de factura
        If Not IsNothing(lineaActual) AndAlso Not String.IsNullOrWhiteSpace(lineaActual.Factura) Then
            parameters.Add("numeroFactura", lineaActual.Factura.Trim())
        End If

        dialogService.ShowDialog("CopiarFacturaView", parameters, Sub(result)
                                                                      If result.Result = ButtonResult.OK Then
                                                                          ' El usuario quiere abrir el pedido creado
                                                                          Dim numeroPedido = result.Parameters.GetValue(Of Integer)("numeroPedido")
                                                                          If numeroPedido > 0 Then
                                                                              ' Cargar el pedido creado en la vista actual
                                                                              Dim empresaPedido = If(Not IsNothing(pedido), pedido.empresa, "1")
                                                                              cmdCargarPedido.Execute(New ResumenPedido With {.empresa = empresaPedido, .numero = numeroPedido})
                                                                          End If
                                                                      End If
                                                                  End Sub)
    End Sub

    Private Function CanCopiarFactura() As Boolean
        ' Issue #85: Temporalmente restringido solo a Informatica mientras se termina de desarrollar
        Return configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA)
    End Function
#End Region

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

        ' Issue #258: Eliminar líneas de producto (TipoLinea=1) sin referencia
        ' Esto evita enviar la línea en blanco que se crea al dar Enter tras la última referencia
        Dim lineasInvalidas = pedido.Model.Lineas.Where(
            Function(l) l.TipoLinea = 1 AndAlso String.IsNullOrWhiteSpace(l.Producto)
        ).ToList()
        For Each lineaInvalida In lineasInvalidas
            Dim unused = pedido.Model.Lineas.Remove(lineaInvalida)
            ' También quitar del wrapper para mantener sincronizado
            Dim wrapperInvalido = pedido.Lineas.FirstOrDefault(Function(w) w.Model Is lineaInvalida)
            If wrapperInvalido IsNot Nothing Then
                Dim unused2 = pedido.Lineas.Remove(wrapperInvalido)
            End If
        Next

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

    ' Carlos 09/12/25: Issue #245 - Actualizar EsSerieCursos cuando cambia la serie
    ' Carlos 09/12/25: Issue #253/#52 - Reinicializar FormaVenta y Almacén cuando cambia la serie
    Private Sub OnPedidoPropertyChanged(sender As Object, e As ComponentModel.PropertyChangedEventArgs)
        If e.PropertyName = NameOf(pedido.serie) Then
            RaisePropertyChanged(NameOf(EsSerieCursos))
            RaisePropertyChanged(NameOf(HayLineasEditables))
            RaisePropertyChanged(NameOf(PuedeEditarSelectoresLinea))
            ' Reinicializar selectores cuando cambia la serie (ej: al cambiar a CV)
            InicializarFormaVentaParaLineas()
            InicializarAlmacenParaLineas()
            ' Issue #51: Activar fechas individuales automáticamente para serie CV
            UsarFechasIndividuales = EsSerieCursos
        End If
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
        Dim papelMembrete = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PedidoVentaPapelMembrete)
        _papelConMembrete = papelMembrete?.ToLower() = "true"
        RaisePropertyChanged(NameOf(PapelConMembrete))

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

#Region "Helper Visual Tree"
    ''' <summary>
    ''' Busca un hijo de tipo T en el árbol visual.
    ''' Issue #258: Necesario para encontrar TextBox dentro de DataGridTemplateColumn.
    ''' </summary>
    Private Shared Function FindVisualChild(Of T As DependencyObject)(parent As DependencyObject) As T
        If parent Is Nothing Then Return Nothing

        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(parent) - 1
            Dim child As DependencyObject = VisualTreeHelper.GetChild(parent, i)
            If TypeOf child Is T Then
                Return DirectCast(child, T)
            End If

            Dim result As T = FindVisualChild(Of T)(child)
            If result IsNot Nothing Then
                Return result
            End If
        Next

        Return Nothing
    End Function
#End Region

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
