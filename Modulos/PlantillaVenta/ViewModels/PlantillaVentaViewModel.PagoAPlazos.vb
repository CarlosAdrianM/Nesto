Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Services

''' <summary>
''' Nesto#640 (NestoAPI#864): ofrecer el pago a plazos sin tener que ir a buscar el pedido. En el paso Finalizar, la casilla
''' «Ofrecer el pago a plazos al grabar el pedido»: solo se pinta si el interruptor de la API lo tiene encendido para ese
''' cliente (apagado, sin fila o sin poder leerlo, no sale nada). Marcada, y ya con el pedido GRABADO, se pregunta a la API si
''' ese pedido se puede pagar a plazos: si sí, se abre el mismo diálogo que en el detalle del pedido; si no, se dice el motivo
''' de la API. Solo al crear un pedido (al modificar o ampliar se pide desde el propio pedido, donde se ve lo que ya tiene).
''' Nada de esto puede impedir ni retrasar que el pedido se grabe: va después de grabar y nunca lanza.
''' </summary>
Partial Public Class PlantillaVentaViewModel

    ''' <summary>Friend para los tests (en producción se crea en el constructor).</summary>
    Friend Property ServicioPagoAPlazos As IServicioPagoAPlazos

    Private _interruptorPagoAPlazos As InterruptorPagoAPlazosModel
    Private _ofrecerPagoAPlazos As Boolean

    Private Sub InicializarPagoAPlazos(servicioAutenticacion As IServicioAutenticacion)
        ServicioPagoAPlazos = New ServicioPagoAPlazos(configuracion, servicioAutenticacion)
    End Sub

    ''' <summary>La casilla se pinta: el interruptor de la API está encendido para el cliente de la plantilla.</summary>
    Public ReadOnly Property MostrarOfrecerPagoAPlazos As Boolean
        Get
            Return ReglasPagoAPlazos.SeOfreceAntesDeGrabar(_interruptorPagoAPlazos, _clienteSeleccionado?.cliente)
        End Get
    End Property

    ''' <summary>La casilla del paso Finalizar. Es de este pedido: no se guarda en ningún sitio y nace sin marcar.</summary>
    Public Property OfrecerPagoAPlazos As Boolean
        Get
            Return _ofrecerPagoAPlazos AndAlso MostrarOfrecerPagoAPlazos
        End Get
        Set(value As Boolean)
            If _ofrecerPagoAPlazos <> value Then
                _ofrecerPagoAPlazos = value
                OnPropertyChanged()
            End If
        End Set
    End Property

    Public ReadOnly Property TextoOfrecerPagoAPlazos As String
        Get
            Return ReglasPagoAPlazos.TEXTO_CASILLA_PLANTILLA
        End Get
    End Property

    Public ReadOnly Property AyudaOfrecerPagoAPlazos As String
        Get
            Return ReglasPagoAPlazos.AYUDA_CASILLA_PLANTILLA
        End Get
    End Property

    ''' <summary>Al cambiar el cliente: en modo de pruebas la casilla solo sale con los clientes de prueba.</summary>
    Private Sub NotificarOfrecerPagoAPlazos()
        OnPropertyChanged(NameOf(MostrarOfrecerPagoAPlazos))
        OnPropertyChanged(NameOf(OfrecerPagoAPlazos))
    End Sub

    ''' <summary>
    ''' Lee el interruptor de la API al llegar a la plantilla. Nunca lanza: sin fila, sin red o con cualquier error, como si
    ''' estuviera apagado (no sale la casilla). Friend para los tests.
    ''' </summary>
    Friend Async Function CargarOfrecerPagoAPlazosAsync() As Task
        Dim interruptor As InterruptorPagoAPlazosModel = Nothing
        Try
            If ServicioPagoAPlazos IsNot Nothing Then
                interruptor = Await ServicioPagoAPlazos.LeerInterruptor()
            End If
        Catch ex As Exception
            interruptor = Nothing
        End Try
        _interruptorPagoAPlazos = interruptor
        NotificarOfrecerPagoAPlazos()
    End Function

    ''' <summary>
    ''' Con el pedido ya grabado y la casilla marcada: pregunta a la API por ESE pedido y abre el diálogo del pago a plazos, o
    ''' dice por qué no se puede. Nunca lanza (el pedido ya está grabado y tiene que abrirse igual). Friend para los tests.
    ''' </summary>
    Friend Async Function OfrecerPagoAPlazosAlTerminarAsync(empresa As String, numPedido As String, esPedidoNuevo As Boolean) As Task
        Try
            If Not OfrecerPagoAPlazos Then
                Return
            End If
            If Not esPedidoNuevo Then
                dialogService.ShowNotification(ReglasPagoAPlazos.TITULO, ReglasPagoAPlazos.TEXTO_PLANTILLA_SOLO_PEDIDO_NUEVO)
                Return
            End If
            Dim numero As Integer
            If ServicioPagoAPlazos Is Nothing OrElse Not Integer.TryParse(numPedido, numero) OrElse numero <= 0 Then
                Return
            End If

            Dim disponibilidad As DisponibilidadPagoAPlazosModel = Nothing
            Dim fallo As String = Nothing
            Try
                disponibilidad = Await ServicioPagoAPlazos.LeerDisponible(empresa, numero)
            Catch ex As Exception
                fallo = If(ex.Message, String.Empty)
            End Try
            If fallo IsNot Nothing Then
                dialogService.ShowNotification(ReglasPagoAPlazos.TITULO, ReglasPagoAPlazos.TextoNoSeHaPodidoComprobarTrasGrabar(numero, fallo))
                Return
            End If
            If Not ReglasPagoAPlazos.PuedePedirse(disponibilidad) Then
                dialogService.ShowNotification(ReglasPagoAPlazos.TITULO, ReglasPagoAPlazos.TextoNoSePuedeTrasGrabar(numero, disponibilidad))
                Return
            End If

            Dim unused As ResultadoDialogo = Await dialogService.ShowDialogAsync(PagoAPlazosDialogViewModel.NOMBRE, New ParametrosDialogo From {
                {PagoAPlazosDialogViewModel.PARAMETRO_EMPRESA, empresa},
                {PagoAPlazosDialogViewModel.PARAMETRO_PEDIDO, numero},
                {PagoAPlazosDialogViewModel.PARAMETRO_DISPONIBILIDAD, disponibilidad}
            })
        Catch ex As Exception
            ' A propósito: el pedido ya está grabado; el pago a plazos se puede pedir después desde el propio pedido
        End Try
    End Function
End Class
