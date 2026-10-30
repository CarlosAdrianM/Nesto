''' <summary>
''' Nesto#581 / NestoAPI#760: GET api/NotasEntrega/Recogidas/{codigo}. La nota de recogida de una etiqueta NR{Pedido}-{N}
''' (o NR{Pedido}), para enseñarla antes de crear el abono.
''' </summary>
Public Class RecogidaDTO
    Public Property Empresa As String
    Public Property PedidoNota As Integer
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Nombre As String
    Public Property TextoNota As String
    Public Property Bultos As Integer
    Public Property FacturaOriginal As String
    ''' <summary>"Tramitado", "Incidentado", "Entregado" (= recogido) o Nothing (sin envío).</summary>
    Public Property EstadoEnvio As String
    Public Property FechaRecogida As Date?
    ''' <summary>Quién la entregó al repartidor (solo Almacén, Administración y Dirección; a los demás les llega vacío).</summary>
    Public Property Receptor As String
    Public Property PedidoAbono As Integer?
End Class

''' <summary>Nesto#581 / NestoAPI#760: la respuesta de POST api/NotasEntrega/Recogidas/Abono (201 creado, 200 si ya existía).</summary>
Public Class AbonoRecogidaDTO
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Nombre As String
    Public Property Serie As String
    Public Property PedidoNota As Integer
    Public Property FacturaOriginal As String
    Public Property TextoNota As String
    Public Property YaExistia As Boolean
End Class

''' <summary>
''' Nesto#581: la API contesta 409 RECOGIDA_NO_RECOGIDA porque el repartidor no ha marcado la recogida como hecha. Nesto
''' pregunta y, si el usuario dice que sí, repite con Forzar = True.
''' </summary>
Public Class RecogidaNoRecogidaException
    Inherits Exception

    Public Const CODIGO As String = "RECOGIDA_NO_RECOGIDA"
    Public Const ESTADO_SIN_ENVIO As String = "SinEnvio"

    Public Sub New(mensaje As String, estado As String)
        MyBase.New(mensaje)
        Me.Estado = estado
    End Sub

    ''' <summary>"Tramitado", "Incidentado" o "SinEnvio" (details.estado).</summary>
    Public ReadOnly Property Estado As String

    ''' <summary>El estado para la pregunta al usuario.</summary>
    Public ReadOnly Property EstadoLegible As String
        Get
            If String.IsNullOrWhiteSpace(Estado) OrElse Estado = ESTADO_SIN_ENVIO Then
                Return "sin envío de reparto propio"
            End If
            Return "estado " & Estado
        End Get
    End Property
End Class
