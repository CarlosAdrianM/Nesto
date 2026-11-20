Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Services
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

''' <summary>
''' Carlos 20/11/26: acciones sobre los envíos del pedido en la pestaña «Entrega» (las mismas que Agencias › Tramitados):
''' «Añadir nota» a la historia de un envío. Nada de esto rompe la carga del pedido.
''' </summary>
Partial Public Class DetallePedidoViewModel

    ''' <summary>Friend para los tests (en producción se crea al primer uso con la factoría de la API).</summary>
    Friend Property ServicioDeNotasEnvio As IServicioNotasEnvio

    Private _servicioAutenticacionAccionesEnvio As IServicioAutenticacion

    Public Property AnadirNotaEnvioCommand As AsyncRelayCommand(Of EnvioAgenciaDTO)

    Private Sub InicializarAccionesEnvio(servicioAutenticacion As IServicioAutenticacion)
        _servicioAutenticacionAccionesEnvio = servicioAutenticacion
        AnadirNotaEnvioCommand = New AsyncRelayCommand(Of EnvioAgenciaDTO)(AddressOf AnadirNotaEnvioAsync)
    End Sub

    Private Function ServicioNotasEnvio() As IServicioNotasEnvio
        If ServicioDeNotasEnvio Is Nothing AndAlso configuracion IsNot Nothing Then
            ServicioDeNotasEnvio = New ControlesUsuario.Services.ServicioNotasEnvio(New ClienteApiFactory(configuracion.servidorAPI, _servicioAutenticacionAccionesEnvio))
        End If
        Return ServicioDeNotasEnvio
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
End Class
