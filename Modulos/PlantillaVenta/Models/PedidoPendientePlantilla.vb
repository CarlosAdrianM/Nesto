Imports System.Globalization
Imports Newtonsoft.Json

''' <summary>
''' Nesto#550 / Nesto#554 (sugerencias 759 y 775, Paloma): un pedido pendiente del cliente tal y como lo devuelve
''' GET api/PlantillaVentas/PedidosPendientesDetalle. Numero = 0 es «Pedido nuevo» (la opción de no ampliar ninguno).
''' </summary>
Public Class PedidoPendientePlantilla
    Private Shared ReadOnly culturaEspanola As CultureInfo = CultureInfo.GetCultureInfo("es-ES")

    Public Const TEXTO_PEDIDO_NUEVO As String = "Pedido nuevo"

    Public Property Numero As Integer
    ''' <summary>Fecha del pedido.</summary>
    Public Property Fecha As DateTime?
    ''' <summary>Líneas pendientes.</summary>
    Public Property NumeroLineas As Integer
    ''' <summary>Base imponible de las líneas pendientes.</summary>
    Public Property BaseImponible As Decimal
    ''' <summary>La fecha de entrega más próxima de las líneas pendientes.</summary>
    Public Property FechaEntrega As DateTime?
    ''' <summary>Nesto#554: las notas de entrega no se pueden modificar desde la plantilla.</summary>
    Public Property NotaEntrega As Boolean
    ''' <summary>Alguna línea pendiente ya tiene picking.</summary>
    Public Property TienePicking As Boolean
    ''' <summary>Retenido «hasta nuevo aviso» (NestoAPI#698).</summary>
    Public Property Retenido As Boolean

    ''' <summary>Lo que se enseña en el desplegable «Ampliar en el pedido» y en el diálogo de pendientes.</summary>
    <JsonIgnore>
    Public ReadOnly Property Texto As String
        Get
            Return Describir(Me)
        End Get
    End Property

    ''' <summary>La opción «Pedido nuevo» que cierra la lista (igual que el 0 de siempre).</summary>
    Public Shared Function PedidoNuevo() As PedidoPendientePlantilla
        Return New PedidoPendientePlantilla With {.Numero = 0}
    End Function

    ''' <summary>
    ''' Nesto#550: «928141 · del 08/10/2026 · 5 líneas · 138,10 € · entrega 09/10/2027 · retenido · con picking».
    ''' Lo que no se sabe (sin fecha, sin líneas) no se escribe. Numero = 0 → «Pedido nuevo».
    ''' </summary>
    Public Shared Function Describir(pedido As PedidoPendientePlantilla) As String
        If pedido Is Nothing OrElse pedido.Numero = 0 Then
            Return TEXTO_PEDIDO_NUEVO
        End If

        Dim partes As New List(Of String) From {pedido.Numero.ToString(culturaEspanola)}
        If pedido.Fecha.HasValue Then
            partes.Add($"del {pedido.Fecha.Value.ToString("dd/MM/yyyy", culturaEspanola)}")
        End If
        If pedido.NumeroLineas > 0 Then
            partes.Add(If(pedido.NumeroLineas = 1, "1 línea", $"{pedido.NumeroLineas} líneas"))
            partes.Add($"{pedido.BaseImponible.ToString("N2", culturaEspanola)} €")
        End If
        If pedido.FechaEntrega.HasValue Then
            partes.Add($"entrega {pedido.FechaEntrega.Value.ToString("dd/MM/yyyy", culturaEspanola)}")
        End If
        If pedido.Retenido Then
            partes.Add("retenido")
        End If
        If pedido.TienePicking Then
            partes.Add("con picking")
        End If
        If pedido.NotaEntrega Then
            partes.Add("nota de entrega")
        End If
        Return String.Join(" · ", partes)
    End Function
End Class
