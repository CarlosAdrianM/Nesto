''' <summary>
''' Modelo para guardar borradores de PlantillaVenta localmente.
''' Issue #286: Borradores de PlantillaVenta
'''
''' IMPORTANTE: Guardamos el ESTADO de la PlantillaVenta, NO el PedidoVentaDTO.
''' El PedidoVentaDTO es el resultado final y no contiene información suficiente
''' para reconstruir la plantilla (ofertas 6+1, Ganavisiones, etc.)
'''
''' Los borradores se guardan como archivos JSON en %LOCALAPPDATA%\Nesto\Borradores\
''' Usamos directamente LineaPlantillaVenta y LineaRegalo para simplicidad.
''' </summary>
Public Class BorradorPlantillaVenta
    ''' <summary>
    ''' Identificador único del borrador (GUID)
    ''' </summary>
    Public Property Id As String

    ''' <summary>
    ''' Fecha y hora en que se guardó el borrador
    ''' </summary>
    Public Property FechaCreacion As DateTime

    ''' <summary>
    ''' Usuario que creó el borrador
    ''' </summary>
    Public Property Usuario As String

    ''' <summary>
    ''' Mensaje de error que causó la creación del borrador (si aplica)
    ''' </summary>
    Public Property MensajeError As String

    ' ========== Datos del cliente ==========
    ''' <summary>
    ''' Empresa del cliente
    ''' </summary>
    Public Property Empresa As String

    ''' <summary>
    ''' Número de cliente
    ''' </summary>
    Public Property Cliente As String

    ''' <summary>
    ''' Contacto/dirección de entrega
    ''' </summary>
    Public Property Contacto As String

    ''' <summary>
    ''' Nombre del cliente (para mostrar en la lista)
    ''' </summary>
    Public Property NombreCliente As String

    ' ========== Líneas de productos ==========
    ''' <summary>
    ''' Líneas de productos con cantidad > 0 o cantidadOferta > 0.
    ''' Usamos directamente LineaPlantillaVenta para guardar todos los datos del producto.
    ''' </summary>
    Public Property LineasProducto As List(Of LineaPlantillaVenta)

    ' ========== Regalos Ganavisiones ==========
    ''' <summary>
    ''' Líneas de regalos (Ganavisiones) con cantidad > 0.
    ''' Usamos directamente LineaRegalo para guardar todos los datos.
    ''' </summary>
    Public Property LineasRegalo As List(Of LineaRegalo)

    ' ========== Configuración ==========
    ''' <summary>
    ''' Comentario para la ruta
    ''' </summary>
    Public Property ComentarioRuta As String

    ''' <summary>
    ''' Si es presupuesto en lugar de pedido
    ''' </summary>
    Public Property EsPresupuesto As Boolean

    ''' <summary>
    ''' Forma de venta seleccionada (1=Directa, 2=Teléfono, o número de otra forma)
    ''' </summary>
    Public Property FormaVenta As Integer

    ''' <summary>
    ''' Código de forma de venta "otras" (si FormaVenta > 2)
    ''' </summary>
    Public Property FormaVentaOtrasCodigo As String

    ''' <summary>
    ''' Código de forma de pago
    ''' </summary>
    Public Property FormaPago As String

    ''' <summary>
    ''' Código de plazos de pago
    ''' </summary>
    Public Property PlazosPago As String

    ' ========== Entrega ==========
    ''' <summary>
    ''' Fecha de entrega seleccionada
    ''' </summary>
    Public Property FechaEntrega As Date

    ''' <summary>
    ''' Código de almacén seleccionado
    ''' </summary>
    Public Property AlmacenCodigo As String

    ''' <summary>
    ''' Si mantener junto en el reparto
    ''' </summary>
    Public Property MantenerJunto As Boolean

    ''' <summary>
    ''' Si servir junto
    ''' </summary>
    Public Property ServirJunto As Boolean

    ' ========== Comentarios ==========
    ''' <summary>
    ''' Comentario de picking introducido por el usuario
    ''' </summary>
    Public Property ComentarioPicking As String

    ' ========== Propiedades calculadas para la UI ==========

    ' Contadores cacheados para cuando las colecciones se limpian en la lista de borradores
    <Newtonsoft.Json.JsonIgnore>
    Friend Property NumeroLineasCache As Integer?
    <Newtonsoft.Json.JsonIgnore>
    Friend Property NumeroRegalosCache As Integer?

    ''' <summary>
    ''' Número total de líneas (productos + regalos)
    ''' </summary>
    Public ReadOnly Property NumeroLineas As Integer
        Get
            Dim count = (If(LineasProducto?.Count, 0)) + (If(LineasRegalo?.Count, 0))
            Return If(count > 0, count, If(NumeroLineasCache, 0))
        End Get
    End Property

    ''' <summary>
    ''' Descripción corta para mostrar en la lista de borradores
    ''' </summary>
    Public ReadOnly Property Descripcion As String
        Get
            Dim lineasInfo = $"{NumeroLineas} líneas"
            Dim numRegalos = If(LineasRegalo?.Count, If(NumeroRegalosCache, 0))
            If numRegalos > 0 Then
                lineasInfo &= $" ({numRegalos} regalos)"
            End If
            Return $"{Cliente} - {NombreCliente} ({lineasInfo}) - {FechaCreacion:dd/MM/yyyy HH:mm}"
        End Get
    End Property

    ' ========== Propiedad obsoleta para compatibilidad ==========
    ''' <summary>
    ''' OBSOLETO: No usar. Solo para compatibilidad con borradores antiguos.
    ''' Los nuevos borradores usan LineasProducto y LineasRegalo.
    ''' </summary>
    <Obsolete("Usar LineasProducto y LineasRegalo en su lugar")>
    Public Property Pedido As Nesto.Models.PedidoVentaDTO

    ''' <summary>
    ''' Total estimado del pedido (solo productos, sin regalos)
    ''' </summary>
    Public Property Total As Decimal
End Class
