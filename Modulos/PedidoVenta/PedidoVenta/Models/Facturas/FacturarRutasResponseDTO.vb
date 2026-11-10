Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Nesto.Modulos.PedidoVenta.Models.Facturas

''' <summary>
''' Respuesta de facturación masiva de pedidos por rutas
''' </summary>
Public Class FacturarRutasResponseDTO
    Public Sub New()
        PedidosConErrores = New List(Of PedidoConErrorDTO)()
        Albaranes = New List(Of AlbaranCreadoDTO)()
        Facturas = New List(Of FacturaCreadaDTO)()
        NotasEntrega = New List(Of NotaEntregaCreadaDTO)()
        ReembolsosDesfasados = New List(Of ReembolsoDesfasadoDTO)()
        ImpresosEnPacking = New List(Of DocumentoEnPapelDTO)()
        PendientesEnPacking = New List(Of DocumentoEnPapelDTO)()
    End Sub

    ''' <summary>NestoAPI#828: en Facturar rutas de agencias, lo de hoy que ya salió en papel en el packing (no entra en la tanda).</summary>
    Public Property ImpresosEnPacking As List(Of DocumentoEnPapelDTO)

    ''' <summary>NestoAPI#828: lo de hoy que no salió en el packing (la impresora falló), con su pedido y su envío.</summary>
    Public Property PendientesEnPacking As List(Of DocumentoEnPapelDTO)

    ''' <summary>
    ''' NestoAPI#828: el resumen de la API: «N facturas en papel ya impresas en el packing.», lo pendiente y lo que sale en esta
    ''' facturación con su envío para localizar la caja. Nothing si no hay nada que decir (o una API anterior).
    ''' </summary>
    Public Property ResumenPapelPacking As String

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
    ''' Lista de notas de entrega y de recogida creadas.
    ''' Nesto#580 / NestoAPI#758: las de ruta propia traen DatosImpresion (PDF con el código de barras NE/NR, 2 copias).
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

    ''' <summary>
    ''' Nesto#615 (NestoAPI#810): envíos del reparto propio sin entregar de los pedidos facturados cuyo reembolso no es lo que
    ''' queda por cobrar de sus facturas. No se cambian solos: ResolutorReembolsosDesfasados pregunta y llama al PUT.
    ''' </summary>
    Public Property ReembolsosDesfasados As List(Of ReembolsoDesfasadoDTO)

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
    ''' Número de albaranes con datos de impresión que tiene que imprimir Nesto (Nesto#620: sin los ya impresos en el servidor)
    ''' </summary>
    Public ReadOnly Property AlbaranesParaImprimir As Integer
        Get
            If Albaranes Is Nothing Then Return 0
            Return Albaranes.Where(Function(a) a.HayQueImprimirEnLocal).Count()
        End Get
    End Property

    ''' <summary>
    ''' Número de facturas con datos de impresión que tiene que imprimir Nesto (Nesto#620: sin las ya impresas en el servidor)
    ''' </summary>
    Public ReadOnly Property FacturasParaImprimir As Integer
        Get
            If Facturas Is Nothing Then Return 0
            Return Facturas.Where(Function(f) f.HayQueImprimirEnLocal).Count()
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

    ''' <summary>
    ''' Nesto#580: notas (de entrega o de recogida) con datos de impresión.
    ''' </summary>
    Public ReadOnly Property NotasEntregaParaImprimir As Integer
        Get
            Return NotasDeEntregaParaImprimir + NotasDeRecogidaParaImprimir
        End Get
    End Property

    ''' <summary>
    ''' Nesto#580: notas de ENTREGA con datos de impresión.
    ''' </summary>
    Public ReadOnly Property NotasDeEntregaParaImprimir As Integer
        Get
            If NotasEntrega Is Nothing Then Return 0
            Return NotasEntrega.Where(Function(n) n IsNot Nothing AndAlso n.HayQueImprimirEnLocal AndAlso Not n.EsRecogida).Count()
        End Get
    End Property

    ''' <summary>
    ''' Nesto#580: notas de RECOGIDA con datos de impresión.
    ''' </summary>
    Public ReadOnly Property NotasDeRecogidaParaImprimir As Integer
        Get
            If NotasEntrega Is Nothing Then Return 0
            Return NotasEntrega.Where(Function(n) n IsNot Nothing AndAlso n.HayQueImprimirEnLocal AndAlso n.EsRecogida).Count()
        End Get
    End Property

    ''' <summary>
    ''' Nesto#580: notas de RECOGIDA creadas (con o sin impresión).
    ''' </summary>
    Public ReadOnly Property NotasRecogidaCreadas As Integer
        Get
            If NotasEntrega Is Nothing Then Return 0
            Return NotasEntrega.Where(Function(n) n IsNot Nothing AndAlso n.EsRecogida).Count()
        End Get
    End Property

    ''' <summary>
    ''' Nesto#580: etiquetas de recogida (NR{Pedido}-{N}) que la API ha impreso por la Zebra.
    ''' </summary>
    Public ReadOnly Property EtiquetasRecogidaImpresas As Integer
        Get
            If NotasEntrega Is Nothing Then Return 0
            Return NotasEntrega.Where(Function(n) n IsNot Nothing).Sum(Function(n) n.EtiquetasRecogidaImpresas)
        End Get
    End Property

    ''' <summary>
    ''' Nesto#580: todo lo que hay que imprimir en papel en Nesto (facturas, albaranes y notas de entrega / recogida).
    ''' Nesto#620 (NestoAPI#806): sin los que la API ya ha impreso en el servidor (ImpresoEnServidor)
    ''' </summary>
    Public ReadOnly Property TotalDocumentosParaImprimir As Integer
        Get
            Return FacturasParaImprimir + AlbaranesParaImprimir + NotasEntregaParaImprimir
        End Get
    End Property

    ''' <summary>
    ''' Nesto#620 (NestoAPI#806): los documentos en papel que la API ya ha impreso en el servidor (Nesto no los imprime).
    ''' </summary>
    Public ReadOnly Property DocumentosImpresosEnServidor As List(Of DocumentoParaImprimir)
        Get
            Return DocumentosEnPapel().Where(Function(d) d.EstaImpresoEnServidor).ToList()
        End Get
    End Property

    ''' <summary>
    ''' Nesto#620: cuántos documentos en papel vienen ya impresos en el servidor.
    ''' </summary>
    Public ReadOnly Property TotalDocumentosImpresosEnServidor As Integer
        Get
            Return DocumentosImpresosEnServidor.Count
        End Get
    End Property

    ''' <summary>
    ''' Nesto#620: «N documento(s) impresos en el servidor en «X»», o vacío si ninguno.
    ''' </summary>
    Public ReadOnly Property TextoImpresosEnServidor As String
        Get
            Return ImpresionEnServidor.TextoImpresos(DocumentosImpresosEnServidor)
        End Get
    End Property

    Private Function DocumentosEnPapel() As IEnumerable(Of DocumentoParaImprimir)
        Dim documentos As New List(Of DocumentoImprimibleDTO)
        If Facturas IsNot Nothing Then documentos.AddRange(Facturas)
        If Albaranes IsNot Nothing Then documentos.AddRange(Albaranes)
        If NotasEntrega IsNot Nothing Then documentos.AddRange(NotasEntrega)
        Return documentos.Where(Function(d) d IsNot Nothing AndAlso d.DatosImpresion IsNot Nothing).Select(Function(d) d.DatosImpresion)
    End Function
End Class
