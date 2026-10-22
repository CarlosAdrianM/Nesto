Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts

''' <summary>Una opción (un botón) del diálogo de pedidos pendientes. Numero = 0 es «Hacer un pedido nuevo».</summary>
Public Class OpcionPedidoPendiente
    Public Property Numero As Integer
    Public Property Texto As String
End Class

''' <summary>
''' Novedad 735: al elegir un cliente con pedidos pendientes y la plantilla vacía, se pregunta si se quiere modificar
''' uno de ellos (así el cheque, los portes y Ganavisiones cuentan todo el pedido) o hacer uno nuevo. Un botón por
''' opción. Entra con «mensaje» (String) y «opciones» (List(Of OpcionPedidoPendiente)); sale con «pedido» (Integer,
''' 0 = pedido nuevo). Cerrar la ventana es lo mismo que «Hacer un pedido nuevo».
''' </summary>
Public Class ElegirPedidoPendienteDialogViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "ElegirPedidoPendienteDialog"
    Public Const PARAMETRO_MENSAJE As String = "mensaje"
    Public Const PARAMETRO_OPCIONES As String = "opciones"
    Public Const PARAMETRO_PEDIDO As String = "pedido"

    Public Sub New()
        Title = "Pedidos pendientes"
    End Sub

    Private _mensaje As String
    Public Property Mensaje As String
        Get
            Return _mensaje
        End Get
        Set(value As String)
            Dim unused = SetProperty(_mensaje, value)
        End Set
    End Property

    Private _opciones As List(Of OpcionPedidoPendiente) = New List(Of OpcionPedidoPendiente)
    Public Property Opciones As List(Of OpcionPedidoPendiente)
        Get
            Return _opciones
        End Get
        Set(value As List(Of OpcionPedidoPendiente))
            Dim unused = SetProperty(_opciones, If(value, New List(Of OpcionPedidoPendiente)))
        End Set
    End Property

    Private _elegirCommand As RelayCommand(Of OpcionPedidoPendiente)
    Public ReadOnly Property ElegirCommand As RelayCommand(Of OpcionPedidoPendiente)
        Get
            If _elegirCommand Is Nothing Then
                _elegirCommand = New RelayCommand(Of OpcionPedidoPendiente)(
                    Sub(opcion)
                        RaiseRequestClose(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {
                            {PARAMETRO_PEDIDO, If(opcion?.Numero, 0)}
                        }))
                    End Sub)
            End If
            Return _elegirCommand
        End Get
    End Property

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        If parameters.ContainsKey(PARAMETRO_MENSAJE) Then
            Mensaje = parameters.GetValue(Of String)(PARAMETRO_MENSAJE)
        End If
        If parameters.ContainsKey(PARAMETRO_OPCIONES) Then
            Opciones = parameters.GetValue(Of List(Of OpcionPedidoPendiente))(PARAMETRO_OPCIONES)
        End If
    End Sub
End Class
