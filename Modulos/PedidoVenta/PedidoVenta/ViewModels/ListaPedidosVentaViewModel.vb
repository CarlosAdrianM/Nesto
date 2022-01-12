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
            .TieneDatosIniciales = True
        }

        'AddHandler ListaPedidos.ElementoSeleccionadoChanged, Sub(sender As Object, args As EventArgs)
        '                                                         CargarResumenSeleccionado()
        '                                                     End Sub

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

    'Private _filtroPedidos As String
    'Public Property filtroPedidos As String
    '    Get
    '        Return _filtroPedidos
    '    End Get
    '    Set(value As String)
    '        SetProperty(_filtroPedidos, value)

    '        If filtroPedidos = "" Then
    '            ListaPedidos.Lista = ListaPedidos.ListaOriginal
    '        Else
    '            If Not IsNothing(ListaPedidos) Then
    '                ListaPedidos = New ObservableCollection(Of ResumenPedido)(ListaPedidos.Where(Function(p) (Not IsNothing(p.direccion) AndAlso p.direccion.ToLower.Contains(filtroPedidos.ToLower)) OrElse
    '                                                                                             (Not IsNothing(p.nombre) AndAlso p.nombre.ToLower.Contains(filtroPedidos.ToLower)) OrElse
    '                                                                                             (Not IsNothing(p.cliente) AndAlso p.cliente.Trim.ToLower.Equals(filtroPedidos.ToLower)) OrElse
    '                                                                                             (p.numero = Me.convertirCadenaInteger(filtroPedidos))
    '                                                                                             ))

    '            End If
    '            If Not IsNothing(ListaPedidos) AndAlso ListaPedidos.Count = 1 Then
    '                resumenSeleccionado = ListaPedidos.FirstOrDefault
    '            End If

    '            If (Not IsNothing(ListaPedidos) AndAlso ListaPedidos.Count = 0) OrElse estaCargandoListaPedidos Then
    '                Dim nuevoResumen As ResumenPedido = New ResumenPedido With {
    '                    .empresa = empresaSeleccionada,
    '                    .numero = filtroPedidos
    '                }
    '                resumenSeleccionado = nuevoResumen
    '            End If


    '        End If

    '        'p.direccion.Contains(filtroPedidos) OrElse
    '        'p.nombre.Contains(filtroPedidos) OrElse
    '        'p.cliente.Contains(filtroPedidos)
    '        'OrElse p.numero = CInt(filtroPedidos)
    '        '   ))
    '    End Set
    'End Property

    Private _listaPedidos As ColeccionFiltrable
    Public Property ListaPedidos() As ColeccionFiltrable
        Get
            Return _listaPedidos
        End Get
        Set(ByVal value As ColeccionFiltrable)
            SetProperty(_listaPedidos, value)
        End Set
    End Property

    'Private _listaPedidosOriginal As ObservableCollection(Of ResumenPedido)
    'Public Property listaPedidosOriginal As ObservableCollection(Of ResumenPedido)
    '    Get
    '        Return _listaPedidosOriginal
    '    End Get
    '    Set(value As ObservableCollection(Of ResumenPedido))
    '        SetProperty(_listaPedidosOriginal, value)
    '    End Set
    'End Property

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
                Dim mensajeError As String = String.Format("Se van a unir los pedidos {0} y {1}, manteniendo los datos de cabecera del {0}", value.numero, resumenSeleccionado.numero)
                Dim continuar As Boolean
                dialogService.ShowConfirmation("Faltan datos en el cliente", mensajeError, Sub(r)
                                                                                               continuar = (r.Result = ButtonResult.OK)
                                                                                           End Sub)
                If continuar Then
                    Try
                        servicio.UnirPedidos(value.empresa, value.numero, resumenSeleccionado.numero)
                        CargarResumenSeleccionado()
                        dialogService.ShowDialog(String.Format("Se han unido los pedidos {0} y {1} correctamente", value.numero, resumenSeleccionado.numero))
                    Catch ex As Exception
                        dialogService.ShowError(ex.Message)
                    End Try
                End If
            End If
        End Set
    End Property

    Private _resumenSeleccionado As ResumenPedido
    Public Property resumenSeleccionado() As ResumenPedido
        Get
            Return _resumenSeleccionado
        End Get
        Set(ByVal value As ResumenPedido)
            SetProperty(_resumenSeleccionado, value)
            CargarResumenSeleccionado()
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
            If vendedor.Trim() <> "" Then
                Dim verTodos As Integer = Await configuracion.leerParametro("1", "PermitirVerTodosLosPedidos")
                If verTodos = "1" Then
                    vendedor = ""
                End If
            End If
            ListaPedidos.ListaOriginal = New ObservableCollection(Of IFiltrableItem)(Await servicio.cargarListaPedidos(vendedor, verTodosLosVendedores, mostrarPresupuestos))
            'listaPedidosOriginal = ListaPedidos
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaCargandoListaPedidos = False
        End Try
    End Sub

#End Region

#Region "Funciones auxiliares"
    Private Sub ActualizarResumen(pedido As Models.PedidoVenta.PedidoVentaDTO)
        resumenSeleccionado.baseImponible = pedido.baseImponible
        resumenSeleccionado.contacto = pedido.contacto
        resumenSeleccionado.tienePicking = Not IsNothing(pedido.LineasPedido.FirstOrDefault(Function(p) p.picking > 0))
        resumenSeleccionado.tieneFechasFuturas = Not IsNothing(pedido.LineasPedido.FirstOrDefault(Function(c) c.estado >= -1 AndAlso c.estado <= 1 AndAlso c.fechaEntrega > Today))
        resumenSeleccionado.tieneProductos = Not IsNothing(pedido.LineasPedido.FirstOrDefault(Function(l) l.tipoLinea = 1))
        RaisePropertyChanged(NameOf(resumenSeleccionado))
    End Sub

    Private Async Function CargarResumenSeleccionado() As Task
        Dim parameters As NavigationParameters = New NavigationParameters()
        parameters.Add("resumenPedidoParameter", resumenSeleccionado)
        scopedRegionManager.RequestNavigate("DetallePedidoRegion", "DetallePedidoView", parameters)
        If Not IsNothing(resumenSeleccionado) Then
            empresaSeleccionada = resumenSeleccionado.empresa
            Dim pendientes As ObservableCollection(Of Integer) = Await servicio.CargarPedidosPendientes(resumenSeleccionado.empresa, resumenSeleccionado.cliente)
            ListaPedidosPendientes = New ObservableCollection(Of ResumenPedido)(pendientes.Where(Function(p) p <> resumenSeleccionado.numero).Select(Function(p) New ResumenPedido With {
                .empresa = resumenSeleccionado.empresa,
                .numero = p
                                                                                                                                                   }))
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
