''' <summary>
''' Nesto#562 (NestoAPI#723): cuerpo y respuesta de PUT api/Facturas/{empresa}/{numero}/SuPedido, que pone el P.O.
''' («Su pedido») en una factura ya emitida (y en su pedido) sin abonarla.
''' </summary>
Public Class SuPedidoFacturaModel
    Public Property Empresa As String
    Public Property Numero As String
    Public Property SuPedido As String
    Public Property SuPedidoAnterior As String
End Class

''' <summary>
''' Nesto#562 (NestoAPI#723): lo que se ve en Nesto de «el cliente exige el P.O.». La regla de verdad (grabar y facturar)
''' vive en NestoAPI; aquí solo se resalta «Su pedido» y se arma el texto del aviso.
''' </summary>
Public NotInheritable Class ReglaSuPedidoObligatorio
    Private Sub New()
    End Sub

    ''' <summary>Máximo de la columna SuPedido (el servidor rechaza con 400 los más largos).</summary>
    Public Const LONGITUD_MAXIMA As Integer = 50

    Public Const TEXTO_ETIQUETA_OBLIGATORIO As String = "Su Pedido / P.O. *:"
    Public Const TEXTO_ETIQUETA_NORMAL As String = "Su Pedido / P.O.:"
    Public Const TOOLTIP_OBLIGATORIO As String = "P.O. obligatorio: este cliente exige el P.O. en todos sus pedidos. Sin él no se puede grabar el pedido ni facturarlo."

    ''' <summary>Si falta el P.O. de un cliente que lo exige (en blanco cuenta como que falta).</summary>
    Public Shared Function Falta(exige As Boolean, suPedido As String) As Boolean
        Return exige AndAlso String.IsNullOrWhiteSpace(suPedido)
    End Function

    ''' <summary>El texto de la notificación después de poner el P.O. en una factura.</summary>
    Public Shared Function TextoPuesto(respuesta As SuPedidoFacturaModel, factura As String) As String
        Dim numero As String = If(String.IsNullOrWhiteSpace(respuesta?.Numero), factura?.Trim(), respuesta.Numero.Trim())
        Dim nuevo As String = respuesta?.SuPedido?.Trim()
        Dim anterior As String = respuesta?.SuPedidoAnterior?.Trim()
        Dim texto As String = $"P.O. {nuevo} puesto en la factura {numero}"
        texto &= If(String.IsNullOrWhiteSpace(anterior), " (antes no tenía).", $" (antes: {anterior}).")
        Return texto & " Si la vuelve a imprimir o a enviar, ya saldrá con él."
    End Function
End Class
