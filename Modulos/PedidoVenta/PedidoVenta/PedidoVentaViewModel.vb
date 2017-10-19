﻿Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports System.Net.Http
Imports System.Text
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports Nesto.Contratos
Imports Nesto.Models.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Newtonsoft.Json
Imports Unity

Public Class PedidoVentaViewModel
    Inherits ViewModelBase

    Private regionManager As IRegionManager
    Private ReadOnly container As IUnityContainer
    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IPedidoVentaService

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IPedidoVentaService, container As IUnityContainer)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.container = container
        Me.servicio = servicio

        cmdAbrirModulo = New DelegateCommand(Of Object)(AddressOf OnAbrirModulo, AddressOf CanAbrirModulo)

        Titulo = "Lista de Pedidos"
    End Sub

    Public Property empresaInicial As String
    Public Property pedidoInicial As Integer

    Private _scopedRegionManager As IRegionManager
    Public Property scopedRegionManager As IRegionManager
        Get
            Return _scopedRegionManager
        End Get
        Set(value As IRegionManager)
            SetProperty(_scopedRegionManager, value)
        End Set
    End Property

#Region "Comandos"
    Private _cmdAbrirModulo As DelegateCommand(Of Object)
    Public Property cmdAbrirModulo As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirModulo
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdAbrirModulo, value)
        End Set
    End Property
    Private Function CanAbrirModulo(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirModulo(arg As Object)
        Dim view = Me.container.Resolve(Of PedidoVentaView)
        If Not IsNothing(view) Then
            Dim region = regionManager.Regions("MainRegion")
            scopedRegionManager = region.Add(view, Nothing, True)
            view.scopedRegionManager = scopedRegionManager
            region.Activate(view)
        End If
    End Sub

#End Region

    Public Shared Sub cargarPedido(empresa As String, pedido As Integer, container As IUnityContainer)
        Dim view = container.Resolve(Of PedidoVentaView)
        Dim regionManager = container.Resolve(Of IRegionManager)
        If Not IsNothing(view) Then
            Dim region = regionManager.Regions("MainRegion")
            regionManager = region.Add(view, Nothing, True)
            view.scopedRegionManager = regionManager
            view.DataContext.empresaInicial = empresa
            view.DataContext.pedidoInicial = pedido
            region.Activate(view)
        End If
    End Sub

    Public Shared Async Function CrearPedidoAsync(pedido As PedidoVentaDTO, configuracion As IConfiguracion) As Task(Of String)
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json")

            Try

                response = Await client.PostAsync("PedidosVenta", content)

                If response.IsSuccessStatusCode Then
                    Dim pathNumeroPedido = response.Headers.Location.LocalPath
                    Dim numPedido As String = pathNumeroPedido.Substring(pathNumeroPedido.LastIndexOf("/") + 1)
                    Return "Pedido " + numPedido + " creado correctamente"
                Else
                    Dim respuestaError As String = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As String = JsonConvert.DeserializeObject(Of String)(respuestaError)
                    Return detallesError
                End If
            Catch ex As Exception
                Return ex.Message
            Finally

            End Try

        End Using

    End Function


End Class
