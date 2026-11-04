''' <summary>
''' NestoAPI#699 (corte 2): una fila de GET api/EnviosAgencias/Devueltos: un envío DEVUELTO o INCIDENTADO de los últimos
''' 60 días con la decisión sobre él (sin decidir, reenviar o abonar). Es el POCO que llega por la red; la pestaña Devueltos
''' lo enlaza directamente.
''' </summary>
Public Class EnvioDevueltoModel

    Public Const SIN_DECIDIR As String = "SinDecidir"
    Public Const REENVIAR As String = "Reenviar"
    Public Const ABONAR As String = "Abonar"

    Public Const ESTADO_INCIDENTADO As Short = 3
    Public Const ESTADO_DEVUELTO As Short = 4

    Public Property Numero As Integer
    Public Property Empresa As String
    Public Property Agencia As Integer
    Public Property NombreAgencia As String
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Pedido As Integer?
    Public Property Estado As Short
    Public Property Fecha As Date
    ''' <summary>El texto de la agencia («DEVOLUCIÓN», «AUSENTE»…): el motivo.</summary>
    Public Property DetalleEstado As String
    Public Property Nombre As String
    Public Property Direccion As String
    Public Property CodPostal As String
    Public Property Poblacion As String
    Public Property Provincia As String
    Public Property Telefono As String
    Public Property Movil As String
    Public Property Observaciones As String
    Public Property CodigoBarras As String
    Public Property Reembolso As Decimal

    Public Property Decision As String
    Public Property EnvioNuevo As Integer?
    Public Property EstadoEnvioNuevo As Short?
    Public Property EnvioNuevoConEtiqueta As Boolean
    Public Property PortesIncluidos As Boolean
    Public Property UsuarioDecision As String
    Public Property FechaDecision As Date?

    Public ReadOnly Property SinDecidir As Boolean
        Get
            Return String.IsNullOrWhiteSpace(Decision) OrElse Decision = SIN_DECIDIR
        End Get
    End Property

    Public ReadOnly Property TextoEstado As String
        Get
            Return If(Estado = ESTADO_DEVUELTO, "Devuelto", If(Estado = ESTADO_INCIDENTADO, "Incidentado", $"Estado {Estado}"))
        End Get
    End Property

    ''' <summary>Lo que ve el almacén: qué hacer con la caja cuando llegue.</summary>
    Public ReadOnly Property TextoDecision As String
        Get
            If SinDecidir Then
                Return "Sin decidir"
            End If
            If Decision = ABONAR Then
                Return "Abonar"
            End If
            If Decision = REENVIAR Then
                Dim portes As String = If(PortesIncluidos, " (con portes)", String.Empty)
                Return If(EnvioNuevoConEtiqueta,
                          $"Reenviado en el envío {EnvioNuevo}{portes}",
                          $"REENVIAR: envío {EnvioNuevo} listo para imprimir la etiqueta{portes}")
            End If
            Return Decision
        End Get
    End Property

End Class

''' <summary>NestoAPI#699: el cuerpo de POST api/EnviosAgencias/{id}/Reenviar (mismos nombres que ReenviarEnvioDTO).</summary>
Public Class ReenvioEnvioPeticion
    Public Property Direccion As String
    Public Property CodPostal As String
    Public Property Poblacion As String
    Public Property Provincia As String
    Public Property Telefono As String
    Public Property Movil As String
    Public Property Observaciones As String
    Public Property IncluirPortes As Boolean
End Class

''' <summary>NestoAPI#699: la respuesta de Reenviar y de Abonar (DecisionReenvioDTO).</summary>
Public Class DecisionReenvioModel
    Public Property EnvioOrigen As Integer
    Public Property Pedido As Integer?
    Public Property Decision As String
    Public Property EnvioNuevo As Integer?
    Public Property CodigoBarras As String
    Public Property PortesIncluidos As Boolean
    Public Property Reembolso As Decimal
    Public Property Avisos As List(Of String) = New List(Of String)
    Public Property Mensaje As String
End Class
