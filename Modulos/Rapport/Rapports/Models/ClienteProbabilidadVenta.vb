Imports ControlesUsuario.Models

Public Class ClienteProbabilidadVenta
    Inherits ClienteDTO

    Public Property Probabilidad As Single
    Public Property DiasDesdeUltimoPedido As Integer
    Public Property DiasDesdeUltimaInteraccion As Integer

    Public ReadOnly Property DiasDesdeUltimoPedidoInteraccionTexto As String
        Get
            Return $"{DiasDesdeUltimoPedido} días desde el último pedido.{vbCr}{DiasDesdeUltimaInteraccion} días desde la última interacción."
        End Get
    End Property
End Class
