Imports System.Collections.ObjectModel
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Regions
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models

Public Class ListaPedidosVentaViewModel
    Inherits BindableBase


    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IPedidoVentaService
    Private ReadOnly dialogService As IDialogService

    Private vendedor As String
    Private verTodosLosVendedores As Boolean = False

    Public Sub New(configuracion As IConfiguracion, servicio As IPedidoVentaService, eventAggregator As IEventAggregator, dialogService As IDialogService)
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.dialogService = dialogService

        cmdCargarListaPedidos = New DelegateCommand(AddressOf OnCargarListaPedidos)

        eventAggregator.GetEvent(Of SacarPickingEvent).Subscribe(AddressOf CargarResumenSeleccionado)
        eventAggregator.GetEvent(Of PedidoModificadoEvent).Subscribe(AddressOf ActualizarResumen)

        ListaPedidos = New ColeccionFiltrable With {
            .TieneDatosIniciales = True,
            .SeleccionarPrimerElemento = False
        }

        'AddHandler ListaPedidos.ElementoSeleccionadoChanged, Sub(sender, args) resumenSeleccionado = ListaPedidos.ElementoSeleccionado
        AddHandler ListaPedidos.ElementoSeleccionadoChanged, Sub(sender, args) CargarResumenSeleccionado()

        AddHandler ListaPedidos.HayQueCargarDatos, Sub()
                                                       Dim numeroPedido As Integer
                                                       If Not IsNothing(ListaPedidos) AndAlso Not IsNothing(ListaPedidos.Lista) AndAlso Not ListaPedidos.Lista.Any AndAlso Integer.TryParse(ListaPedidos.Filtro, numeroPedido) Then
                                                           Dim nuevoResumen As ResumenPedido = New ResumenPedido With {
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
            SetProperty(_empresaSeleccionada, value)
        End Set
    End Property

    Private _estaCargandoListaPedidos As Boolean
    Public Property estaCargandoListaPedidos() As Boolean
        Get
            Return _estaCargandoListaPedidos
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaCargandoListaPedidos, value)
        End Set
    End Property

    Private _listaPedidos As ColeccionFiltrable
    Public Property ListaPedidos() As ColeccionFiltrable
        Get
            Return _listaPedidos
        End Get
        Set(ByVal value As ColeccionFiltrable)
            SetProperty(_listaPedidos, value)
        End Set
    End Property

    Private _listaPedidosPendientes As ObservableCollection(Of ResumenPedido)
    Public Property ListaPedidosPendientes As ObservableCollection(Of ResumenPedido)
        Get
            Return _listaPedidosPendientes
        End Get
        Set(ByVal value As ObservableCollection(Of ResumenPedido))
            SetProperty(_listaPedidosPendientes, value)
        End Set
    End Property

    Private _mostrarPresupuestos As Boolean = False
    Public Property mostrarPresupuestos As Boolean
        Get
            Return _mostrarPresupuestos
        End Get
        Set(value As Boolean)
            If value <> _mostrarPresupuestos Then
                SetProperty(_mostrarPresupuestos, value)
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
                SetProperty(_mostrarSoloPendientes, value)
                If value Then
                    ListaPedidos.Lista = New ObservableCollection(Of IFiltrableItem)(ListaPedidos.Lista.Where(Function(l) CType(l, ResumenPedido).tienePendientes))
                Else
                    ListaPedidos.Lista = ListaPedidos.ListaOriginal
                End If

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
                SetProperty(_mostrarSoloPicking, value)
                If value Then
                    ListaPedidos.Lista = New ObservableCollection(Of IFiltrableItem)(ListaPedidos.Lista.Where(Function(l) CType(l, ResumenPedido).tienePicking))
                Else
                    ListaPedidos.Lista = ListaPedidos.ListaOriginal
                End If

            End If
        End Set
    End Property

    Private _pedidoPendienteUnir As ResumenPedido
    Public Property PedidoPendienteUnir As ResumenPedido
        Get
            Return _pedidoPendienteUnir
        End Get
        Set(value As ResumenPedido)
            SetProperty(_pedidoPendienteUnir, value)
            If Not IsNothing(value) Then
                Dim mensajeError As String = String.Format("Se van a unir los pedidos {0} y {1}, manteniendo los datos de cabecera del {0}", value.numero, CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero)
                Dim continuar As Boolean
                dialogService.ShowConfirmation("Faltan datos en el cliente", mensajeError, Sub(r)
                                                                                               continuar = (r.Result = ButtonResult.OK)
                                                                                           End Sub)
                If continuar Then
                    Try
                        servicio.UnirPedidos(value.empresa, value.numero, CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero)
                        CargarResumenSeleccionado()
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
            SetProperty(_scopedRegionManager, value)
        End Set
    End Property

    Public ReadOnly Property TextoUnirPedido As String
        Get
            If IsNothing(ListaPedidosPendientes) OrElse ListaPedidosPendientes.Count = 0 Then
                Return String.Empty
            End If
            Return String.Format("Unir ({0})", ListaPedidosPendientes.Count)
        End Get
    End Property


