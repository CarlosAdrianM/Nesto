Imports System.Globalization

''' <summary>
''' Nesto#640 (NestoAPI#864): GET api/PedidosVenta/{empresa}/{pedido}/PagoAPlazos/Disponible. La financiera es un dato
''' (<see cref="NombreProveedor"/>): aquí no se nombra a ninguna.
''' </summary>
Public Class DisponibilidadPagoAPlazosModel
    Public Property Disponible As Boolean
    ''' <summary>Por qué no (de usted). Nothing si está disponible.</summary>
    Public Property Motivo As String
    ''' <summary>Código del proveedor. Nothing con el interruptor apagado o sin proveedor: entonces no se ofrece nada.</summary>
    Public Property Proveedor As String
    Public Property NombreProveedor As String
    ''' <summary>Lo que se financiaría: lo pendiente de pagar del pedido, con IVA.</summary>
    Public Property Importe As Decimal
    Public Property ImporteMinimo As Decimal?
    Public Property ImporteMaximo As Decimal?
    Public Property DiasPlazoCaptura As Integer?
    Public Property ClienteTieneApp As Boolean
    ''' <summary>El canal sugerido: «App» o «Enlace».</summary>
    Public Property Canal As String
    ''' <summary>Algo que conviene saber (no impide nada).</summary>
    Public Property Aviso As String
    Public Property Pruebas As Boolean
    ''' <summary>Mensaje listo para pegar con el enlace para instalar la app. Nothing si el cliente ya la tiene (o la API es anterior).</summary>
    Public Property InvitacionApp As String
End Class

''' <summary>Nesto#640: GET …/PagoAPlazos/Estado y la respuesta del DELETE.</summary>
Public Class EstadoPagoAPlazosModel
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property HaySolicitud As Boolean
    Public Property IdSolicitud As Integer?
    ''' <summary>SinSolicitud, Pendiente, Aprobado, Capturado, Cancelado, Caducado o Rechazado.</summary>
    Public Property Estado As String
    Public Property Proveedor As String
    Public Property NombreProveedor As String
    Public Property Canal As String
    Public Property Importe As Decimal?
    Public Property ImporteCapturado As Decimal?
    Public Property FechaSolicitud As Date?
    Public Property FechaAprobacion As Date?
    Public Property FechaLimiteCaptura As Date?
    Public Property DiasParaCapturar As Integer?
    Public Property FechaCaptura As Date?
    Public Property FechaCierre As Date?
    Public Property Motivo As String
    Public Property Usuario As String
    ''' <summary>El enlace para el cliente (solo pendiente y por el canal Enlace).</summary>
    Public Property UrlPago As String
    Public Property Pruebas As Boolean
End Class

''' <summary>Nesto#640: la respuesta del POST …/PagoAPlazos: el estado y qué se ha hecho para que el cliente se entere.</summary>
Public Class SolicitudPagoAPlazosModel
    Inherits EstadoPagoAPlazosModel

    Public Property DispositivosAvisados As Integer
    Public Property Aviso As String
    ''' <summary>Mensaje listo para pegar con el enlace para instalar la app. Nothing si el cliente ya la tiene (o la API es anterior).</summary>
    Public Property InvitacionApp As String
    ''' <summary>
    ''' Con enlace de pago, el mensaje completo para el cliente: el enlace y, si no tiene la app, la invitación debajo. Nothing sin
    ''' enlace (o con una API anterior: entonces se copia solo el enlace).
    ''' </summary>
    Public Property MensajeCliente As String
End Class

''' <summary>
''' Nesto#640: el interruptor del pago a plazos tal y como lo tiene la API en ParametrosUsuario de «(defecto)». Es solo una PISTA
''' para pintar la casilla de la plantilla de venta ANTES de que el pedido exista; la verdad la da siempre
''' GET …/PagoAPlazos/Disponible del pedido ya grabado.
''' </summary>
Public Class InterruptorPagoAPlazosModel
    ''' <summary>«Activo», «Pruebas» o cualquier otra cosa (apagado).</summary>
    Public Property Modo As String
    ''' <summary>En modo Pruebas, los clientes a los que se les ofrece (separados por comas).</summary>
    Public Property ClientesPrueba As String
