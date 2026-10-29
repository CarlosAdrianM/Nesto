Imports System.Threading.Tasks
Imports Newtonsoft.Json.Linq

''' <summary>
''' NestoAPI#712 (pedido 929057: 27/10/2027 en vez de 2026): la API no graba porque alguna línea tiene la fecha de entrega
''' a más de 60 días y el pedido no venía confirmado. Código FECHA_ENTREGA_LEJANA; el mensaje ya trae la pregunta
''' («La fecha de entrega de las líneas es el miércoles 27/10/2027 (dentro de 365 días). ¿Es correcta?»).
''' Sí → se repite con ConfirmarFechaEntregaLejana = True; No → se vuelve a editar. Compartida por el detalle y la plantilla.
''' </summary>
Public Class FechaEntregaLejanaException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "FECHA_ENTREGA_LEJANA"
    Public Const TITULO As String = "Fecha de entrega lejana"

    Public Sub New(mensaje As String)
        MyBase.New(mensaje)
    End Sub

    ''' <summary>Si el error de la API es FECHA_ENTREGA_LEJANA, la excepción con su mensaje legible; si no, Nothing.</summary>
    Public Shared Function DesdeRespuesta(detallesError As JObject, mensajeLegible As String) As FechaEntregaLejanaException
        Dim errorObj As JObject = TryCast(detallesError?("error"), JObject)
        If errorObj?("code")?.ToString() <> CODIGO_ERROR Then
            Return Nothing
        End If
        Dim mensaje As String = errorObj("message")?.ToString()
        Return New FechaEntregaLejanaException(If(String.IsNullOrWhiteSpace(mensaje), mensajeLegible, mensaje))
    End Function

    ''' <summary>
    ''' El núcleo común del detalle y la plantilla. Graba con el aviso activado (ConfirmarFechaEntregaLejana = False si no
    ''' venía ya decidido); si la API contesta FECHA_ENTREGA_LEJANA, pregunta con su mensaje: Sí → marca la confirmación y
    ''' repite la misma llamada; No → <see cref="GuardadoCanceladoPorFechaEntregaException"/> (no se graba, se vuelve a editar).
    ''' Quien empieza un guardado nuevo pone antes el flag a False para que una confirmación anterior no valga para este.
    ''' </summary>
    Public Shared Async Function GuardarPreguntando(Of T)(pedido As PedidoVentaDTO, guardar As Func(Of Task(Of T)),
                                                          preguntar As Func(Of String, String, Task(Of Boolean))) As Task(Of T)
        If Not pedido.ConfirmarFechaEntregaLejana.HasValue Then
            pedido.ConfirmarFechaEntregaLejana = False
        End If
        Dim aviso As FechaEntregaLejanaException = Nothing
        Try
            Return Await guardar()
        Catch ex As FechaEntregaLejanaException
            aviso = ex ' en VB no se puede esperar el diálogo dentro del Catch
        End Try
        If Not Await preguntar(TITULO, aviso.Message) Then
            Throw New GuardadoCanceladoPorFechaEntregaException()
        End If
        pedido.ConfirmarFechaEntregaLejana = True
        Return Await guardar()
    End Function
End Class

''' <summary>NestoAPI#712: el usuario ha dicho que la fecha de entrega lejana NO es correcta; no se graba y se vuelve a editar.</summary>
Public Class GuardadoCanceladoPorFechaEntregaException
    Inherits Exception

    Public Sub New()
        MyBase.New("Revise la fecha de entrega de las líneas.")
    End Sub

    Protected Sub New(mensaje As String)
        MyBase.New(mensaje)
    End Sub
End Class
