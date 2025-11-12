Imports System

Namespace Models.Facturas
    ''' <summary>
    ''' Request para facturación masiva de pedidos por rutas.
    ''' REFACTORIZACIÓN: TipoRuta ahora es String (Id del tipo) en lugar de enum.
    ''' </summary>
    Public Class FacturarRutasRequestDTO
        ''' <summary>
        ''' Id del tipo de ruta a facturar (ej: "PROPIA", "AGENCIA")
        ''' </summary>
        Public Property TipoRuta As String

        ''' <summary>
        ''' Fecha de entrega desde la cual filtrar pedidos.
        ''' Si es Nothing, usa DateTime.Today
        ''' </summary>
        Public Property FechaEntregaDesde As DateTime?
    End Class
End Namespace