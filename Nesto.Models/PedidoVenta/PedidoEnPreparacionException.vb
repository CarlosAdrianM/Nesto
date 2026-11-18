Imports Newtonsoft.Json.Linq

''' <summary>
''' Caso 931007 (NestoAPI, Carlos 18/11/26): la API no guarda el pedido porque se le añaden productos cuando ya está EN PREPARACIÓN
''' (picking o albarán de hoy) y sale hoy. Código PEDIDO_EN_PREPARACION (409). Se elige: un pedido nuevo con lo añadido o, si la
''' agencia todavía no lo tiene, pedir a Almacén que lo meta en el envío de hoy. No se ha guardado nada.
''' </summary>
Public Class PedidoEnPreparacionException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "PEDIDO_EN_PREPARACION"
    Public Const OPCION_PEDIDO_NUEVO As String = "PedidoNuevo"
    Public Const OPCION_PEDIR_ALMACEN As String = "PedirAlmacen"

    Public Sub New(mensaje As String, Optional pickings As List(Of Integer) = Nothing, Optional envio As Integer? = Nothing,
                   Optional agenciaYaLoTiene As Boolean = False, Optional opciones As List(Of String) = Nothing)
        MyBase.New(mensaje)
        Me.Pickings = If(pickings, New List(Of Integer))
        Me.Envio = envio
        Me.AgenciaYaLoTiene = agenciaYaLoTiene
        Me.Opciones = If(opciones, New List(Of String) From {OPCION_PEDIDO_NUEVO})
    End Sub

    Public ReadOnly Property Pickings As List(Of Integer)
    Public ReadOnly Property Envio As Integer?
    Public ReadOnly Property AgenciaYaLoTiene As Boolean
    Public ReadOnly Property Opciones As List(Of String)

    ''' <summary>La API ofrece pedírselo a Almacén (la agencia no tiene el envío y su etiqueta no lleva reembolso).</summary>
    Public ReadOnly Property PuedePedirAlmacen As Boolean
        Get
            Return Opciones.Contains(OPCION_PEDIR_ALMACEN)
        End Get
    End Property

    ''' <summary>Si el error de la API es PEDIDO_EN_PREPARACION, la excepción con su mensaje y sus detalles; si no, Nothing.</summary>
    Public Shared Function DesdeRespuesta(detallesError As JObject, mensajeLegible As String) As PedidoEnPreparacionException
        Dim errorObj As JObject = TryCast(detallesError?("error"), JObject)
        If errorObj?("code")?.ToString() <> CODIGO_ERROR Then
            Return Nothing
        End If
        Dim mensaje As String = errorObj("message")?.ToString()
        Dim detalles As JObject = TryCast(errorObj("details"), JObject)
        Dim pickings As New List(Of Integer)
        Dim lista As JArray = TryCast(detalles?("Pickings"), JArray)
        If lista IsNot Nothing Then
            For Each token As JToken In lista
                Dim numero As Integer
                If Integer.TryParse(token.ToString(), numero) Then
                    pickings.Add(numero)
                End If
            Next
        End If
        Dim envio As Integer? = Nothing
        Dim envioToken As JToken = detalles?("Envio")
        Dim numeroEnvio As Integer
        If envioToken IsNot Nothing AndAlso envioToken.Type <> JTokenType.Null AndAlso Integer.TryParse(envioToken.ToString(), numeroEnvio) Then
            envio = numeroEnvio
        End If
        Dim agencia As Boolean = detalles?("AgenciaYaLoTiene")?.Type = JTokenType.Boolean AndAlso detalles("AgenciaYaLoTiene").Value(Of Boolean)()
        Dim opciones As New List(Of String)
        Dim listaOpciones As JArray = TryCast(detalles?("Opciones"), JArray)
        If listaOpciones IsNot Nothing Then
            opciones.AddRange(listaOpciones.Select(Function(o) o.ToString()))
        End If
        If opciones.Count = 0 Then
            opciones.Add(OPCION_PEDIDO_NUEVO)
        End If
        Return New PedidoEnPreparacionException(If(String.IsNullOrWhiteSpace(mensaje), mensajeLegible, mensaje), pickings, envio, agencia, opciones)
    End Function
End Class

''' <summary>Caso 931007: ha cancelado la pregunta del pedido en preparación; no se ha guardado nada y sigue editando.</summary>
Public Class GuardadoCanceladoPorAmpliacionException
    Inherits GuardadoCanceladoPorFechaEntregaException

    Public Sub New()
        MyBase.New("No se ha guardado nada.")
    End Sub
End Class

''' <summary>
''' Caso 931007: guarda y, si la API contesta PEDIDO_EN_PREPARACION, pregunta qué hacer con lo añadido (<paramref name="elegir"/>
''' devuelve «PedidoNuevo», «PedirAlmacen» o Nothing para cancelar) y vuelve a guardar con la elección. La elección se queda en el
''' pedido hasta el siguiente guardado por aquí (que la borra al empezar): así el reintento «¿de todos modos?» de la validación de
''' precios, que va directo, la lleva. Sin <paramref name="elegir"/>, la excepción sigue su camino.
''' </summary>
Public Module AmpliacionEnPreparacionPreguntando
    Public Async Function GuardarPreguntando(Of T)(pedido As PedidoVentaDTO, guardar As Func(Of Task(Of T)),
                                                  elegir As Func(Of PedidoEnPreparacionException, String)) As Task(Of T)
        pedido.AmpliacionEnPreparacion = Nothing
        Dim rechazo As PedidoEnPreparacionException = Nothing
        Try
            Return Await guardar()
        Catch ex As PedidoEnPreparacionException When elegir IsNot Nothing
            rechazo = ex ' en VB no se puede esperar el diálogo dentro del Catch
        End Try
        Dim opcion As String = elegir(rechazo)
        If String.IsNullOrWhiteSpace(opcion) Then
            Throw New GuardadoCanceladoPorAmpliacionException()
        End If
        pedido.AmpliacionEnPreparacion = opcion
        Return Await guardar()
    End Function

    ''' <summary>El pedido nuevo que ha creado la API con lo añadido (aviso «AmpliacionEnPedidoNuevo», Datos.PedidoNuevo), o Nothing.</summary>
    Public Function PedidoNuevoDeLosAvisos(avisos As IEnumerable(Of (Tipo As String, Datos As JObject))) As Integer?
        For Each aviso In If(avisos, Enumerable.Empty(Of (Tipo As String, Datos As JObject))())
            If aviso.Tipo = TIPO_AVISO_PEDIDO_NUEVO Then
                Dim numero As Integer
                If Integer.TryParse(aviso.Datos?("PedidoNuevo")?.ToString(), numero) AndAlso numero > 0 Then
                    Return numero
                End If
            End If
        Next
        Return Nothing
    End Function

    ''' <summary>Tipo del aviso de la respuesta del PUT cuando lo añadido ha ido a un pedido nuevo.</summary>
    Public Const TIPO_AVISO_PEDIDO_NUEVO As String = "AmpliacionEnPedidoNuevo"
    ''' <summary>Tipo del aviso de la respuesta del PUT cuando se ha pedido a Almacén.</summary>
    Public Const TIPO_AVISO_PEDIDO_A_ALMACEN As String = "AmpliacionPedidaAAlmacen"
End Module
