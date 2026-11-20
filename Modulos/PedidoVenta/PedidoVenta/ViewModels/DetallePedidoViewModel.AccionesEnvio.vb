Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Services
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

''' <summary>
''' Carlos 20/11/26: acciones sobre los envíos del pedido en la pestaña «Entrega» (las mismas que Agencias › Tramitados):
''' «Añadir nota» a la historia de un envío y, si el pedido es una nota de recogida, «Anular recogida» (caso NR930553, parada 251104:
''' una recogida que ya no hacía falta y no se podía anular de ninguna manera). Nada de esto rompe la carga del pedido.
''' </summary>
Partial Public Class DetallePedidoViewModel

    ''' <summary>Friend para los tests (en producción se crea al primer uso con la factoría de la API).</summary>
    Friend Property ServicioDeNotasEnvio As IServicioNotasEnvio
    ''' <summary>Friend para los tests (en producción se crea al primer uso con la factoría de la API).</summary>
    Friend Property ServicioDeRecogidasReparto As IServicioRecogidasReparto

    Private _servicioAutenticacionAccionesEnvio As IServicioAutenticacion

    Public Property AnadirNotaEnvioCommand As AsyncRelayCommand(Of EnvioAgenciaDTO)
    Public Property AnularRecogidaNotaCommand As AsyncRelayCommand

    Private Sub InicializarAccionesEnvio(servicioAutenticacion As IServicioAutenticacion)
        _servicioAutenticacionAccionesEnvio = servicioAutenticacion
        AnadirNotaEnvioCommand = New AsyncRelayCommand(Of EnvioAgenciaDTO)(AddressOf AnadirNotaEnvioAsync)
        AnularRecogidaNotaCommand = New AsyncRelayCommand(AddressOf AnularRecogidaNotaAsync, Function() PuedeAnularRecogidaNota)
    End Sub

    Private Function ServicioNotasEnvio() As IServicioNotasEnvio
        If ServicioDeNotasEnvio Is Nothing AndAlso configuracion IsNot Nothing Then
            ServicioDeNotasEnvio = New ControlesUsuario.Services.ServicioNotasEnvio(New ClienteApiFactory(configuracion.servidorAPI, _servicioAutenticacionAccionesEnvio))
        End If
        Return ServicioDeNotasEnvio
    End Function

    Private Function ServicioRecogidasReparto() As IServicioRecogidasReparto
        If ServicioDeRecogidasReparto Is Nothing AndAlso configuracion IsNot Nothing Then
            ServicioDeRecogidasReparto = New ControlesUsuario.Services.ServicioRecogidasReparto(New ClienteApiFactory(configuracion.servidorAPI, _servicioAutenticacionAccionesEnvio))
        End If
        Return ServicioDeRecogidasReparto
    End Function

    ''' <summary>«Añadir nota» de la fila: pide el texto, lo guarda y lo dice (el error, de usted). Friend para los tests.</summary>
    Friend Async Function AnadirNotaEnvioAsync(envio As EnvioAgenciaDTO) As Task
        Dim servicioNotas As IServicioNotasEnvio = ServicioNotasEnvio()
        If envio Is Nothing OrElse envio.Numero <= 0 OrElse servicioNotas Is Nothing Then
            Return
        End If
        Dim anotador As New AnotadorEnvios(servicioNotas, dialogService)
        Dim numeroPedido As Integer? = If(pedido Is Nothing OrElse pedido.numero <= 0, CType(Nothing, Integer?), pedido.numero)
        Await anotador.AnadirNota(envio.Numero, numeroPedido)
    End Function

    ''' <summary>
    ''' «Anular recogida»: el pedido guardado es una nota de recogida y quien mira es de Almacén, Administración, Dirección o
    ''' Informática (la API lo comprueba igual). Si la recogida no está en reparto o ya está recogida, lo dice la API.
    ''' </summary>
    Public ReadOnly Property PuedeAnularRecogidaNota As Boolean
        Get
            Return EsNotaRecogida AndAlso pedido IsNot Nothing AndAlso pedido.numero > 0 AndAlso AnuladorRecogidas.PuedeAnular(configuracion)
        End Get
    End Property

    Private Sub NotificarCambioAnularRecogida()
        OnPropertyChanged(NameOf(PuedeAnularRecogidaNota))
        AnularRecogidaNotaCommand?.NotifyCanExecuteChanged()
    End Sub

    ''' <summary>La parada de la nota (envío del reparto propio Tramitado o Incidentado), para nombrarla en la pregunta; o Nothing.</summary>
    Friend Shared Function ParadaDeLaNota(envios As IEnumerable(Of EnvioAgenciaDTO)) As EnvioAgenciaDTO
        Return envios?.FirstOrDefault(Function(e) e IsNot Nothing AndAlso e.AgenciaId = Constantes.Agencias.AGENCIA_REPARTO_PROPIO AndAlso
                                          (e.Estado = Constantes.Agencias.ESTADO_TRAMITADO_ENVIO OrElse e.Estado = Constantes.Agencias.ESTADO_INCIDENTADO_ENVIO))
    End Function

    ''' <summary>Pide motivo y confirmación, anula y vuelve a leer los envíos del pedido. Friend para los tests.</summary>
    Friend Async Function AnularRecogidaNotaAsync() As Task
        Dim servicioRecogidas As IServicioRecogidasReparto = ServicioRecogidasReparto()
        If Not PuedeAnularRecogidaNota OrElse servicioRecogidas Is Nothing Then
            Return
        End If
        Dim numeroPedido As Integer = pedido.numero
        Dim parada As EnvioAgenciaDTO = ParadaDeLaNota(ListaEnlacesSeguimiento)
        Dim anulador As New AnuladorRecogidas(servicioRecogidas, dialogService)
        Dim anulada As AnulacionRecogidaModel = Await anulador.Anular(numeroPedido, parada?.Numero, ClienteCompleto?.nombre)
        If anulada Is Nothing OrElse pedido Is Nothing OrElse pedido.numero <> numeroPedido Then
            Return
        End If
        Try
            ListaEnlacesSeguimiento = Await servicio.CargarEnlacesSeguimiento(pedido.empresa, numeroPedido)
        Catch ex As Exception
            ' Ya está anulada: los envíos se verán al volver a abrir el pedido
        End Try
    End Function
End Class
