Imports System.Globalization
Imports Newtonsoft.Json

''' <summary>
''' Nesto#505 (sugerencia de Paloma, 30/09/26): lo que el cliente tiene a su favor en el extracto
''' (GET api/ExtractosCliente/SaldoAFavor), para avisar a quien va a mandarle un enlace de pago.
''' Solo informa: puede ser una entrega a cuenta de otro pedido o una reserva para un evento, así
''' que NUNCA se descuenta solo. Lo decide el usuario, después de mirarlo en el extracto.
''' </summary>
Public Class SaldoAFavorCliente
    Private Shared ReadOnly culturaEspanola As CultureInfo = CultureInfo.GetCultureInfo("es-ES")

    ''' <summary>La suma de lo que hay a favor, en positivo.</summary>
    Public Property Total As Decimal
    ''' <summary>Lo que el cliente debe: si no es cero, parte de lo «a favor» puede ser un cobro sin casar con su factura.</summary>
    Public Property PendienteDePago As Decimal
    Public Property Movimientos As New List(Of MovimientoAFavor)

    ''' <summary>De qué cliente es: si en la plantilla se cambia de cliente, este saldo ya no vale.</summary>
    <JsonIgnore>
    Public Property Cliente As String

    Public ReadOnly Property HayAlgoAFavor As Boolean
        Get
            Return Total > 0
        End Get
    End Property

    Public ReadOnly Property Resumen As String
        Get
            Return String.Format(culturaEspanola, "El cliente tiene {0:C2} a su favor:", Total)
        End Get
    End Property

    Public ReadOnly Property TieneAvisoPendienteDePago As Boolean
        Get
            Return PendienteDePago > 0
        End Get
    End Property

    Public ReadOnly Property AvisoPendienteDePago As String
        Get
            Return If(PendienteDePago > 0,
                String.Format(culturaEspanola, "Ojo: también tiene {0:C2} pendientes de pago. Parte de ese saldo puede ser un cobro todavía sin casar con su factura.", PendienteDePago),
                String.Empty)
        End Get
    End Property

    Public ReadOnly Property TextoDescontar As String
        Get
            Return String.Format(culturaEspanola, "Descontar {0:C2} del enlace de pago", Total)
        End Get
    End Property

    ''' <summary>
    ''' Lo que se descuenta del enlace: nada, salvo que el usuario lo haya marcado y el saldo sea
    ''' del mismo cliente al que se le hace el pedido.
    ''' </summary>
    Public Shared Function SaldoADescontar(saldo As SaldoAFavorCliente, descontar As Boolean, clienteDelPedido As String) As Decimal
        If Not descontar OrElse saldo Is Nothing OrElse saldo.Total <= 0 Then
            Return 0
        End If
        Return If(String.Equals(saldo.Cliente?.Trim(), clienteDelPedido?.Trim(), StringComparison.OrdinalIgnoreCase), saldo.Total, 0D)
    End Function

    ''' <summary>Por cuánto sale el enlace. Cero si el saldo cubre el pedido entero: entonces no se manda.</summary>
    Public Shared Function ImporteDelEnlace(totalPedido As Decimal, saldoADescontar As Decimal) As Decimal
        Return Math.Max(0D, Math.Round(totalPedido - Math.Max(0D, saldoADescontar), 2, MidpointRounding.AwayFromZero))
    End Function
End Class

Public Class MovimientoAFavor
    Private Shared ReadOnly culturaEspanola As CultureInfo = CultureInfo.GetCultureInfo("es-ES")

    Public Property Id As Integer
    Public Property Empresa As String
    Public Property Contacto As String
    Public Property Fecha As Date
    Public Property Documento As String
    Public Property Concepto As String
    Public Property FormaPago As String
    ''' <summary>Lo que queda a favor en este apunte, en positivo.</summary>
    Public Property Importe As Decimal

    ''' <summary>«12/09/26 · Entrega a cuenta · 30,00 €»: lo justo para buscarlo en el extracto.</summary>
    Public ReadOnly Property Texto As String
        Get
            Dim que As String = If(String.IsNullOrWhiteSpace(Concepto), Documento, Concepto)
            Return String.Format(culturaEspanola, "{0:dd/MM/yy} · {1} · {2:C2}", Fecha, que?.Trim(), Importe)
        End Get
    End Property
End Class
