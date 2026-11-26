Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#642 (NestoAPI#873, pedido 929555): la API no guarda el pedido porque alguien lo ha cambiado desde que se abrió (la
''' Version que manda Nesto ya no es la del pedido guardado). Código PEDIDO_CAMBIADO; el mensaje ya dice quién y cuándo
''' («El pedido 929555 lo ha modificado Aida a las 15:14 desde que usted lo abrió. Recárguelo y vuelva a hacer los cambios.»).
''' No se ha guardado nada.
''' </summary>
Public Class PedidoCambiadoException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "PEDIDO_CAMBIADO"

    Public Sub New(mensaje As String, Optional usuario As String = Nothing, Optional fecha As Date? = Nothing,
                   Optional pickings As List(Of Integer) = Nothing, Optional huellaContenido As String = Nothing)
        MyBase.New(mensaje)
        Me.Usuario = usuario
        Me.Fecha = fecha
        Me.Pickings = pickings
        Me.HuellaContenido = huellaContenido
    End Sub

    ''' <summary>Carlos 26/11/26: los pickings del pedido de ahora (Nothing con la API antigua).</summary>
    Public ReadOnly Property Pickings As List(Of Integer)
    ''' <summary>Carlos 26/11/26: la huella del contenido de ahora (Nothing con la API antigua). Igual a la que se tenía = solo ha cambiado el picking.</summary>
    Public ReadOnly Property HuellaContenido As String

    ''' <summary>Quién lo tocó el último (sin dominio), si la API lo sabe.</summary>
    Public ReadOnly Property Usuario As String
    ''' <summary>Cuándo, si la API lo sabe.</summary>
    Public ReadOnly Property Fecha As Date?

    ''' <summary>Si el error de la API es PEDIDO_CAMBIADO, la excepción con su mensaje legible; si no, Nothing.</summary>
    Public Shared Function DesdeRespuesta(detallesError As JObject, mensajeLegible As String) As PedidoCambiadoException
        Dim errorObj As JObject = TryCast(detallesError?("error"), JObject)
        If errorObj?("code")?.ToString() <> CODIGO_ERROR Then
            Return Nothing
        End If
        ' El mensaje limpio de la API (sin el «[PEDIDO_CAMBIADO]» que antepone HttpErrorHelper).
        Dim mensaje As String = errorObj("message")?.ToString()
        Dim detalles As JObject = TryCast(errorObj("details"), JObject)
        Dim usuario As String = detalles?("Usuario")?.ToString()
        Dim fecha As Date? = Nothing
        Dim fechaToken As JToken = detalles?("Fecha")
        If fechaToken IsNot Nothing AndAlso fechaToken.Type = JTokenType.Date Then
            fecha = fechaToken.Value(Of Date)()
        End If
        Dim pickings As List(Of Integer) = Nothing
        Dim pickingsToken As JArray = TryCast(detalles?("Pickings"), JArray)
        If pickingsToken IsNot Nothing Then
            pickings = New List(Of Integer)
            For Each p As JToken In pickingsToken
                If p.Type = JTokenType.Integer Then
                    pickings.Add(p.Value(Of Integer)())
                End If
            Next
        End If
        Dim huella As String = detalles?("HuellaContenido")?.ToString()
        Return New PedidoCambiadoException(If(String.IsNullOrWhiteSpace(mensaje), mensajeLegible, mensaje), usuario, fecha, pickings,
                                           If(String.IsNullOrWhiteSpace(huella), Nothing, huella))
    End Function
End Class
