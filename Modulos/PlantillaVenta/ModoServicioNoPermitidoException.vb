''' <summary>
''' NestoAPI#518: la API rechaza el pedido porque el modo de servicio elegido ya no tiene sentido (código
''' MODO_SERVICIO_NO_PERMITIDO). El mensaje es accionable y trae el modo que sí vale, para preseleccionarlo.
''' </summary>
Public Class ModoServicioNoPermitidoException
    Inherits Exception

    Public Sub New(mensaje As String, modoSugerido As Byte?)
        MyBase.New(mensaje)
        Me.ModoSugerido = modoSugerido
    End Sub

    Public ReadOnly Property ModoSugerido As Byte?
End Class
