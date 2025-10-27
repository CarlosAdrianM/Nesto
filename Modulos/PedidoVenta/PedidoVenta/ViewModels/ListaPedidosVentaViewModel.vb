Imports System.Collections.ObjectModel
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Events
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Mvvm
Imports Prism.Regions
Imports Prism.Services.Dialogs

Public Class ListaPedidosVentaViewModel
    Inherits BindableBase


    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IPedidoVentaService
    Private ReadOnly dialogService As IDialogService

    Private vendedor As String
    Private ReadOnly verTodosLosVendedores As Boolean = False

    Public Event PedidoCreadoConfirmado(numeroPedido As Integer)
    Public Event PedidoCreacionCancelada()

    Public Sub New(configuracion As IConfiguracion, servicio As IPedidoVentaService, eventAggregator As IEventAggregator, dialogService As IDialogService)
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.dialogService = dialogService

        cmdCargarListaPedidos = New DelegateCommand(AddressOf OnCargarListaPedidos)
        CrearPedidoCommand = New DelegateCommand(AddressOf OnCrearPedido)
        CancelarCreacionCommand = New DelegateCommand(AddressOf OnCancelarCreacion)

        Dim unused3 = eventAggregator.GetEvent(Of SacarPickingEvent).Subscribe(AddressOf CargarResumenSeleccionado)
        Dim unused2 = eventAggregator.GetEvent(Of PedidoModificadoEvent).Subscribe(AddressOf ActualizarResumen)
        Dim unused1 = eventAggregator.GetEvent(Of PedidoCreadoEvent).Subscribe(AddressOf OnPedidoCreado)

        ListaPedidos = New ColeccionFiltrable With {
            .TieneDatosIniciales = True,
            .SeleccionarPrimerElemento = False
        }

        'AddHandler ListaPedidos.ElementoSeleccionadoChanged, Sub(sender, args) resumenSeleccionado = ListaPedidos.ElementoSeleccionado
        AddHandler ListaPedidos.ElementoSeleccionadoChanged, Sub(sender, args)
                                                                 Dim unused = CargarResumenSeleccionado()
                                                             End Sub

        AddHandler ListaPedidos.HayQueCargarDatos, Sub()
                                                       Dim numeroPedido As Integer
                                                       If Not IsNothing(ListaPedidos) AndAlso Not IsNothing(ListaPedidos.Lista) AndAlso Not ListaPedidos.Lista.Any AndAlso Integer.TryParse(ListaPedidos.Filtro, numeroPedido) Then
                                                           Dim nuevoResumen As New ResumenPedido With {
                                                                    .empresa = empresaSeleccionada,
                                                                    .numero = numeroPedido
                                                                 }
                                                           ListaPedidos.FiltrosPuestos.Clear()
                                                           ListaPedidos.ElementoSeleccionado = nuevoResumen
                                                       End If
                                                   End Sub
    End Sub

