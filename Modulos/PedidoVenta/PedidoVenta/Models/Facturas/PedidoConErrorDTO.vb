Imports System

''' <summary>
''' Nivel de severidad de un mensaje en el proceso de facturación
''' </summary>
Public Enum NivelSeveridad
    ''' <summary>
    ''' Error crítico que impide el proceso (ej: excepción al crear albarán/factura)
    ''' </summary>
    [Error] = 0

    ''' <summary>
    ''' Aviso informativo que el usuario puede ignorar (ej: factura pendiente por MantenerJunto)
    ''' </summary>
    Warning = 1

    ''' <summary>
    ''' Información adicional para el usuario (sin acción requerida)
    ''' </summary>
    Info = 2
End Enum

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
    ''' Nivel de severidad del mensaje.
    ''' Error: fallo crítico que impide el proceso.
    ''' Warning: aviso informativo que el usuario puede ignorar.
    ''' Info: información adicional sin acción requerida.
    ''' </summary>
    Public Property Severidad As NivelSeveridad = NivelSeveridad.Error

    ''' <summary>
    ''' Fecha de entrega del pedido
    ''' </summary>
    Public Property FechaEntrega As DateTime

    ''' <summary>
    ''' Total del pedido
    ''' </summary>
    Public Property Total As Decimal
End Class
