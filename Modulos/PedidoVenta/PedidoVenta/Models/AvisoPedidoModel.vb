Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#420: respuesta del PUT PedidosVenta cuando el servidor tiene avisos operativos
''' (RespuestaModificacionPedidoDTO de NestoAPI). Sin avisos el PUT devuelve 204 sin cuerpo.
''' </summary>
Public Class RespuestaModificacionPedidoModel
    Public Property Avisos As List(Of AvisoPedidoModel) = New List(Of AvisoPedidoModel)
End Class

''' <summary>
''' Un aviso del guardado. Datos es un JObject porque cada Tipo lleva su propia estructura
''' (p. ej. ReembolsoEnvioSinAjustar: Envio, Reembolso, ComisionQuitada, Ajustable).
''' Tipos desconocidos se ignoran sin romper (forward-compatible).
''' </summary>
Public Class AvisoPedidoModel
    Public Property Tipo As String
    Public Property Mensaje As String
    Public Property Datos As JObject
End Class
