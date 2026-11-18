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

    ' NestoAPI#870: si la caja ha vuelto físicamente a Algete (la lectura de Ariadna o «Ya está aquí») y si hay un reenvío
    ' esperándola (decisión Reenviar, el reenvío sin tramitar y la caja sin volver: la API no lo deja salir).
    Public Property HaVuelto As Boolean
    Public Property FechaHaVuelto As Date?
    Public Property UsuarioHaVuelto As String
    ''' <summary>«Lectura» (Ariadna), «YaEstaAqui» (botón de Nesto) o «AlImprimir» (forzado al imprimir el reenvío).</summary>
    Public Property ComoHaVuelto As String
    Public Property ReenvioEsperandoCaja As Boolean

    ''' <summary>
    ''' Sugerencia 1329: lo calcula la API (ReglaDevueltoResuelto): la caja ha vuelto, hay decisión y, si es Reenviar, el reenvío ya
    ''' está tramitado. No queda nada por hacer. Con una API anterior no llega y vale False (se ve todo, como antes).
    ''' </summary>
    Public Property Resuelto As Boolean

    ''' <summary>
    ''' Sugerencia 1329: lo que enseña la pestaña Devueltos: por defecto solo lo que tiene algo por hacer; con
    ''' <paramref name="verResueltos"/>, todo. Conserva el orden de la API.
    ''' </summary>
    Public Shared Function Visibles(lista As IEnumerable(Of EnvioDevueltoModel), verResueltos As Boolean) As List(Of EnvioDevueltoModel)
        If lista Is Nothing Then
            Return New List(Of EnvioDevueltoModel)
        End If
        Return lista.Where(Function(d) d IsNot Nothing AndAlso (verResueltos OrElse Not d.Resuelto)).ToList()
    End Function

    ''' <summary>NestoAPI#870: la columna «Caja»: esperando, ha vuelto (con día y hora) o nada.</summary>
    Public ReadOnly Property TextoCaja As String
        Get
            If HaVuelto Then
                Return If(FechaHaVuelto.HasValue, $"Ha vuelto ({FechaHaVuelto.Value:dd/MM HH:mm})", "Ha vuelto")
            End If
            If ReenvioEsperandoCaja Then
                Return "Esperando la caja"
            End If
            Return String.Empty
        End Get
    End Property

    ''' <summary>NestoAPI#870: el tooltip de la columna «Caja»: quién y cómo la dio por vuelta.</summary>
    Public ReadOnly Property DetalleCaja As String
        Get
            If HaVuelto Then
                Dim como As String = TextoComoHaVuelto(ComoHaVuelto)
                Dim quien As String = If(String.IsNullOrWhiteSpace(UsuarioHaVuelto), String.Empty, $" por {UsuarioHaVuelto.Trim()}")
                Dim cuando As String = If(FechaHaVuelto.HasValue, $" el {FechaHaVuelto.Value:dd/MM/yyyy HH:mm}", String.Empty)
                Return $"Ha vuelto{cuando}{quien}{como}"
            End If
            If ReenvioEsperandoCaja Then
                Return $"El reenvío {EnvioNuevo} no puede salir hasta que vuelva esta caja. Cuando llegue, léala en Ariadna («Ha vuelto un paquete») o pulse «Ya está aquí»."
            End If
            Return Nothing
        End Get
    End Property

    Friend Shared Function TextoComoHaVuelto(como As String) As String
        Select Case como
            Case "Lectura" : Return " (leída en Ariadna)"
            Case "YaEstaAqui" : Return " (botón «Ya está aquí»)"
            Case "AlImprimir" : Return " (al imprimir el reenvío)"
            Case Else : Return String.Empty
        End Select
    End Function

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
