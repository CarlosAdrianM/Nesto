''' <summary>
''' Nesto#468: una fila de GET api/EnviosAgencias/Retrasados (NestoAPI#173): un envío que salió
''' hace días y que la agencia todavía no ha dado por entregado. Es el POCO que llega por la red;
''' la pestaña Retrasados lo enlaza directamente (no hace falta convertirlo a EnviosAgencia).
''' </summary>
Public Class EnvioRetrasadoModel

    Public Property Numero As Integer
    Public Property Empresa As String
    Public Property Pedido As Integer?
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Nombre As String
    Public Property Agencia As Integer
    Public Property NombreAgencia As String
    Public Property CodigoBarras As String
    Public Property Fecha As Date?

    ''' <summary>Días naturales desde que salió. Los calcula el servidor.</summary>
    Public Property DiasTranscurridos As Integer

    ''' <summary>El último evento que contó la agencia ("REPARTO", "DOCUMENTADO"...). Es la columna
    ''' que más dice de un vistazo.</summary>
    Public Property DetalleEstado As String

    Public Property Poblacion As String
    Public Property CodPostal As String
    Public Property Telefono As String
    Public Property Movil As String
    Public Property Email As String
    Public Property Observaciones As String
    Public Property Vendedor As String

    ''' <summary>
    ''' Tramo de gravedad para colorear la rejilla: 0 = hasta 5 días, 1 = de 6 a 10, 2 = más de 10.
    ''' Calibrado con los dos casos reales de la issue (19 días en REPARTO = perdido).
    ''' </summary>
    Public ReadOnly Property Tramo As Integer
        Get
            If DiasTranscurridos > 10 Then
                Return 2
            End If
            If DiasTranscurridos > 5 Then
                Return 1
            End If
            Return 0
        End Get
    End Property

End Class
