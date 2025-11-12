Imports System

''' <summary>
''' Información de un pedido que tuvo errores en el proceso de facturación
''' </summary>
Public Class PedidoConErrorDTO
    ''' <summary>
    ''' Código de empresa
    ''' </summary>
    Public Property Empresa As String

    ''' <summary>
    ''' Número de pedido
    ''' </summary>
    Public Property NumeroPedido As Integer

    ''' <summary>
    ''' Código de cliente
    ''' </summary>
    Public Property Cliente As String

    ''' <summary>
    ''' Código de contacto
    ''' </summary>
    Public Property Contacto As String

    ''' <summary>
    ''' Nombre del cliente
    ''' </summary>
    Public Property NombreCliente As String

    ''' <summary>
    ''' Código de ruta
    ''' </summary>
    Public Property Ruta As String

    ''' <summary>
    ''' Periodo de facturación (NRM, FDM)
    ''' </summary>
    Public Property PeriodoFacturacion As String

    ''' <summary>
    ''' Tipo de error (Albarán, Factura, Impresión, etc.)
    ''' </summary>
    Public Property TipoError As String

    ''' <summary>
    ''' Mensaje de error detallado
    ''' </summary>
    Public Property MensajeError As String

    ''' <summary>
    ''' Fecha de entrega del pedido
    ''' </summary>
    Public Property FechaEntrega As DateTime

    ''' <summary>
    ''' Total del pedido
    ''' </summary>
    Public Property Total As Decimal
End Class
