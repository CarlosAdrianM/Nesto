Imports System
Imports System.Collections.Generic
Imports System.Linq

''' <summary>
''' Respuesta de facturación masiva de pedidos por rutas
''' </summary>
Public Class FacturarRutasResponseDTO
    Public Sub New()
        PedidosConErrores = New List(Of PedidoConErrorDTO)()
        Albaranes = New List(Of AlbaranCreadoDTO)()
        Facturas = New List(Of FacturaCreadaDTO)()
        NotasEntrega = New List(Of NotaEntregaCreadaDTO)()
    End Sub

    ''' <summary>
    ''' Número de pedidos procesados correctamente
    ''' </summary>
    Public Property PedidosProcesados As Integer

    ''' <summary>
    ''' Lista de albaranes creados.
    ''' Algunos pueden tener DatosImpresion rellenos (con bytes del PDF) si deben imprimirse.
    ''' </summary>
    Public Property Albaranes As List(Of AlbaranCreadoDTO)

    ''' <summary>
    ''' Lista de facturas creadas.
    ''' Algunas pueden tener DatosImpresion rellenos (con bytes del PDF) si deben imprimirse.
    ''' </summary>
    Public Property Facturas As List(Of FacturaCreadaDTO)

    ''' <summary>
    ''' Lista de notas de entrega creadas.
    ''' Las notas de entrega NO se imprimen directamente (no tienen DatosImpresion).
    ''' </summary>
    Public Property NotasEntrega As List(Of NotaEntregaCreadaDTO)

    ''' <summary>
    ''' Lista de pedidos que tuvieron errores en el proceso
    ''' </summary>
    Public Property PedidosConErrores As List(Of PedidoConErrorDTO)

    ''' <summary>
    ''' Tiempo total del proceso
    ''' </summary>
    Public Property TiempoTotal As TimeSpan

    ' Propiedades de conveniencia para contadores (compatibilidad con código existente)

    ''' <summary>
    ''' Número de albaranes creados (calculado)
    ''' </summary>
    Public ReadOnly Property AlbaranesCreados As Integer
        Get
            Return If(Albaranes?.Count, 0)
        End Get
    End Property

    ''' <summary>
    ''' Número de facturas creadas (calculado)
    ''' </summary>
    Public ReadOnly Property FacturasCreadas As Integer
        Get
            Return If(Facturas?.Count, 0)
        End Get
    End Property

    ''' <summary>
    ''' Número de albaranes con datos de impresión (calculado)
    ''' </summary>
    Public ReadOnly Property AlbaranesParaImprimir As Integer
        Get
            If Albaranes Is Nothing Then Return 0
            Return Albaranes.Where(Function(a) a.DatosImpresion IsNot Nothing).Count()
        End Get
    End Property

    ''' <summary>
    ''' Número de facturas con datos de impresión (calculado)
    ''' </summary>
    Public ReadOnly Property FacturasParaImprimir As Integer
        Get
            If Facturas Is Nothing Then Return 0
            Return Facturas.Where(Function(f) f.DatosImpresion IsNot Nothing).Count()
        End Get
    End Property

    ''' <summary>
    ''' Número de notas de entrega creadas (calculado)
    ''' </summary>
    Public ReadOnly Property NotasEntregaCreadas As Integer
        Get
            Return If(NotasEntrega?.Count, 0)
        End Get
    End Property
End Class
