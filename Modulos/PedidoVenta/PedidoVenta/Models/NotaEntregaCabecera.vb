''' <summary>
''' Nesto#579 / NestoAPI#757: cabecera de una nota de entrega (tabla NotasEntregaCabecera). La devuelven
''' GET api/NotasEntrega/{empresa}/{pedido} y PUT api/NotasEntrega/{empresa}/{pedido}/Cabecera.
''' Sin fila en BD (notas de Nesto viejo) la API dice Entrega y 1 bulto.
''' </summary>
Public Class NotaEntregaCabeceraDTO
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property Tipo As String
    Public Property Bultos As Integer
    Public Property FacturaOriginal As String
    Public Property PedidoAbono As Integer?
    Public Property NumeroNota As Integer?
    Public Property Procesada As Boolean
    Public Property EnvioId As Integer?
    Public Property EstadoEnvio As String
    Public Property CodigoDocumento As String
End Class

''' <summary>
''' Nesto#580 / NestoAPI#758: respuesta de POST api/NotasEntrega/{empresa}/{pedido}/EtiquetasRecogida
''' (las etiquetas NR{Pedido}-{N} que la API ha mandado a la Zebra).
''' </summary>
Public Class EtiquetasRecogidaRespuestaDTO
    Public Property Impresas As Integer
    Public Property Codigos As List(Of String) = New List(Of String)()
End Class

''' <summary>Nesto#579: los tipos de nota de entrega (los mismos textos que guarda la API).</summary>
Public NotInheritable Class TiposNotaEntrega
    Public Const ENTREGA As String = "Entrega"
    Public Const RECOGIDA As String = "Recogida"
    Public Shared ReadOnly Todos As IReadOnlyList(Of String) = New List(Of String) From {ENTREGA, RECOGIDA}

    Private Sub New()
    End Sub

    ''' <summary>El tipo tal cual lo guarda la API; vacío o desconocido = Entrega (las notas de Nesto viejo).</summary>
    Public Shared Function Normalizar(tipo As String) As String
        Return If(String.Equals(tipo?.Trim(), RECOGIDA, StringComparison.OrdinalIgnoreCase), RECOGIDA, ENTREGA)
    End Function
End Class
