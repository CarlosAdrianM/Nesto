Imports System.Collections.ObjectModel
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Services
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

''' <summary>
''' Sugerencia 1356 (Laura, 19/11/26): la notificación «Envío devuelto del pedido X: ¿lo sigue queriendo el cliente?» abre el
''' pedido; la decisión se toma aquí, en la pestaña «Entrega», sin ir a Agencias › Devueltos (Carlos: «para que no todo el mundo
''' ande tocando en Agencias»). Mismas acciones y el mismo paso que Devueltos (<see cref="DecisorEnviosDevueltos"/>): «Volver a
''' enviar…» y «Abonar». Solo se pregunta a la API si algún envío del pedido está incidentado o devuelto; si ninguno espera
''' decisión, no se ve nada. Nada de esto rompe la carga del pedido.
''' </summary>
Partial Public Class DetallePedidoViewModel

    ''' <summary>Friend para los tests (en producción se crea al primer uso con la factoría de la API).</summary>
    Friend Property ServicioDeDevueltos As IServicioEnviosDevueltos

    Private _servicioAutenticacionDevueltos As IServicioAutenticacion
    Private _versionDevueltos As Integer

    Public Property ReenviarDevueltoCommand As AsyncRelayCommand(Of EnvioDevueltoModel)
    Public Property AbonarDevueltoCommand As AsyncRelayCommand(Of EnvioDevueltoModel)

    Private Sub InicializarDevueltos(servicioAutenticacion As IServicioAutenticacion)
        _servicioAutenticacionDevueltos = servicioAutenticacion
        ReenviarDevueltoCommand = New AsyncRelayCommand(Of EnvioDevueltoModel)(AddressOf ReenviarDevueltoAsync)
        AbonarDevueltoCommand = New AsyncRelayCommand(Of EnvioDevueltoModel)(AddressOf AbonarDevueltoAsync)
    End Sub

    Private Function ServicioDevueltos() As IServicioEnviosDevueltos
        If ServicioDeDevueltos Is Nothing AndAlso configuracion IsNot Nothing Then
            ServicioDeDevueltos = New ControlesUsuario.Services.ServicioEnviosDevueltos(New ClienteApiFactory(configuracion.servidorAPI, _servicioAutenticacionDevueltos))
        End If
        Return ServicioDeDevueltos
    End Function

    Private _devueltosPendientes As New ObservableCollection(Of EnvioDevueltoModel)
    ''' <summary>Los envíos de este pedido devueltos o incidentados que esperan decisión (normalmente uno).</summary>
    Public Property DevueltosPendientes As ObservableCollection(Of EnvioDevueltoModel)
        Get
            Return _devueltosPendientes
        End Get
        Private Set(value As ObservableCollection(Of EnvioDevueltoModel))
            If SetProperty(_devueltosPendientes, value) Then
                OnPropertyChanged(NameOf(HayDevueltosPendientes))
            End If
        End Set
    End Property

    Public ReadOnly Property HayDevueltosPendientes As Boolean
        Get
            Return _devueltosPendientes IsNot Nothing AndAlso _devueltosPendientes.Count > 0
        End Get
    End Property

    ''' <summary>Si merece la pena preguntar: algún envío del pedido está incidentado (3) o devuelto (4).</summary>
    Friend Shared Function HayEnviosDevueltosOIncidentados(envios As IEnumerable(Of EnvioAgenciaDTO)) As Boolean
        Return envios IsNot Nothing AndAlso envios.Any(Function(e) e IsNot Nothing AndAlso
                                                          (e.Estado = EnvioDevueltoModel.ESTADO_INCIDENTADO OrElse e.Estado = EnvioDevueltoModel.ESTADO_DEVUELTO))
    End Function

    ''' <summary>
    ''' Lee de la API (GET api/EnviosAgencias/Devueltos, la lista de Agencias) los envíos de este pedido que esperan decisión. Nunca
    ''' lanza: si falla (sin permiso, sin script, sin red) no se enseña nada. Si mientras tanto se abre otro pedido, la respuesta del
    ''' anterior se descarta. Friend para los tests.
    ''' </summary>
    Friend Async Function CargarDevueltosDelPedidoAsync() As Task
        _versionDevueltos += 1
        Dim version As Integer = _versionDevueltos
        Dim modelo As Nesto.Models.PedidoVentaDTO = pedido?.Model
        If modelo Is Nothing OrElse modelo.numero <= 0 OrElse Not HayEnviosDevueltosOIncidentados(ListaEnlacesSeguimiento) Then
            DevueltosPendientes = New ObservableCollection(Of EnvioDevueltoModel)
            Return
        End If
        Dim lector As IServicioEnviosDevueltos = ServicioDevueltos()
        If lector Is Nothing Then
            Return
        End If
        Dim empresa As String = modelo.empresa
        Dim numero As Integer = modelo.numero
        Dim pendientes As List(Of EnvioDevueltoModel)
        Try
            pendientes = EnvioDevueltoModel.PendientesDeDecidirDelPedido(Await lector.Cargar(empresa), empresa, numero)
        Catch ex As Exception
            pendientes = New List(Of EnvioDevueltoModel)
        End Try
        If version = _versionDevueltos Then
            DevueltosPendientes = New ObservableCollection(Of EnvioDevueltoModel)(pendientes)
        End If
    End Function

    Private Function Decisor() As DecisorEnviosDevueltos
        Return New DecisorEnviosDevueltos(ServicioDevueltos(), dialogService, ocupado:=Sub(ocupado) estaBloqueado = ocupado)
    End Function

    ''' <summary>«El cliente lo sigue queriendo: volver a enviar…» (el diálogo de Devueltos, con la dirección para corregirla).</summary>
    Friend Async Function ReenviarDevueltoAsync(devuelto As EnvioDevueltoModel) As Task
        If devuelto Is Nothing OrElse ServicioDevueltos() Is Nothing Then
            Return
        End If
        If Await Decisor().Reenviar(devuelto) = ResultadoDecisionDevuelto.Decidido Then
            Await RecargarEnviosTrasDecidirAsync()
        End If
    End Function

    ''' <summary>«No lo quiere: abonar». Solo lo apunta: el abono se hace con Copiar factura.</summary>
    Friend Async Function AbonarDevueltoAsync(devuelto As EnvioDevueltoModel) As Task
        If devuelto Is Nothing OrElse ServicioDevueltos() Is Nothing Then
            Return
        End If
        If Await Decisor().Abonar(devuelto) = ResultadoDecisionDevuelto.Decidido Then
            Await RecargarEnviosTrasDecidirAsync()
        End If
    End Function

    ''' <summary>Tras decidir: la lista de envíos (el reenvío nuevo sale en ella) y, con ella, el bloque de devueltos.</summary>
    Private Async Function RecargarEnviosTrasDecidirAsync() As Task
        Dim modelo As Nesto.Models.PedidoVentaDTO = pedido?.Model
        If modelo Is Nothing Then
            Return
        End If
        Try
            ListaEnlacesSeguimiento = Await servicio.CargarEnlacesSeguimiento(modelo.empresa, modelo.numero)
        Catch ex As Exception
            ' La decisión ya está apuntada; solo se vuelve a leer el bloque.
            Dim unused = CargarDevueltosDelPedidoAsync()
        End Try
    End Function

End Class
