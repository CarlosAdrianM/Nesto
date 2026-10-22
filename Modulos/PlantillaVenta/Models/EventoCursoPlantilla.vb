Imports System.Globalization
Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#543 / NestoAPI#691: un evento futuro de un curso (GET api/Eventos/PorProducto), con las modalidades que se
''' pueden vender. La plantilla lo enseña en el diálogo de elegir evento y modalidad.
''' </summary>
Public Class EventoCursoDTO
    Public Property Id As Integer
    Public Property Titulo As String
    Public Property Fecha As DateTime
    Public Property HoraInicio As String
    Public Property Almacen As String
    Public Property Formato As String
    Public Property PlazasLibres As Integer
    Public Property Modalidades As List(Of ModalidadEventoDTO) = New List(Of ModalidadEventoDTO)

    Public Const FORMATO_ONLINE As String = "Online"

    ''' <summary>La tienda del evento con su nombre (REI = Reina, ALC = Alcobendas).</summary>
    Public ReadOnly Property NombreTienda As String
        Get
            Return EventoCursoDTO.NombreDeTienda(Almacen)
        End Get
    End Property

    Public ReadOnly Property EsOnline As Boolean
        Get
            Return String.Equals(Formato?.Trim(), FORMATO_ONLINE, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    ''' <summary>«Online» o «Presencial», tal y como se le dice al vendedor.</summary>
    Public ReadOnly Property TextoFormato As String
        Get
            Return If(EsOnline, "Online", "Presencial")
        End Get
    End Property

    ''' <summary>Fecha y hora: «10/11/2026 10:00» (sin hora si la API no la trae).</summary>
    Public ReadOnly Property TextoFechaHora As String
        Get
            Dim texto As String = Fecha.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("es-ES"))
            If Not String.IsNullOrWhiteSpace(HoraInicio) Then
                texto &= " " & HoraInicio.Trim()
            End If
            Return texto
        End Get
    End Property

    Public ReadOnly Property TextoPlazas As String
        Get
            Return If(PlazasLibres = 1, "1 plaza libre", $"{PlazasLibres} plazas libres")
        End Get
    End Property

    ''' <summary>Lo que se ve en la línea del pedido: «10/11/2026 10:00 · Reina · Presencial».</summary>
    Public ReadOnly Property TextoEvento As String
        Get
            Return $"{TextoFechaHora} · {NombreTienda} · {TextoFormato}"
        End Get
    End Property

    Public Shared Function NombreDeTienda(almacen As String) As String
        Select Case almacen?.Trim().ToUpperInvariant()
            Case "REI"
                Return "Reina"
            Case "ALC"
                Return "Alcobendas"
            Case Else
                Return If(almacen?.Trim(), String.Empty)
        End Select
    End Function
End Class

''' <summary>Nesto#543: una modalidad del evento («Con cupón de productos: 100 €»). El importe es el precio de la línea.</summary>
Public Class ModalidadEventoDTO
    Public Property Modalidad As String
    Public Property Nombre As String
    Public Property Importe As Decimal

    Public ReadOnly Property Texto As String
        Get
            Return String.Format(CultureInfo.GetCultureInfo("es-ES"), "{0}: {1:C2}", Nombre?.Trim(), Importe)
        End Get
    End Property
End Class

''' <summary>Nesto#543 / NestoAPI#691: un pedido que la API ha creado aparte al guardar (los cursos van en serie CV).</summary>
Public Class PedidoAdicionalDTO
    Public Property Numero As Integer
    Public Property Motivo As String
End Class

''' <summary>
''' Nesto#543: lo que contesta la API al crear el pedido desde la plantilla. El número sale de la cabecera Location
''' (como siempre) y, si la API ha separado los cursos, los pedidos adicionales salen del cuerpo.
''' </summary>
Public Class RespuestaCrearPedidoPlantilla
    Public Property Numero As String
    Public Property PedidosAdicionales As List(Of PedidoAdicionalDTO) = New List(Of PedidoAdicionalDTO)

    ''' <summary>
    ''' Lee «PedidosAdicionales» del cuerpo de la respuesta, sin importar mayúsculas. Cualquier otra cosa (cuerpo
    ''' vacío, API anterior que no lo trae, JSON raro) da lista vacía: el pedido ya está creado y no se estropea nada.
    ''' </summary>
    Public Shared Function LeerPedidosAdicionales(json As String) As List(Of PedidoAdicionalDTO)
        Dim lista As New List(Of PedidoAdicionalDTO)
        If String.IsNullOrWhiteSpace(json) Then
            Return lista
        End If
        Try
            Dim raiz = TryCast(JToken.Parse(json), JObject)
            If raiz Is Nothing Then
                Return lista
            End If
            Dim token As JToken = raiz.GetValue("PedidosAdicionales", StringComparison.OrdinalIgnoreCase)
            Dim adicionales = TryCast(token, JArray)
            If adicionales Is Nothing Then
                Return lista
            End If
            For Each item In adicionales.OfType(Of JObject)()
                Dim numero As JToken = item.GetValue("Numero", StringComparison.OrdinalIgnoreCase)
                Dim valor As Integer
                If numero Is Nothing OrElse Not Integer.TryParse(numero.ToString(), valor) OrElse valor <= 0 Then
                    Continue For
                End If
                lista.Add(New PedidoAdicionalDTO With {
                    .Numero = valor,
                    .Motivo = item.GetValue("Motivo", StringComparison.OrdinalIgnoreCase)?.ToString()
                })
            Next
        Catch ex As Exception
            ' Respuesta que no es JSON: sin pedidos adicionales
        End Try
        Return lista
    End Function
End Class
