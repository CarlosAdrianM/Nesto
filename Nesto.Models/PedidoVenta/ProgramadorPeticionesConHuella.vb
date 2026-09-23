Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' NestoAPI#517 / Nesto#484: pedir al servidor algo calculado sobre el pedido que se está editando (modo de
''' servicio, ofertas) sin poder inundarlo. El 23/09/26 las plantillas abiertas dejaron RDS2016 al 100 %:
''' <list type="bullet">
''' <item>Un único temporizador que se reprograma: cada cambio reinicia el reloj (no uno nuevo por cambio).</item>
''' <item>Huella: lo que se manda (JSON). Si es igual a la de la última petición, no se pide nada; con el
''' pedido quieto, ninguna respuesta puede provocar otra petición.</item>
''' <item>Una sola petición en vuelo: si llega otra mientras tanto, se anota y se reprograma al terminar.</item>
''' </list>
''' Los fallos no se propagan: esto es una ayuda y el pedido se tiene que poder guardar igual.
''' Lo usan la plantilla y el detalle de pedido.
''' </summary>
Public Class ProgramadorPeticionesConHuella
    Private ReadOnly _retardoMs As Integer
    Private ReadOnly _alVencer As Action
    Private _temporizador As Timer
    Private _ultimaHuella As String
    Private _enVuelo As Boolean
    Private _pendiente As Boolean

    ''' <param name="retardoMs">Respiro tras el último cambio antes de preguntar.</param>
    ''' <param name="alVencer">Qué hacer al vencer el reloj (normalmente: pasar al hilo de la interfaz y ejecutar).</param>
    Public Sub New(retardoMs As Integer, alVencer As Action)
        _retardoMs = retardoMs
        _alVencer = alVencer
    End Sub

    ''' <summary>Cuántas veces se ha preguntado de verdad al servidor (para los tests).</summary>
    Public ReadOnly Property PeticionesEnviadas As Integer

    ''' <summary>Programa (o reprograma) la petición tras el respiro.</summary>
    Public Sub Programar()
        If _temporizador Is Nothing Then
            _temporizador = New Timer(Sub(state) _alVencer?.Invoke(), Nothing, _retardoMs, Timeout.Infinite)
        Else
            _temporizador.Change(_retardoMs, Timeout.Infinite)
        End If
    End Sub

    ''' <summary>La próxima petición sale aunque la huella no cambie (p. ej. al abrir otro pedido).</summary>
    Public Sub OlvidarHuella()
        _ultimaHuella = Nothing
    End Sub

    ''' <summary>
    ''' Pregunta si hace falta. <paramref name="preparar"/> devuelve lo que se mandaría, o Nothing si no hay nada
    ''' que preguntar (entonces se llama a <paramref name="alNoHaberNada"/>). <paramref name="huellaDe"/> resume
    ''' lo que se manda; <paramref name="pedir"/> hace la llamada y aplica la respuesta.
    ''' </summary>
    Public Async Function EjecutarAsync(Of T As Class)(preparar As Func(Of T), huellaDe As Func(Of T, String),
                                                        alNoHaberNada As Action, pedir As Func(Of T, Task)) As Task
        If _enVuelo Then
            _pendiente = True ' ya hay una en camino; al terminar se vuelve a mirar (y solo se pide si cambió algo)
            Return
        End If
        Try
            Dim datos As T = preparar()
            Dim huella As String = If(datos Is Nothing, Nothing, huellaDe(datos))
            If huella Is Nothing Then
                _ultimaHuella = Nothing
                alNoHaberNada?.Invoke()
                Return
            End If
            If huella = _ultimaHuella Then
                Return ' lo mismo que la última vez: no se vuelve a preguntar
            End If
            _ultimaHuella = huella
            _enVuelo = True
            _PeticionesEnviadas += 1
            Await pedir(datos).ConfigureAwait(True)
        Catch ex As Exception
            ' Sin respuesta se sigue trabajando igual: no se molesta al usuario con esto.
        Finally
            _enVuelo = False
            If _pendiente Then
                _pendiente = False
                Programar()
            End If
        End Try
    End Function
End Class