End Class

''' <summary>
''' Nesto#640 (NestoAPI#864): lo que enseña Nesto del pago a plazos de un pedido. Las reglas de verdad (si se puede, los límites,
''' los plazos) son de la API; aquí solo cuándo se pinta la acción y los textos.
''' </summary>
Public NotInheritable Class ReglasPagoAPlazos
    Private Sub New()
    End Sub

    Public Const CANAL_APP As String = "App"
    Public Const CANAL_ENLACE As String = "Enlace"

    Public Const ESTADO_SIN_SOLICITUD As String = "SinSolicitud"
    Public Const ESTADO_PENDIENTE As String = "Pendiente"
    Public Const ESTADO_APROBADO As String = "Aprobado"
    Public Const ESTADO_CAPTURADO As String = "Capturado"
    Public Const ESTADO_CANCELADO As String = "Cancelado"
    Public Const ESTADO_CADUCADO As String = "Caducado"
    Public Const ESTADO_RECHAZADO As String = "Rechazado"

    Public Const TITULO As String = "Pago a plazos"
    Public Const TITULO_CANCELAR As String = "Cancelar pago a plazos"
    Public Const TEXTO_FALTA_MOTIVO As String = "Para cancelar el pago a plazos hay que indicar el motivo."
    Public Const TEXTO_ENLACE_COPIADO As String = "Enlace copiado. Péguelo en el mensaje para el cliente."
    Public Const TEXTO_MENSAJE_COPIADO As String = "Mensaje copiado. Péguelo en el mensaje para el cliente."
    Public Const TEXTO_INVITACION_COPIADA As String = "Invitación copiada. Péguela en el mensaje para el cliente."
    Public Const TEXTO_COPIAR_INVITACION As String = "Copiar invitación a la app"
    Public Const AYUDA_COPIAR_INVITACION As String = "Copia un mensaje para el cliente con el enlace para instalar la app"
    Public Const TEXTO_PRUEBAS As String = "Entorno de pruebas de la financiera: no se mueve dinero."
    Private Const NOMBRE_POR_DEFECTO As String = "la financiera"

    Private Shared ReadOnly Castellano As CultureInfo = CultureInfo.GetCultureInfo("es-ES")

    Private Shared Function Es(estado As String, cual As String) As Boolean
        Return String.Equals(estado?.Trim(), cual, StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared Function Euros(importe As Decimal) As String
        Return importe.ToString("N2", Castellano) & " €"
    End Function

    ''' <summary>El nombre para pintar: el de la API o, si no viene, «la financiera».</summary>
    Public Shared Function NombreProveedor(nombre As String) As String
        Return If(String.IsNullOrWhiteSpace(nombre), NOMBRE_POR_DEFECTO, nombre.Trim())
    End Function

#Region "La acción «Pago a plazos…»"
    ''' <summary>
    ''' La acción se pinta solo si la API dice con quién se financiaría: con el interruptor apagado (o sin proveedor) la
    ''' respuesta viene sin proveedor y no sale nada. Sin respuesta (503, error), tampoco.
    ''' </summary>
    Public Shared Function SeOfrece(disponibilidad As DisponibilidadPagoAPlazosModel) As Boolean
        Return disponibilidad IsNot Nothing AndAlso
            Not (String.IsNullOrWhiteSpace(disponibilidad.Proveedor) AndAlso String.IsNullOrWhiteSpace(disponibilidad.NombreProveedor))
    End Function

    ''' <summary>Se ofrece y además este pedido se puede financiar ahora.</summary>
    Public Shared Function PuedePedirse(disponibilidad As DisponibilidadPagoAPlazosModel) As Boolean
        Return SeOfrece(disponibilidad) AndAlso disponibilidad.Disponible
    End Function

    ''' <summary>El ToolTip del botón: qué hace o, si no se puede, el motivo de la API tal cual.</summary>
    Public Shared Function TextoAyuda(disponibilidad As DisponibilidadPagoAPlazosModel) As String
        If Not SeOfrece(disponibilidad) Then
            Return String.Empty
        End If
        If disponibilidad.Disponible Then
            Return $"Financiar {Euros(disponibilidad.Importe)} con {NombreProveedor(disponibilidad.NombreProveedor)}: " &
                "el cliente termina el pago en su app o con un enlace."
        End If
        Return If(String.IsNullOrWhiteSpace(disponibilidad.Motivo), "Este pedido no se puede pagar a plazos.", disponibilidad.Motivo.Trim())
    End Function

    ''' <summary>«App» solo si el cliente la tiene y la API la sugiere; si no, «Enlace».</summary>
    Public Shared Function CanalInicial(disponibilidad As DisponibilidadPagoAPlazosModel) As String
        Return If(disponibilidad IsNot Nothing AndAlso disponibilidad.ClienteTieneApp AndAlso Es(disponibilidad.Canal, CANAL_APP),
                  CANAL_APP, CANAL_ENLACE)
    End Function

    ''' <summary>Lo que se le dice al usuario al abrirse la solicitud (el aviso de la API va aparte).</summary>
    Public Shared Function TextoResultado(solicitud As SolicitudPagoAPlazosModel) As String
        If solicitud Is Nothing Then
            Return String.Empty
        End If
        Dim hayEnlace As Boolean = Not String.IsNullOrWhiteSpace(solicitud.UrlPago)
        If Es(solicitud.Canal, CANAL_APP) Then
            If solicitud.DispositivosAvisados > 0 Then
                Dim aCuantos As String = If(solicitud.DispositivosAvisados = 1, "1 dispositivo", $"{solicitud.DispositivosAvisados} dispositivos")
                Return $"Aviso enviado a la app del cliente ({aCuantos}): el cliente termina el pago ahí."
            End If
            Return If(hayEnlace,
                      "El aviso no ha llegado a ningún dispositivo del cliente. Copie el enlace y mándeselo usted.",
                      "El aviso no ha llegado a ningún dispositivo del cliente.")
        End If
        Return If(hayEnlace,
                  "Solicitud creada. Copie el enlace y mándeselo al cliente: el pago no empieza hasta que lo abra y lo termine.",
                  "Solicitud creada.")
    End Function
#End Region

#Region "La plantilla de venta"
    Public Const TEXTO_CASILLA_PLANTILLA As String = "Ofrecer el pago a plazos al grabar el pedido"
    Public Const AYUDA_CASILLA_PLANTILLA As String = "Al grabar el pedido se abre «Pago a plazos» para ese pedido: el cliente termina el pago en su app o con un enlace. " &
        "Si el pedido no se puede pagar a plazos, se le dirá el motivo; el pedido se graba igual. No se aplica al modificar ni al ampliar un pedido: en ese caso, pídalo desde el propio pedido."
    Public Const TEXTO_PLANTILLA_SOLO_PEDIDO_NUEVO As String = "El pedido se ha grabado. El pago a plazos de un pedido modificado o ampliado se pide desde el propio pedido, con el botón «Pago a plazos…»."

    ''' <summary>
    ''' Si la casilla de la plantilla se pinta para ese cliente: con el interruptor «Activo», a todos; en «Pruebas», solo a los
    ''' clientes de prueba; apagado, sin fila o sin poder leerlo, a nadie (la misma regla que aplica la API). Es una pista: lo
    ''' que se puede hacer de verdad lo dice la API con el pedido ya grabado.
    ''' </summary>
    Public Shared Function SeOfreceAntesDeGrabar(interruptor As InterruptorPagoAPlazosModel, cliente As String) As Boolean
        If interruptor Is Nothing Then
            Return False
        End If
        If Es(interruptor.Modo, "Activo") Then
            Return True
        End If
        If Not (Es(interruptor.Modo, "Pruebas") OrElse Es(interruptor.Modo, "Sandbox")) OrElse String.IsNullOrWhiteSpace(cliente) Then
            Return False
        End If
        Dim clientes As String() = If(interruptor.ClientesPrueba, String.Empty).Split({","c, ";"c, " "c}, StringSplitOptions.RemoveEmptyEntries)
        Return clientes.Any(Function(c) String.Equals(c.Trim(), cliente.Trim(), StringComparison.OrdinalIgnoreCase))
    End Function

    ''' <summary>Lo que se dice tras grabar desde la plantilla si ese pedido no se puede pagar a plazos: el motivo de la API tal cual.</summary>
    Public Shared Function TextoNoSePuedeTrasGrabar(pedido As Integer, disponibilidad As DisponibilidadPagoAPlazosModel) As String
        Dim motivo As String = If(String.IsNullOrWhiteSpace(disponibilidad?.Motivo), "Ahora mismo no se ofrece el pago a plazos.", disponibilidad.Motivo.Trim())
        Return $"El pedido {pedido} se ha grabado, pero no se puede pagar a plazos." & vbCrLf & vbCrLf & motivo
    End Function

    ''' <summary>Lo que se dice si, con el pedido ya grabado, la API no contesta si se puede pagar a plazos.</summary>
    Public Shared Function TextoNoSeHaPodidoComprobarTrasGrabar(pedido As Integer, fallo As String) As String
        Dim detalle As String = If(String.IsNullOrWhiteSpace(fallo), String.Empty, vbCrLf & vbCrLf & fallo.Trim())
        Return $"El pedido {pedido} se ha grabado, pero no se ha podido comprobar si se puede pagar a plazos. " &
            "Puede pedirlo desde el propio pedido, con el botón «Pago a plazos…»." & detalle
    End Function
#End Region

#Region "El estado"
    Public Shared Function HaySolicitud(estado As EstadoPagoAPlazosModel) As Boolean
        Return estado IsNot Nothing AndAlso estado.HaySolicitud AndAlso Not String.IsNullOrWhiteSpace(estado.Estado) AndAlso
            Not Es(estado.Estado, ESTADO_SIN_SOLICITUD)
    End Function

    ''' <summary>Viva = se puede cancelar: pendiente del cliente o aprobada sin cobrar.</summary>
    Public Shared Function EsViva(estado As EstadoPagoAPlazosModel) As Boolean
        Return HaySolicitud(estado) AndAlso (Es(estado.Estado, ESTADO_PENDIENTE) OrElse Es(estado.Estado, ESTADO_APROBADO))
    End Function

    Public Shared Function EstaAprobado(estado As EstadoPagoAPlazosModel) As Boolean
        Return HaySolicitud(estado) AndAlso Es(estado.Estado, ESTADO_APROBADO)
    End Function

    ''' <summary>«Pago a plazos (Financiera): aprobado, a capturar antes del 20/11». Vacío si el pedido no tiene solicitud.</summary>
    Public Shared Function TextoEstado(estado As EstadoPagoAPlazosModel) As String
        If Not HaySolicitud(estado) Then
            Return String.Empty
        End If
        Dim cabecera As String = If(String.IsNullOrWhiteSpace(estado.NombreProveedor), TITULO, $"{TITULO} ({estado.NombreProveedor.Trim()})")
        Return $"{cabecera}: {TextoDelEstado(estado)}"
    End Function

    Private Shared Function TextoDelEstado(estado As EstadoPagoAPlazosModel) As String
        If Es(estado.Estado, ESTADO_PENDIENTE) Then
            Return "pendiente del cliente"
        End If
        If Es(estado.Estado, ESTADO_APROBADO) Then
            Return If(estado.FechaLimiteCaptura.HasValue,
                      $"aprobado, a capturar antes del {estado.FechaLimiteCaptura.Value.ToString("dd/MM", Castellano)}",
                      "aprobado")
        End If
        If Es(estado.Estado, ESTADO_CAPTURADO) Then
            Return "capturado"
        End If
        If Es(estado.Estado, ESTADO_RECHAZADO) Then
            Return "rechazado"
        End If
        If Es(estado.Estado, ESTADO_CADUCADO) Then
            Return "caducado"
        End If
        If Es(estado.Estado, ESTADO_CANCELADO) Then
            Return "cancelado"
        End If
        Return estado.Estado.Trim().ToLower(Castellano)
    End Function

    ''' <summary>El ToolTip del estado: importe, canal, fechas, quién lo pidió y el motivo, una cosa por línea.</summary>
    Public Shared Function TextoDetalleEstado(estado As EstadoPagoAPlazosModel) As String
        If Not HaySolicitud(estado) Then
            Return String.Empty
        End If
        Dim lineas As New List(Of String)
        If estado.Importe.HasValue Then
            lineas.Add($"Importe: {Euros(estado.Importe.Value)}")
        End If
        If Es(estado.Canal, CANAL_APP) Then
            lineas.Add("Canal: la app del cliente")
        ElseIf Es(estado.Canal, CANAL_ENLACE) Then
            lineas.Add("Canal: enlace")
        End If
        If estado.FechaSolicitud.HasValue Then
            Dim quien As String = If(String.IsNullOrWhiteSpace(estado.Usuario), String.Empty, $" por {estado.Usuario.Trim()}")
            lineas.Add($"Pedido el {FechaHora(estado.FechaSolicitud.Value)}{quien}")
        End If
        If estado.FechaAprobacion.HasValue Then
            lineas.Add($"Aprobado el {FechaHora(estado.FechaAprobacion.Value)}")
        End If
        If Es(estado.Estado, ESTADO_APROBADO) AndAlso estado.FechaLimiteCaptura.HasValue Then
            Dim dias As String = String.Empty
            If estado.DiasParaCapturar.HasValue Then
                dias = If(estado.DiasParaCapturar.Value = 1, " (queda 1 día)", $" (quedan {estado.DiasParaCapturar.Value} días)")
            End If
            lineas.Add($"El pedido tiene que salir antes del {estado.FechaLimiteCaptura.Value.ToString("dd/MM/yyyy", Castellano)}{dias}")
        End If
        If estado.FechaCaptura.HasValue Then
            Dim cuanto As String = If(estado.ImporteCapturado.HasValue, $": {Euros(estado.ImporteCapturado.Value)}", String.Empty)
            lineas.Add($"Capturado el {FechaHora(estado.FechaCaptura.Value)}{cuanto}")
        End If
        If estado.FechaCierre.HasValue AndAlso Not Es(estado.Estado, ESTADO_CAPTURADO) Then
            lineas.Add($"Cerrado el {FechaHora(estado.FechaCierre.Value)}")
        End If
        If Not String.IsNullOrWhiteSpace(estado.Motivo) Then
            lineas.Add(estado.Motivo.Trim())
        End If
        If estado.Pruebas Then
            lineas.Add(TEXTO_PRUEBAS)
        End If
        Return String.Join(vbCrLf, lineas)
    End Function

    Private Shared Function FechaHora(fecha As Date) As String
        Return fecha.ToString("dd/MM/yyyy HH:mm", Castellano)
    End Function
#End Region

#Region "Cancelar"
    ''' <summary>Lo que se pregunta al pedir el motivo: qué pasa si se cancela, según cómo esté.</summary>
    Public Shared Function TextoPedirMotivoCancelacion(estado As EstadoPagoAPlazosModel) As String
        Dim nombre As String = NombreProveedor(estado?.NombreProveedor)
        Dim consecuencia As String = If(EstaAprobado(estado),
            $"El pago ya está aprobado: se cancelará en {nombre} y se quitará el prepago del pedido.",
            "El cliente ya no podrá terminar el pago con el aviso o el enlace que tiene.")
        Return consecuencia & vbCrLf & vbCrLf & "Indique el motivo de la cancelación:"
    End Function
#End Region
End Class
