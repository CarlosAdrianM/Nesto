Imports System.Globalization
Imports Nesto.Models

''' <summary>
''' Sugerencia 1192 (aprobada por Carlos 11/11/26): con un cliente de público final (estado 8, el mismo que
''' Constantes.Clientes.Estados.PUBLICO_FINAL de NestoAPI) la plantilla enseña junto al precio el precio con IVA,
''' para que nadie confunda el precio de la web (con IVA) con el de la plantilla (sin IVA). Solo informativo: no
''' cambia ningún precio ni lo que se graba. El porcentaje sale de ParametrosIva (IVA del cliente x IVA del producto).
''' </summary>
Public Module PrecioConIvaPublicoFinal
    Public Const ESTADO_PUBLICO_FINAL As Integer = 8

    Private ReadOnly CulturaEspanola As New CultureInfo("es-ES")

    ''' <summary>
    ''' ¿Hay que enseñar el precio con IVA? El cliente lleva IVA y es de público final: estado 8 o el cliente especial
    ''' «venta tienda» (Constantes.Clientes.Especiales.PUBLICO_FINAL), que está en estado 3.
    ''' </summary>
    Public Function EsPublicoFinal(estadoCliente As Integer, ivaCliente As String, cliente As String) As Boolean
        If String.IsNullOrWhiteSpace(ivaCliente) Then
            Return False
        End If
        Return estadoCliente = ESTADO_PUBLICO_FINAL OrElse
            String.Equals(cliente?.Trim(), Nesto.Infrastructure.Shared.Constantes.Clientes.Especiales.PUBLICO_FINAL, StringComparison.Ordinal)
    End Function

    ''' <summary>El porcentaje (0,21 = 21 %) del IVA del producto según los parámetros de IVA del cliente; Nothing si no se sabe.</summary>
    Public Function PorcentajeDe(codigoIvaProducto As String, parametros As IEnumerable(Of ParametrosIvaBase)) As Decimal?
        If String.IsNullOrWhiteSpace(codigoIvaProducto) OrElse parametros Is Nothing Then
            Return Nothing
        End If
        Dim parametro = parametros.FirstOrDefault(Function(p) p IsNot Nothing AndAlso
            String.Equals(p.CodigoIvaProducto?.Trim(), codigoIvaProducto.Trim(), StringComparison.OrdinalIgnoreCase))
        If parametro Is Nothing Then
            Return Nothing
        End If
        Return parametro.PorcentajeIvaProducto
    End Function

    Public Function PrecioConIva(precio As Decimal, porcentajeIva As Decimal) As Decimal
        Return Math.Round(precio * (1D + porcentajeIva), 2, MidpointRounding.AwayFromZero)
    End Function

    ''' <summary>«con IVA: 83,49 €»; Nothing si no hay que enseñarlo.</summary>
    Public Function Texto(precio As Decimal, porcentajeIva As Decimal?) As String
        If Not porcentajeIva.HasValue Then
            Return Nothing
        End If
        Return "con IVA: " & PrecioConIva(precio, porcentajeIva.Value).ToString("#,##0.00", CulturaEspanola) & " €"
    End Function
End Module
