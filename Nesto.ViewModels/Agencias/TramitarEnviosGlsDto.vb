''' <summary>
''' NestoAPI#552 (fase 4): cuerpo de POST api/EnviosAgencias/TramitarTodos. «Tramitar todos» de GLS desde la API: por cada
''' envío, grabarlo en GLS y darlo por tramitado (lo mismo que el botón Tramitar con AgenciaASM).
''' </summary>
Public Class TramitarEnviosGlsDto
    Public Property Agencia As Integer?
    Public Property Envios As List(Of Integer)
End Class

''' <summary>NestoAPI#552 (fase 4): lo que ha pasado con UN envío en «Tramitar todos».</summary>
Public Class ResultadoTramitarEnvioGlsDto
    Public Const TRAMITADO As String = "Tramitado"
    Public Const YA_TRAMITADO As String = "YaTramitado"
    Public Const [ERROR] As String = "Error"

    Public Property Numero As Integer
    Public Property Pedido As Integer?
    Public Property Resultado As String
    Public Property Mensaje As String
    Public Property Asiento As Integer
    Public Property Expedicion As String
    Public Property YaEstabaEnGls As Boolean
End Class

''' <summary>NestoAPI#552 (fase 4): el resumen de «Tramitar todos» (los contadores los calcula el cliente con la lista).</summary>
Public Class ResultadoTramitarEnviosGlsDto
    Public Property Envios As New List(Of ResultadoTramitarEnvioGlsDto)

    Public ReadOnly Property Tramitados As Integer
        Get
            Return Envios.Where(Function(e) e.Resultado = ResultadoTramitarEnvioGlsDto.TRAMITADO).Count()
        End Get
    End Property

    Public ReadOnly Property YaTramitados As Integer
        Get
            Return Envios.Where(Function(e) e.Resultado = ResultadoTramitarEnvioGlsDto.YA_TRAMITADO).Count()
        End Get
    End Property

    Public ReadOnly Property EnviosFallidos As IEnumerable(Of ResultadoTramitarEnvioGlsDto)
        Get
            Return Envios.Where(Function(e) e.Resultado = ResultadoTramitarEnvioGlsDto.ERROR)
        End Get
    End Property
End Class

''' <summary>NestoAPI#552 (fase 4): la etiqueta ZPL de un envío de GLS montada en la API (GET api/EnviosAgencias/{id}/EtiquetaZpl).</summary>
Public Class EtiquetaEnvioGlsDto
    Public Property Envio As Integer
    Public Property CodigoBarras As String
    Public Property Bultos As Integer
    Public Property EtiquetaTipo As String
    Public Property EtiquetaCodificacion As String
    Public Property EtiquetaContenido As String
End Class
