''' <summary>
''' NestoAPI#590 (slice «Quitar picking», 09/11/26): lo que un mozo ya ha leído en el picking de los productos del pedido que
''' se quita (lo que tiene que apartar para el pedido).
''' </summary>
Public Class LecturaQuitarPicking
    Public Property Picking As Integer
    Public Property Usuario As String
    Public Property Dispositivo As String
    Public Property Producto As String
    Public Property Nombre As String
    Public Property Cantidad As Integer
End Class

''' <summary>
''' NestoAPI#590: la respuesta de POST api/Picking/{empresa}/{pedido}/QuitarPicking. <see cref="NecesitaConfirmar"/> es el 409
''' con lecturas: no se ha tocado nada y hay que preguntar al usuario.
''' </summary>
Public Class ResultadoQuitarPicking
    Public Property Message As String
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property Pickings As New List(Of Integer)
    Public Property Lineas As Integer
    Public Property Lecturas As New List(Of LecturaQuitarPicking)
    Public Property NecesitaConfirmar As Boolean
End Class

''' <summary>
''' Novedad 1146 (Alfredo, 10/11/26: «lo más común es quitar alguna línea, por ejemplo, los portes»): una línea del pedido con
''' picking, de GET api/Picking/{empresa}/{pedido}/LineasConPicking. <see cref="Marcado"/> lo decide quien quita el picking.
''' </summary>
Public Class LineaConPicking
    Inherits CommunityToolkit.Mvvm.ComponentModel.ObservableObject

    ''' <summary>Nº Orden de LinPedidoVta.</summary>
    Public Property Linea As Integer
    Public Property Picking As Integer
    Public Property Estado As Short
    Public Property TipoLinea As Short
    Public Property Producto As String
    Public Property Texto As String
    Public Property Cantidad As Integer
    Public Property SePuedeQuitar As Boolean
    ''' <summary>Por qué no se puede quitar (vacío si se puede).</summary>
    Public Property Motivo As String

    Private _marcado As Boolean
    Public Property Marcado As Boolean
        Get
            Return _marcado
        End Get
        Set(value As Boolean)
            SetProperty(_marcado, value AndAlso SePuedeQuitar)
        End Set
    End Property
End Class
