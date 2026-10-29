Imports System.Threading.Tasks
Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#567 (NestoAPI#732): la API no guarda (409) porque el pedido justifica alguna inscripción gratuita por compra y el
''' cambio le quita los productos requeridos (o porque se borra el pedido). Código PEDIDO_JUSTIFICA_INSCRIPCIONES_EVENTO; el
''' mensaje ya trae la pregunta, de usted, con la lista de inscripciones («… Si quita del pedido 930001 esa línea, se anulará
''' la inscripción y se avisará a la tienda del evento. ¿Quiere seguir?»). Sí → se repite con
''' ConfirmarAnularInscripcionesEventos = True (PUT) o con «anularInscripcionesEventos=true» (DELETE); No → no se guarda.
''' Mismo patrón que <see cref="FechaEntregaLejanaException"/> (NestoAPI#712).
''' </summary>
Public Class InscripcionesEventoAnularException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "PEDIDO_JUSTIFICA_INSCRIPCIONES_EVENTO"
    Public Const TITULO As String = "Inscripciones a eventos"

    Public Sub New(mensaje As String)
        MyBase.New(mensaje)
    End Sub

    ''' <summary>
    ''' Si el error de la API es PEDIDO_JUSTIFICA_INSCRIPCIONES_EVENTO, la excepción con su pregunta; si no, Nothing. Si la
    ''' pregunta no nombra las inscripciones de error.details.inscripciones, se añaden debajo.
    ''' </summary>
    Public Shared Function DesdeRespuesta(detallesError As JObject, mensajeLegible As String) As InscripcionesEventoAnularException
        Dim errorObj As JObject = TryCast(detallesError?("error"), JObject)
        If errorObj?("code")?.ToString() <> CODIGO_ERROR Then
            Return Nothing
        End If
        Dim mensaje As String = errorObj("message")?.ToString()
        If String.IsNullOrWhiteSpace(mensaje) Then
            mensaje = Nothing
        End If
        Dim lineas As New List(Of String)
        Dim inscripciones As JArray = TryCast(TryCast(errorObj("details"), JObject)?("inscripciones"), JArray)
        If inscripciones IsNot Nothing Then
            For Each inscripcion As JToken In inscripciones
                Dim linea As String = TextoInscripcion(inscripcion)
                If Not String.IsNullOrWhiteSpace(linea) AndAlso (mensaje Is Nothing OrElse Not mensaje.Contains(If(inscripcion("Evento")?.ToString(), linea))) Then
                    lineas.Add("- " & linea)
                End If
            Next
        End If
        If lineas.Any() Then
            mensaje = If(String.IsNullOrWhiteSpace(mensaje), "El pedido justifica estas inscripciones gratuitas por compra. ¿Quiere seguir y anularlas?", mensaje) &
                Environment.NewLine & Environment.NewLine & String.Join(Environment.NewLine, lineas)
        End If
        Return New InscripcionesEventoAnularException(If(mensaje, mensajeLegible))
    End Function

    ''' <summary>«15191/0 (LUNA) en «Formación por compra Lifting Coreano» del 25/11/2026».</summary>
    Public Shared Function TextoInscripcion(inscripcion As JToken) As String
        If inscripcion Is Nothing OrElse inscripcion.Type <> JTokenType.Object Then
            Return Nothing
        End If
        Dim cliente As String = inscripcion("Cliente")?.ToString()?.Trim()
        Dim contacto As String = inscripcion("Contacto")?.ToString()?.Trim()
        Dim nombre As String = inscripcion("Nombre")?.ToString()?.Trim()
        Dim evento As String = inscripcion("Evento")?.ToString()?.Trim()
        Dim fecha As Date? = Nothing
        Dim fechaToken As JToken = inscripcion("FechaEvento")
        If fechaToken IsNot Nothing AndAlso fechaToken.Type = JTokenType.Date Then
            fecha = fechaToken.ToObject(Of Date)()
        ElseIf fechaToken IsNot Nothing Then
            Dim leida As Date
            If Date.TryParse(fechaToken.ToString(), Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, leida) Then
                fecha = leida
            End If
        End If
        Dim quien As String = If(String.IsNullOrEmpty(contacto), cliente, $"{cliente}/{contacto}") &
            If(String.IsNullOrEmpty(nombre), String.Empty, $" ({nombre})")
        Return quien & If(String.IsNullOrEmpty(evento), String.Empty, $" en «{evento}»") &
            If(fecha.HasValue, $" del {fecha.Value:dd/MM/yyyy}", String.Empty)
    End Function

    ''' <summary>
    ''' Guarda con el aviso activado; si la API contesta PEDIDO_JUSTIFICA_INSCRIPCIONES_EVENTO, pregunta con su mensaje (con
    ''' «No» por defecto): Sí → marca la confirmación y repite la misma llamada; No →
    ''' <see cref="GuardadoCanceladoPorInscripcionesEventoException"/> (no se guarda, se sigue editando).
    ''' </summary>
    Public Shared Async Function GuardarPreguntando(Of T)(pedido As PedidoVentaDTO, guardar As Func(Of Task(Of T)),
                                                          preguntar As Func(Of String, String, Task(Of Boolean))) As Task(Of T)
        If Not pedido.ConfirmarAnularInscripcionesEventos.HasValue Then
            pedido.ConfirmarAnularInscripcionesEventos = False
        End If
        Dim aviso As InscripcionesEventoAnularException = Nothing
        Try
            Return Await guardar()
        Catch ex As InscripcionesEventoAnularException
            aviso = ex ' en VB no se puede esperar el diálogo dentro del Catch
        End Try
        If Not Await preguntar(TITULO, aviso.Message) Then
            Throw New GuardadoCanceladoPorInscripcionesEventoException()
        End If
        pedido.ConfirmarAnularInscripcionesEventos = True
        Return Await guardar()
    End Function
End Class

''' <summary>
''' Nesto#567: el usuario ha dicho que NO quiere anular las inscripciones que justifica el pedido; no se guarda y se sigue
''' editando. Hereda de <see cref="GuardadoCanceladoPorFechaEntregaException"/> para que los guardados que ya tratan esa
''' cancelación (volver a editar sin más mensajes) traten igual esta.
''' </summary>
Public Class GuardadoCanceladoPorInscripcionesEventoException
    Inherits GuardadoCanceladoPorFechaEntregaException

    Public Sub New()
        MyBase.New("No se ha guardado: el pedido sigue justificando las inscripciones a eventos.")
    End Sub
End Class

''' <summary>
''' Nesto#567 (NestoAPI#712 y #732): los dos avisos con confirmación al guardar un pedido (fecha de entrega lejana e
''' inscripciones a eventos que justifica). Cada guardado nuevo vuelve a preguntar.
''' </summary>
Public Module GuardadoPedidoConAvisos
    Public Async Function GuardarPreguntando(Of T)(pedido As PedidoVentaDTO, guardar As Func(Of Task(Of T)),
                                                  preguntar As Func(Of String, String, Task(Of Boolean))) As Task(Of T)
        pedido.ConfirmarAnularInscripcionesEventos = False
        Return Await FechaEntregaLejanaException.GuardarPreguntando(pedido,
            Function() InscripcionesEventoAnularException.GuardarPreguntando(pedido, guardar, preguntar), preguntar)
    End Function
End Module
