Imports System.Collections.Generic
Imports System.Linq

''' <summary>
''' DTO que contiene los documentos listos para imprimir de un pedido.
''' Incluye facturas, albaranes y notas de entrega según corresponda.
''' </summary>
Public Class DocumentosImpresionPedidoDTO
    Public Sub New()
        Facturas = New List(Of FacturaCreadaDTO)()
        Albaranes = New List(Of AlbaranCreadoDTO)()
        NotasEntrega = New List(Of NotaEntregaCreadaDTO)()
    End Sub

    ''' <summary>
    ''' Facturas generadas para el pedido (si se facturó y no es FIN_DE_MES)
    ''' </summary>
    Public Property Facturas As List(Of FacturaCreadaDTO)

    ''' <summary>
    ''' Albaranes generados para el pedido
    ''' </summary>
    Public Property Albaranes As List(Of AlbaranCreadoDTO)

    ''' <summary>
    ''' Notas de entrega generadas para el pedido
    ''' </summary>
    Public Property NotasEntrega As List(Of NotaEntregaCreadaDTO)

    ''' <summary>
    ''' Indica si hay algún documento para imprimir
    ''' </summary>
    Public ReadOnly Property HayDocumentosParaImprimir As Boolean
        Get
            Return (Facturas IsNot Nothing AndAlso Facturas.Any(Function(f) f.DatosImpresion IsNot Nothing)) OrElse
                   (Albaranes IsNot Nothing AndAlso Albaranes.Any(Function(a) a.DatosImpresion IsNot Nothing)) OrElse
                   (NotasEntrega IsNot Nothing AndAlso NotasEntrega.Any(Function(n) n.DatosImpresion IsNot Nothing))
        End Get
    End Property

    ''' <summary>
    ''' Total de documentos que se imprimirán (considerando copias)
    ''' </summary>
    Public ReadOnly Property TotalDocumentosParaImprimir As Integer
        Get
            Dim total As Integer = 0
            If Facturas IsNot Nothing Then
                total += Facturas.Where(Function(f) f.DatosImpresion IsNot Nothing).Sum(Function(f) f.DatosImpresion.NumeroCopias)
            End If
            If Albaranes IsNot Nothing Then
                total += Albaranes.Where(Function(a) a.DatosImpresion IsNot Nothing).Sum(Function(a) a.DatosImpresion.NumeroCopias)
            End If
            If NotasEntrega IsNot Nothing Then
                total += NotasEntrega.Where(Function(n) n.DatosImpresion IsNot Nothing).Sum(Function(n) n.DatosImpresion.NumeroCopias)
            End If
            Return total
        End Get
    End Property

    ''' <summary>
    ''' Tipo de documento principal que se generó (para información al usuario)
    ''' </summary>
    Public Property TipoDocumentoPrincipal As String

    ''' <summary>
    ''' Mensaje descriptivo de lo que se generó
    ''' </summary>
    Public Property Mensaje As String
End Class