#End Region

#Region "Comandos"
    Private _cmdCargarListaPedidos As DelegateCommand
    Public Property cmdCargarListaPedidos As DelegateCommand
        Get
            Return _cmdCargarListaPedidos
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCargarListaPedidos, value)
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

#End Region

#Region "Funciones auxiliares"
    Private Function FechaEntregaAjustada(fecha As DateTime, ruta As String) As DateTime
        Dim fechaMinima As DateTime

        If ruta <> Constantes.Pedidos.RUTA_GLOVO AndAlso EsRutaConPortes(ruta) Then
            fechaMinima = IIf(DateTime.Now.Hour < Constantes.Picking.HORA_MAXIMA_AMPLIAR_PEDIDOS, DateTime.Today, DateTime.Today.AddDays(1))
        Else
            fechaMinima = DateTime.Today
        End If
        Return IIf(fechaMinima < fecha, fecha, fechaMinima)
    End Function
    Private Function EsRutaConPortes(ruta As String) As Boolean
        Return IsNothing(ruta) OrElse ruta.Trim = "FW" OrElse ruta.Trim = "00" OrElse ruta.Trim = "16" OrElse ruta.Trim = "AT" OrElse ruta.Trim = "OT"
    End Function

    Private Sub ActualizarResumen(pedido As PedidoVentaDTO)
        If IsNothing(pedido) OrElse IsNothing(ListaPedidos.ElementoSeleccionado) OrElse pedido.numero <> CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero Then
            Return
        End If
        CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).baseImponible = pedido.BaseImponible
        CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).contacto = pedido.contacto
        CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).tienePicking = pedido.Lineas.Any(Function(p) p.picking <> 0 AndAlso p.estado < Constantes.LineasPedido.ESTADO_ALBARAN)
        CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).tieneFechasFuturas = pedido.Lineas.Any(Function(c) c.estado >= -1 AndAlso c.estado <= 1 AndAlso c.fechaEntrega > FechaEntregaAjustada(DateTime.Now, pedido.ruta))
        CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).tieneProductos = pedido.Lineas.Any(Function(l) l.tipoLinea = 1)
    End Sub

    Private Async Function CargarResumenSeleccionado() As Task
        Dim parameters As NavigationParameters = New NavigationParameters()
        parameters.Add("resumenPedidoParameter", CType(ListaPedidos.ElementoSeleccionado, ResumenPedido))
        scopedRegionManager.RequestNavigate("DetallePedidoRegion", "DetallePedidoView", parameters)
        If Not IsNothing(ListaPedidos.ElementoSeleccionado) Then
            empresaSeleccionada = CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).empresa
            Dim pendientes As ObservableCollection(Of Integer) = Await servicio.CargarPedidosPendientes(CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).empresa, CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).cliente)
            ListaPedidosPendientes = New ObservableCollection(Of ResumenPedido)(pendientes.Where(Function(p) p <> CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).numero).Select(Function(p) New ResumenPedido With {
                .empresa = CType(ListaPedidos.ElementoSeleccionado, ResumenPedido).empresa,
                .numero = p
                                                                                                                                                   }))
            'ListaPedidos.ElementoSeleccionado = ListaPedidos.Lista.FirstOrDefault(Function(p) CType(p, ResumenPedido).numero = resumenSeleccionado.numero)
            RaisePropertyChanged(NameOf(TextoUnirPedido))
        End If
    End Function



    Public Async Function cargarPedidoPorDefecto() As Task(Of ResumenPedido)
        Dim empresaDefecto As String = Await configuracion.leerParametro("1", "EmpresaPorDefecto")
        Dim pedidoDefecto As String = Await configuracion.leerParametro(empresaDefecto, "UltNumPedidoVta")
        Dim nuevoResumen As ResumenPedido = New ResumenPedido With {
            .empresa = empresaDefecto,
            .numero = pedidoDefecto
        }
        Return nuevoResumen
    End Function

#End Region
End Class
