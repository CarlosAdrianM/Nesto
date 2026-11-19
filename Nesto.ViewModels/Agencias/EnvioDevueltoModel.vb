' Sugerencia 1356: EnvioDevueltoModel, ReenvioEnvioPeticion y DecisionReenvioModel viven ahora en ControlesUsuario.Services
' (ControlesUsuario/Services/EnviosDevueltos.cs), para que los usen Agencias › Devueltos y el detalle del pedido.

''' <summary>NestoAPI#870: la respuesta de POST api/EnviosAgencias/{id}/HaVuelto (EnvioHaVueltoDTO).</summary>
Public Class EnvioHaVueltoModel
    Public Property Envio As Integer
    Public Property Empresa As String
    Public Property Agencia As Integer
    Public Property NombreAgencia As String
    Public Property CodigoBarras As String
    Public Property Pedido As Integer?
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Nombre As String
    Public Property Poblacion As String
    Public Property Estado As Short
    Public Property Bultos As Short
    Public Property FechaHaVuelto As Date?
    Public Property UsuarioHaVuelto As String
    Public Property ComoHaVuelto As String
    Public Property YaEstabaMarcado As Boolean
    Public Property Decision As String
    Public Property EnvioNuevo As Integer?
    Public Property EstadoEnvioNuevo As Short?
    Public Property ReenvioEsperando As Boolean
    Public Property AvisoEnviadoA As String
    ''' <summary>El texto para el usuario (de usted), tal cual.</summary>
    Public Property Mensaje As String
End Class