#Region "Propiedades"
    Private _empresaSeleccionada As String = "1  "
    Public Property empresaSeleccionada As String
        Get
            Return _empresaSeleccionada
        End Get
        Set(value As String)
            Dim unused = SetProperty(_empresaSeleccionada, value)
        End Set
    End Property

    Private _estaCargandoListaPedidos As Boolean
    Public Property estaCargandoListaPedidos() As Boolean
        Get
            Return _estaCargandoListaPedidos
        End Get
        Set(ByVal value As Boolean)
            Dim unused = SetProperty(_estaCargandoListaPedidos, value)
        End Set
    End Property

    Public ReadOnly Property EstaCreandoPedido As Boolean
        Get
            Dim resultado As Boolean = Not IsNothing(ListaPedidos.ElementoSeleccionado) AndAlso
               TypeOf ListaPedidos.ElementoSeleccionado Is ResumenPedido AndAlso
               CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero = 0
            Return resultado
        End Get
    End Property

    Public ReadOnly Property EstaCreandoPedidoInvertida As Boolean
        Get
            Return Not EstaCreandoPedido
        End Get
    End Property

    Private _listaPedidos As ColeccionFiltrable
    Public Property ListaPedidos() As ColeccionFiltrable
        Get
            Return _listaPedidos
        End Get
        Set(ByVal value As ColeccionFiltrable)
            Dim unused = SetProperty(_listaPedidos, value)
        End Set
    End Property

    Private _listaPedidosPendientes As ObservableCollection(Of ResumenPedido)
    Public Property ListaPedidosPendientes As ObservableCollection(Of ResumenPedido)
        Get
            Return _listaPedidosPendientes
        End Get
        Set(ByVal value As ObservableCollection(Of ResumenPedido))
            Dim unused = SetProperty(_listaPedidosPendientes, value)
        End Set
    End Property

    Private _mostrarPresupuestos As Boolean = False
    Public Property mostrarPresupuestos As Boolean
        Get
            Return _mostrarPresupuestos
        End Get
        Set(value As Boolean)
            If value <> _mostrarPresupuestos Then
                Dim unused = SetProperty(_mostrarPresupuestos, value)
                'listaPedidosOriginal = Nothing
                'ListaPedidos = Nothing
                ListaPedidos.ListaOriginal = Nothing
                mostrarSoloPendientes = False
                mostrarSoloPicking = False
                cmdCargarListaPedidos.Execute()
            End If
        End Set
    End Property

    Private _mostrarSoloPendientes As Boolean = False
    Public Property mostrarSoloPendientes As Boolean
        Get
            Return _mostrarSoloPendientes
        End Get
        Set(value As Boolean)
            If value <> _mostrarSoloPendientes Then
                Dim unused = SetProperty(_mostrarSoloPendientes, value)
                ListaPedidos.Lista = If(value,
                    New ObservableCollection(Of IFiltrableItem)(ListaPedidos.Lista.Where(Function(l) CType(l, ResumenPedido).tienePendientes)),
                    ListaPedidos.ListaOriginal)

            End If
        End Set
    End Property

    Private _mostrarSoloPicking As Boolean = False
    Public Property mostrarSoloPicking As Boolean
        Get
            Return _mostrarSoloPicking
        End Get
        Set(value As Boolean)
            If value <> _mostrarSoloPicking Then
                Dim unused = SetProperty(_mostrarSoloPicking, value)
                ListaPedidos.Lista = If(value,
                    New ObservableCollection(Of IFiltrableItem)(ListaPedidos.Lista.Where(Function(l) CType(l, ResumenPedido).tienePicking)),
                    ListaPedidos.ListaOriginal)

            End If
        End Set
    End Property

    Private _pedidoPendienteUnir As ResumenPedido
    Public Property PedidoPendienteUnir As ResumenPedido
        Get
            Return _pedidoPendienteUnir
        End Get
        Set(value As ResumenPedido)
            Dim unused2 = SetProperty(_pedidoPendienteUnir, value)
            If Not IsNothing(value) Then
                Dim mensajeError As String = String.Format("Se van a unir los pedidos {0} y {1}, manteniendo los datos de cabecera del {0}", value.numero, CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero)
                Dim continuar As Boolean
                dialogService.ShowConfirmation("Faltan datos en el cliente", mensajeError, Sub(r)
                                                                                               continuar = r.Result = ButtonResult.OK
                                                                                           End Sub)
                If continuar Then
                    Try
                        Dim unused1 = servicio.UnirPedidos(value.empresa, value.numero, CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero)
                        Dim unused = CargarResumenSeleccionado()
                        dialogService.ShowDialog(String.Format("Se han unido los pedidos {0} y {1} correctamente", value.numero, CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero))
                    Catch ex As Exception
                        dialogService.ShowError(ex.Message)
                    End Try
                End If
            End If
        End Set
    End Property

    Private _scopedRegionManager As IRegionManager
    Public Property scopedRegionManager As IRegionManager
        Get
            Return _scopedRegionManager
        End Get
        Set(value As IRegionManager)
            Dim unused = SetProperty(_scopedRegionManager, value)
        End Set
    End Property

    Public ReadOnly Property TextoUnirPedido As String
        Get
            Return If(IsNothing(ListaPedidosPendientes) OrElse ListaPedidosPendientes.Count = 0,
                String.Empty,
                String.Format("Unir ({0})", ListaPedidosPendientes.Count))
        End Get
    End Property


#End Region

