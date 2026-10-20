Imports Nesto.Models.Nesto.Models

''' <summary>
''' NestoAPI#595 (slice 2): cuerpo de POST api/EnviosAgencias/DesdePedido. El servidor parte de su propuesta, aplica
''' los valores de la pantalla (<see cref="Cambios"/>, que se mandan SIEMPRE todos), pide lo que falte (409) y graba el
''' envío. Los nombres son el contrato con CrearEnvioDesdePedidoDTO del servidor (ver AgenciaServiceEnvioDesdePedidoTests).
''' </summary>
Public Class CrearEnvioDesdePedidoDto
    Public Property Empresa As String
    Public Property Pedido As Integer
    ''' <summary>Los bultos TOTALES del envío (en una ampliación, los del envío entero).</summary>
    Public Property Bultos As Short?
    Public Property Peso As Decimal
    ''' <summary>Los códigos de los avisos «Confirmar» que el usuario ya ha confirmado.</summary>
    Public Property Confirmaciones As New List(Of String)
    ''' <summary>True = descontar los prepagos del reembolso; False = no (o ya decidido en la pantalla); Nothing = sin decidir.</summary>
    Public Property DescontarPrepagos As Boolean?
    ''' <summary>Las dimensiones de los bultos de Canteras (AnchoxAltoxLargo).</summary>
    Public Property Dimensiones As String
    ''' <summary>El servicio elegido crea una etiqueta de recogida: el servidor contesta 400 y se sigue por el camino de siempre.</summary>
    Public Property EtiquetaRecogida As Boolean
    Public Property Cambios As CambiosEnvioDesdePedidoDto
End Class

''' <summary>NestoAPI#595 (slice 2): los valores de la pantalla de Agencias (CambiosEnvioDesdePedidoDTO del servidor).</summary>
Public Class CambiosEnvioDesdePedidoDto
    Public Property Agencia As Integer?
    Public Property Servicio As Short?
    Public Property Horario As Short?
    Public Property Retorno As Short?
    ''' <summary>El reembolso de ESTE pedido (el de la pantalla).</summary>
    Public Property Reembolso As Decimal?
    Public Property Nombre As String
    Public Property Direccion As String
    Public Property CodPostal As String
    Public Property Poblacion As String
    Public Property Provincia As String
    Public Property Pais As Integer?
    Public Property Telefono As String
    Public Property Movil As String
    Public Property Email As String
    Public Property Atencion As String
    Public Property Observaciones As String
End Class

''' <summary>NestoAPI#595 (slice 2): un aviso que el servidor necesita resuelto antes de grabar (AvisoPropuestaEnvio).</summary>
Public Class AvisoEnvioDesdePedidoDto
    Public Property Codigo As String
    Public Property Texto As String
    Public Property Tipo As String
End Class

''' <summary>NestoAPI#595 (slice 2): la respuesta 200 (EnvioDesdePedidoDTO del servidor).</summary>
Public Class EnvioDesdePedidoDto
    Public Property Envio As EnviosAgencia
    Public Property Origen As String
    Public Property EnvioOrigen As Integer?
    Public Property HayQueImprimir As Boolean
    Public Property Flujo As String
    Public Property EtiquetaEnNesto As Boolean
    Public Property CorreoEnviado As Boolean
    Public Property Avisos As New List(Of String)
End Class

''' <summary>NestoAPI#595 (slice 2): la respuesta 409 (ConflictoEnvioDesdePedidoDTO del servidor). La propuesta no se lee.</summary>
Public Class ConflictoEnvioDesdePedidoDto
    Public Property Pendientes As New List(Of AvisoEnvioDesdePedidoDto)
End Class

''' <summary>NestoAPI#595 (slice 2): la respuesta 400 (ErrorEnvioDesdePedidoDTO del servidor).</summary>
Public Class ErrorEnvioDesdePedidoDto
    Public Property Message As String
    Public Property Codigo As String
End Class

''' <summary>NestoAPI#595 (slice 2): lo que ha pasado al pedirle al servidor que cree el envío.</summary>
Public Enum EstadoEnvioDesdePedido
    ''' <summary>200: grabado.</summary>
    Creado
    ''' <summary>409: hay que resolver los pendientes y repetir.</summary>
    ConPendientes
    ''' <summary>400 o 404: no se ha grabado nada.</summary>
    Rechazado
End Enum

''' <summary>NestoAPI#595 (slice 2): el resultado de IAgenciaService.CrearEnvioDesdePedido, sea cual sea el código HTTP.</summary>
Public Class ResultadoEnvioDesdePedidoModel
    Public Property Estado As EstadoEnvioDesdePedido
    Public Property Creado As EnvioDesdePedidoDto
    Public Property Pendientes As New List(Of AvisoEnvioDesdePedidoDto)
    ''' <summary>Con Rechazado, el código del servidor (CodigosErrorEnvioDesdePedido; «NoEncontrado» con un 404).</summary>
    Public Property CodigoError As String
    Public Property MensajeError As String
End Class

''' <summary>NestoAPI#595 (slice 2): los códigos y tipos del contrato con el servidor que Nesto necesita conocer.</summary>
Public NotInheritable Class ContratoEnvioDesdePedido
    Public Const AVISO_SIN_PICKING As String = "SinPicking"
    Public Const AVISO_AMPLIACION As String = "Ampliacion"
    Public Const AVISO_DIRECCION_PROPIA As String = "DireccionPropia"
    Public Const AVISO_PREPAGOS As String = "Prepagos"
    Public Const AVISO_DIMENSIONES As String = "Dimensiones"
    Public Const TIPO_CONFIRMAR As String = "Confirmar"
    Public Const TIPO_ELEGIR As String = "Elegir"
    Public Const TIPO_DATO As String = "Dato"
    Public Const ERROR_ETIQUETA_RECOGIDA As String = "EtiquetaRecogida"
    Public Const ERROR_AGENCIA_NO_SOPORTADA As String = "AgenciaNoSoportada"
    Public Const ERROR_NO_ENCONTRADO As String = "NoEncontrado"
    Public Const ORIGEN_NUEVO As String = "Nuevo"
    Public Const ORIGEN_PENDIENTE_REUTILIZADO As String = "PendienteReutilizado"

    Private Sub New()
    End Sub
End Class
