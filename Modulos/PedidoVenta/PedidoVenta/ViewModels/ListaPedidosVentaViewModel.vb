﻿Imports System.Collections.ObjectModel
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Regions
Imports Nesto.Contratos
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs

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

    Private _filtroPedidos As String
    Public Property filtroPedidos As String
        Get
            Return _filtroPedidos
        End Get
        Set(value As String)
            SetProperty(_filtroPedidos, value)

            If filtroPedidos = "" Then
                listaPedidos = listaPedidosOriginal
            Else
                If Not IsNothing(listaPedidos) Then
                    listaPedidos = New ObservableCollection(Of ResumenPedido)(listaPedidos.Where(Function(p) (Not IsNothing(p.direccion) AndAlso p.direccion.ToLower.Contains(filtroPedidos.ToLower)) OrElse
                                                                                                 (Not IsNothing(p.nombre) AndAlso p.nombre.ToLower.Contains(filtroPedidos.ToLower)) OrElse
                                                                                                 (Not IsNothing(p.cliente) AndAlso p.cliente.Trim.ToLower.Equals(filtroPedidos.ToLower)) OrElse
                                                                                                 (p.numero = Me.convertirCadenaInteger(filtroPedidos))
                                                                                                 ))

                End If
                If Not IsNothing(listaPedidos) AndAlso listaPedidos.Count = 1 Then
                    resumenSeleccionado = listaPedidos.FirstOrDefault
                End If

                If (Not IsNothing(listaPedidos) AndAlso listaPedidos.Count = 0) OrElse estaCargandoListaPedidos Then
                    Dim nuevoResumen As ResumenPedido = New ResumenPedido With {
                        .empresa = empresaSeleccionada,
                        .numero = filtroPedidos
                    }
                    resumenSeleccionado = nuevoResumen
                End If


            End If

            'p.direccion.Contains(filtroPedidos) OrElse
            'p.nombre.Contains(filtroPedidos) OrElse
            'p.cliente.Contains(filtroPedidos)
            'OrElse p.numero = CInt(filtroPedidos)
            '   ))
        End Set
    End Property

    Private _listaPedidos As ObservableCollection(Of ResumenPedido)
    Public Property listaPedidos() As ObservableCollection(Of ResumenPedido)
        Get
            Return _listaPedidos
        End Get
        Set(ByVal value As ObservableCollection(Of ResumenPedido))
            SetProperty(_listaPedidos, value)
        End Set
    End Property

    Private _listaPedidosOriginal As ObservableCollection(Of ResumenPedido)
    Public Property listaPedidosOriginal As ObservableCollection(Of ResumenPedido)
        Get
            Return _listaPedidosOriginal
        End Get
        Set(value As ObservableCollection(Of ResumenPedido))
            SetProperty(_listaPedidosOriginal, value)
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
                listaPedidosOriginal = Nothing
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
                    listaPedidos = New ObservableCollection(Of ResumenPedido)(listaPedidos.Where(Function(l) l.tienePendientes))
                Else
                    listaPedidos = listaPedidosOriginal
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
                    listaPedidos = New ObservableCollection(Of ResumenPedido)(listaPedidos.Where(Function(l) l.tienePicking))
                Else
                    listaPedidos = listaPedidosOriginal
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
        If Not IsNothing(listaPedidos) AndAlso listaPedidos.Any Then
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
            listaPedidos = Await servicio.cargarListaPedidos(vendedor, verTodosLosVendedores, mostrarPresupuestos)
            listaPedidosOriginal = listaPedidos
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

    Private Sub CargarResumenSeleccionado()
        Dim parameters As NavigationParameters = New NavigationParameters()
        parameters.Add("resumenPedidoParameter", resumenSeleccionado)
        scopedRegionManager.RequestNavigate("DetallePedidoRegion", "DetallePedidoView", parameters)
        If Not IsNothing(resumenSeleccionado) Then
            empresaSeleccionada = resumenSeleccionado.empresa
        End If
    End Sub

    Private Function convertirCadenaInteger(texto As String) As Integer
        Dim valor As Integer
        Return IIf(Integer.TryParse(texto, valor), valor, Nothing)
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