#Region "Comandos"

    Private _cancelarCreacionCommand As DelegateCommand
    Public Property CancelarCreacionCommand As DelegateCommand
        Get
            Return _cancelarCreacionCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cancelarCreacionCommand, value)
        End Set
    End Property

    Private Sub OnCancelarCreacion()
        CancelarCreacionPedido()
    End Sub

    Private _cmdCargarListaPedidos As DelegateCommand
    Public Property cmdCargarListaPedidos As DelegateCommand
        Get
            Return _cmdCargarListaPedidos
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_cmdCargarListaPedidos, value)
        End Set
    End Property
    Private Async Sub OnCargarListaPedidos()
        If Not IsNothing(ListaPedidos) AndAlso Not IsNothing(ListaPedidos.Lista) AndAlso ListaPedidos.Lista.Any Then
            Return
        End If
        Try
            estaCargandoListaPedidos = True
            vendedor = Await configuracion.leerParametro("1", "Vendedor")
            If Not String.IsNullOrWhiteSpace(vendedor) Then
                Dim verTodos As String = Await configuracion.leerParametro("1", "PermitirVerTodosLosPedidos")
                If verTodos = "1" Then
                    vendedor = String.Empty
                End If
            End If
            ListaPedidos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(Await servicio.cargarListaPedidos(vendedor, verTodosLosVendedores, mostrarPresupuestos))
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaCargandoListaPedidos = False
            ListaPedidos.RefrescarFiltro()
        End Try
    End Sub


    Private _crearPedidoCommand As DelegateCommand
    Public Property CrearPedidoCommand As DelegateCommand
        Get
            Return _crearPedidoCommand
        End Get
        Private Set(value As DelegateCommand)
            Dim unused = SetProperty(_crearPedidoCommand, value)
        End Set
    End Property
    Private Async Sub OnCrearPedido()
        Try
            ' Crear un nuevo ResumenPedido para el pedido en creación
            Dim nuevoResumen As ResumenPedido = Await CrearResumenPedidoNuevo()

            ' Añadirlo a la lista (temporalmente, sin guardarlo en BD aún)
            ListaPedidos.ListaOriginal.Insert(0, nuevoResumen)
            ListaPedidos.RefrescarFiltro()

            ' Seleccionarlo para que se navegue automáticamente al detalle
            ListaPedidos.ElementoSeleccionado = nuevoResumen

        Catch ex As Exception
            dialogService.ShowError("Error al crear nuevo pedido: " & ex.Message)
        End Try
    End Sub

#End Region

