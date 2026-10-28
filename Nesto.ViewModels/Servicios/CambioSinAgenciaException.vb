Imports Newtonsoft.Json.Linq

''' <summary>
''' NestoAPI#725 / Nesto#564 (Carlos, 28/10/26, envío 249990): el PUT de un envío ya registrado en CTT/Innovatrans ha devuelto
''' 409 CAMBIO_SIN_AGENCIA porque cambia datos que viajaron a la agencia (<see cref="Campos"/>). Si se guarda, el cambio se
''' hace en nuestro sistema pero NO en el de la agencia: solo con su confirmación por teléfono
''' (<see cref="IAgenciaService.ModificarConfirmadoConLaAgencia"/>). Nada se ha guardado.
''' </summary>
Public Class CambioSinAgenciaException
    Inherits Exception

    Public Const CODIGO As String = "CAMBIO_SIN_AGENCIA"

    Public ReadOnly Property NumeroEnvio As Integer
    Public ReadOnly Property Campos As IReadOnlyList(Of String)

    Public Sub New(numeroEnvio As Integer, campos As IEnumerable(Of String), mensaje As String)
        MyBase.New(mensaje)
        Me.NumeroEnvio = numeroEnvio
        Me.Campos = If(campos, Enumerable.Empty(Of String)()).ToList()
    End Sub

    ''' <summary>
    ''' La excepción si la respuesta es el 409 con Codigo = CAMBIO_SIN_AGENCIA ({ Codigo, Campos, Mensaje, Message });
    ''' Nothing en cualquier otro caso (otro estado, otro código o un cuerpo que no es JSON).
    ''' </summary>
    Public Shared Function DesdeRespuesta(numeroEnvio As Integer, codigoEstado As Integer, cuerpo As String) As CambioSinAgenciaException
        If codigoEstado <> 409 OrElse String.IsNullOrWhiteSpace(cuerpo) Then
            Return Nothing
        End If
        Dim json As JObject
        Try
            json = JObject.Parse(cuerpo)
        Catch
            Return Nothing
        End Try
        If json.Value(Of String)("Codigo") <> CODIGO Then
            Return Nothing
        End If
        Dim campos As New List(Of String)
        Dim lista = TryCast(json("Campos"), JArray)
        If lista IsNot Nothing Then
            campos.AddRange(lista.Where(Function(t) t.Type = JTokenType.String).Select(Function(t) t.Value(Of String)()))
        End If
        Dim mensaje As String = If(json.Value(Of String)("Mensaje"), json.Value(Of String)("Message"))
        Return New CambioSinAgenciaException(numeroEnvio, campos, mensaje)
    End Function
End Class
