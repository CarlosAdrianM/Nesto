''' <summary>
''' Modelo para guardar borradores de PlantillaVenta localmente.
''' Issue #286: Borradores de PlantillaVenta
'''
''' Los borradores se guardan como archivos JSON en %LOCALAPPDATA%\Nesto\Borradores\
''' cuando falla la creación del pedido por error de red o del servidor.
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
    ''' Cliente del pedido (para mostrar en la lista)
    ''' </summary>
    Public Property Cliente As String

    ''' <summary>
    ''' Nombre del cliente (para mostrar en la lista)
    ''' </summary>
    Public Property NombreCliente As String

    ''' <summary>
    ''' Total del pedido (para mostrar en la lista)
    ''' </summary>
    Public Property Total As Decimal

    ''' <summary>
    ''' Número de líneas del pedido (para mostrar en la lista)
    ''' </summary>
    Public Property NumeroLineas As Integer

    ''' <summary>
    ''' Usuario que creó el borrador
    ''' </summary>
    Public Property Usuario As String

    ''' <summary>
    ''' Mensaje de error que causó la creación del borrador (si aplica)
    ''' </summary>
    Public Property MensajeError As String

    ''' <summary>
    ''' El pedido completo serializado
    ''' </summary>
    Public Property Pedido As Nesto.Models.PedidoVentaDTO

    ''' <summary>
    ''' Descripción corta para mostrar en la lista de borradores
    ''' </summary>
    Public ReadOnly Property Descripcion As String
        Get
            Return $"{Cliente} - {NombreCliente} ({NumeroLineas} líneas, {Total:C2}) - {FechaCreacion:dd/MM/yyyy HH:mm}"
        End Get
    End Property
End Class