#Region "Funciones auxiliares"
    Private Function FechaEntregaAjustada(fecha As Date, ruta As String) As Date
        Dim fechaMinima = If(ruta <> Constantes.Pedidos.RUTA_GLOVO AndAlso EsRutaConPortes(ruta),
            DirectCast(IIf(Date.Now.Hour < Constantes.Picking.HORA_MAXIMA_AMPLIAR_PEDIDOS, Date.Today, Date.Today.AddDays(1)), Date),
            Date.Today)
        Return IIf(fechaMinima < fecha, fecha, fechaMinima)
    End Function
    Private Function EsRutaConPortes(ruta As String) As Boolean
        Return IsNothing(ruta) OrElse ruta.Trim = "FW" OrElse ruta.Trim = "00" OrElse ruta.Trim = "16" OrElse ruta.Trim = "AT" OrElse ruta.Trim = "OT"
    End Function

    Private Sub ActualizarResumen(pedido As PedidoVentaDTO)
        If IsNothing(pedido) OrElse IsNothing(ListaPedidos.ElementoSeleccionado) OrElse pedido.numero <> CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero Then
            Return
        End If
        Dim resumen = CType(ListaPedidos.ElementoSeleccionado, ResumenPedido)
        resumen.baseImponible = pedido.BaseImponible
        resumen.contacto = pedido.contacto
        resumen.vendedor = pedido.vendedor
        resumen.fecha = pedido.fecha
        resumen.cliente = pedido.cliente
        resumen.tienePicking = pedido.Lineas.Any(Function(p) p.picking <> 0 AndAlso p.estado < Constantes.LineasPedido.ESTADO_ALBARAN)
        resumen.tieneFechasFuturas = pedido.Lineas.Any(Function(c) c.estado >= -1 AndAlso c.estado <= 1 AndAlso c.fechaEntrega > FechaEntregaAjustada(Date.Now, pedido.ruta))
        resumen.tieneProductos = pedido.Lineas.Any(Function(l) l.tipoLinea.HasValue AndAlso l.tipoLinea.Value = 1)
        resumen.tienePendientes = pedido.Lineas.Any(Function(l) l.estado = Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE)
    End Sub

    Private Async Function CargarResumenSeleccionado() As Task
        Dim parameters As New NavigationParameters From {
            {"resumenPedidoParameter", CType(ListaPedidos.ElementoSeleccionado, ResumenPedido)}
        }
        scopedRegionManager.RequestNavigate("DetallePedidoRegion", "DetallePedidoView", parameters)
        If Not IsNothing(ListaPedidos.ElementoSeleccionado) Then
            Dim resumenSeleccionado = CType(ListaPedidos.ElementoSeleccionado, ResumenPedido)
            empresaSeleccionada = resumenSeleccionado.empresa

            If Not resumenSeleccionado.esNuevo AndAlso resumenSeleccionado.numero > 0 Then
                Dim pendientes As ObservableCollection(Of Integer) = Await servicio.CargarPedidosPendientes(resumenSeleccionado.empresa, resumenSeleccionado.cliente)
                ListaPedidosPendientes = New ObservableCollection(Of ResumenPedido)(pendientes.Where(Function(p) p <> resumenSeleccionado.numero).Select(Function(p) New ResumenPedido With {
                    .empresa = resumenSeleccionado.empresa,
                    .numero = p
                }))
            Else
                ListaPedidosPendientes = New ObservableCollection(Of ResumenPedido)()
            End If

            RaisePropertyChanged(NameOf(TextoUnirPedido))
            RaisePropertyChanged(NameOf(EstaCreandoPedido))
            RaisePropertyChanged(NameOf(EstaCreandoPedidoInvertida))
        End If
    End Function

    Public Async Function cargarPedidoPorDefecto() As Task(Of ResumenPedido)
        Dim empresaDefecto As String = Await configuracion.leerParametro("1", "EmpresaPorDefecto")
        Dim pedidoDefecto As String = Await configuracion.leerParametro(empresaDefecto, "UltNumPedidoVta")
        Dim nuevoResumen As New ResumenPedido With {
            .empresa = empresaDefecto,
            .numero = pedidoDefecto
        }
        Return nuevoResumen
    End Function

    Private Async Function CrearResumenPedidoNuevo() As Task(Of ResumenPedido)
        Dim empresaDefecto As String = Await configuracion.leerParametro("1", "EmpresaPorDefecto")
        Dim vendedorDefecto As String = Await configuracion.leerParametro("1", "Vendedor")

        ' Crear un ResumenPedido con valores por defecto para nuevo pedido
        Dim nuevoResumen As New ResumenPedido With {
            .empresa = empresaDefecto,
            .numero = 0, ' Número 0 indica que es un pedido nuevo sin guardar
            .fecha = Date.Today,
            .vendedor = vendedorDefecto,
            .baseImponible = 0,
            .contacto = String.Empty,
            .cliente = String.Empty,
            .nombre = "[NUEVO PEDIDO]", ' Texto que se mostrará en la lista
            .tienePicking = False,
            .tienePendientes = False,
            .tieneFechasFuturas = False,
            .tieneProductos = False,
            .esNuevo = True ' Propiedad adicional para identificar pedidos nuevos
        }

        Return nuevoResumen
    End Function

    ' Método para eliminar un pedido nuevo si el usuario cancela la creación
    Public Sub CancelarCreacionPedido()
        If Not IsNothing(ListaPedidos.ElementoSeleccionado) AndAlso
           TypeOf ListaPedidos.ElementoSeleccionado Is ResumenPedido AndAlso
           CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero = 0 Then

            Dim unused = ListaPedidos.ListaOriginal.Remove(ListaPedidos.ElementoSeleccionado)
            ListaPedidos.RefrescarFiltro()

            ' Seleccionar el primer elemento válido si existe
            ListaPedidos.ElementoSeleccionado = If(ListaPedidos.ListaOriginal.Count > 0, ListaPedidos.ListaOriginal.First(), Nothing)
        End If
    End Sub

    ' Método para confirmar la creación y actualizar el resumen con el número real
    Public Sub ConfirmarCreacionPedido(numeroPedidoReal As Integer)
        If Not IsNothing(ListaPedidos.ElementoSeleccionado) AndAlso
           TypeOf ListaPedidos.ElementoSeleccionado Is ResumenPedido AndAlso
           CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero = 0 Then

            Dim resumen = CType(ListaPedidos.ElementoSeleccionado, ResumenPedido)
            resumen.numero = numeroPedidoReal
            resumen.nombre = $"Pedido {numeroPedidoReal}" ' O el formato que uses
            resumen.esNuevo = False

            ' Notificar cambios en las propiedades
            RaisePropertyChanged(NameOf(ListaPedidos))
        End If
    End Sub

    Public Sub NotificarPedidoGuardado(numeroPedidoReal As Integer)
        ConfirmarCreacionPedido(numeroPedidoReal)
        RaiseEvent PedidoCreadoConfirmado(numeroPedidoReal)
        RaisePropertyChanged(NameOf(EstaCreandoPedido))
        RaisePropertyChanged(NameOf(EstaCreandoPedidoInvertida))
    End Sub

    Public Sub NotificarCancelacionCreacion()
        CancelarCreacionPedido()
        RaiseEvent PedidoCreacionCancelada()
        RaisePropertyChanged(NameOf(EstaCreandoPedido))
        RaisePropertyChanged(NameOf(EstaCreandoPedidoInvertida))
    End Sub

    Private Sub OnPedidoCreado(eventArgs As PedidoCreadoEventArgs)
        ' Usar el ElementoSeleccionado si es un pedido nuevo (numero = 0)
        ' Esto maneja correctamente el caso de estar creando el pedido actual
        Dim resumenEnCreacion As ResumenPedido = Nothing

        If Not IsNothing(ListaPedidos.ElementoSeleccionado) AndAlso
           TypeOf ListaPedidos.ElementoSeleccionado Is ResumenPedido Then

            Dim resumenSeleccionado = CType(ListaPedidos.ElementoSeleccionado, ResumenPedido)
            If resumenSeleccionado.numero = 0 AndAlso resumenSeleccionado.empresa = eventArgs.Pedido.empresa Then
                resumenEnCreacion = resumenSeleccionado
            End If
        End If

        If IsNothing(resumenEnCreacion) Then
            Return
        End If

        ' Actualizar el ResumenPedido temporal con los datos reales del pedido guardado
        resumenEnCreacion.numero = eventArgs.Pedido.numero
        resumenEnCreacion.cliente = eventArgs.Pedido.cliente
        resumenEnCreacion.contacto = eventArgs.Pedido.contacto
        resumenEnCreacion.nombre = eventArgs.NombreCliente
        resumenEnCreacion.direccion = eventArgs.DireccionCliente
        resumenEnCreacion.codPostal = eventArgs.CodigoPostal
        resumenEnCreacion.poblacion = eventArgs.Poblacion
        resumenEnCreacion.provincia = eventArgs.Provincia
        resumenEnCreacion.fecha = eventArgs.Pedido.fecha
        resumenEnCreacion.baseImponible = eventArgs.Pedido.BaseImponible
        resumenEnCreacion.vendedor = eventArgs.Pedido.vendedor
        resumenEnCreacion.esNuevo = False ' Ya no es nuevo, está persistido

        ' Actualizar propiedades calculadas
        ' Usar el valor calculado en el evento para tieneProductos
        resumenEnCreacion.tieneProductos = eventArgs.TieneProductos

        If Not IsNothing(eventArgs.Pedido.Lineas) AndAlso eventArgs.Pedido.Lineas.Any() Then
            resumenEnCreacion.tienePicking = eventArgs.Pedido.Lineas.Any(Function(p) p.picking <> 0 AndAlso p.estado < Constantes.LineasPedido.ESTADO_ALBARAN)
            resumenEnCreacion.tieneFechasFuturas = eventArgs.Pedido.Lineas.Any(Function(c) c.estado >= -1 AndAlso c.estado <= 1 AndAlso c.fechaEntrega > FechaEntregaAjustada(Date.Now, eventArgs.Pedido.ruta))
            resumenEnCreacion.tienePendientes = eventArgs.Pedido.Lineas.Any(Function(l) l.estado = Constantes.LineasPedido.ESTADO_LINEA_PENDIENTE)
        Else
            resumenEnCreacion.tienePicking = False
            resumenEnCreacion.tieneFechasFuturas = False
            resumenEnCreacion.tienePendientes = False
        End If

        ' Notificar cambios en la UI
        RaisePropertyChanged(NameOf(EstaCreandoPedido))
        RaisePropertyChanged(NameOf(EstaCreandoPedidoInvertida))
        RaisePropertyChanged(NameOf(ListaPedidos))
    End Sub

#End Region
End Class
