Imports ControlesUsuario.Dialogs
Imports Nesto.Contratos
Imports Nesto.Models.PedidoVenta
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Mvvm
Imports Prism.Services.Dialogs

Public Class PickingPopupViewModel
    Inherits BindableBase
    Implements IDialogAware

    Private ReadOnly servicio As IPedidoVentaService
    Private ReadOnly eventAggregator As IEventAggregator
    Private ReadOnly dialogService As IDialogService


    Public Sub New(servicio As IPedidoVentaService, eventAggregator As IEventAggregator, dialogService As IDialogService)
        Me.servicio = servicio
        Me.eventAggregator = eventAggregator
        Me.dialogService = dialogService

        cmdSacarPicking = New DelegateCommand(Of PedidoVentaDTO)(AddressOf OnSacarPicking)
    End Sub



    Public ReadOnly Property Title As String Implements IDialogAware.Title
        Get
            Return "Picking"
        End Get
    End Property

    Public Event RequestClose As Action(Of IDialogResult) Implements IDialogAware.RequestClose

    Public Sub OnDialogClosed() Implements IDialogAware.OnDialogClosed

    End Sub

    Public Sub OnDialogOpened(parameters As IDialogParameters) Implements IDialogAware.OnDialogOpened
        pedidoPicking = parameters.GetValue(Of PedidoVentaDTO)("pedidoPicking")
        If Not IsNothing(pedidoPicking) Then
            numeroPedidoPicking = pedidoPicking.numero
            numeroClientePicking = pedidoPicking.cliente
        Else
            numeroPedidoPicking = 0
        End If
    End Sub

    Public Function CanCloseDialog() As Boolean Implements IDialogAware.CanCloseDialog
        Return True
    End Function




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

    Private _numeroClientePicking As String
    Public Property numeroClientePicking() As String
        Get
            Return _numeroClientePicking
        End Get
        Set(ByVal value As String)
            SetProperty(_numeroClientePicking, value)
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

    Private _pedidoPicking As PedidoVentaDTO
    Public Property pedidoPicking As PedidoVentaDTO
        Get
            Return _pedidoPicking
        End Get
        Set(value As PedidoVentaDTO)
            SetProperty(_pedidoPicking, value)
        End Set
    End Property






    Private _cmdSacarPicking As DelegateCommand(Of PedidoVentaDTO)
    Public Property cmdSacarPicking As DelegateCommand(Of PedidoVentaDTO)
        Get
            Return _cmdSacarPicking
        End Get
        Private Set(value As DelegateCommand(Of PedidoVentaDTO))
            SetProperty(_cmdSacarPicking, value)
        End Set
    End Property
    Private Async Sub OnSacarPicking(pedidoPicking As PedidoVentaDTO)
        Try
            estaSacandoPicking = True
            Await Task.Run(Sub()
                               Try
                                   If esPickingPedido Then
                                       Dim empresaPicking As String
                                       If Not IsNothing(pedidoPicking) Then
                                           empresaPicking = pedidoPicking.empresa
                                       Else
                                           empresaPicking = Constantes.Empresas.EMPRESA_DEFECTO
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
            dialogService.ShowNotification("Picking", textoMensaje)
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
            dialogService.ShowNotification(tituloError, textoError)
        Finally
            estaSacandoPicking = False
        End Try
    End Sub

End Class
