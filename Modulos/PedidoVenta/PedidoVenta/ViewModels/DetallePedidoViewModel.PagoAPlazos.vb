Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.PedidoVenta.Services

''' <summary>
''' Nesto#640 (NestoAPI#864): pago a plazos de un pedido ya creado, con la financiera que diga la API (aquí no se nombra a
''' ninguna). En la cabecera del pedido, bajo «Mandar enlace pago»: la acción «Pago a plazos…» (solo si la API lo ofrece; si
''' este pedido no se puede, deshabilitada con el motivo), el estado de la solicitud y «Cancelar pago a plazos». El estado se
''' lee al abrir el pedido y tras cada acción: sin temporizadores. Nada de esto rompe la carga del pedido.
''' </summary>
Partial Public Class DetallePedidoViewModel

    ''' <summary>Friend para los tests (en producción se crea en el constructor).</summary>
    Friend Property ServicioPagoAPlazos As IServicioPagoAPlazos

    Public Property PagoAPlazosCommand As AsyncRelayCommand
    Public Property CancelarPagoAPlazosCommand As AsyncRelayCommand

    Private _versionPagoAPlazos As Integer
    Private _trabajandoPagoAPlazos As Boolean

    Private Sub InicializarPagoAPlazos(servicioAutenticacion As IServicioAutenticacion)
        ServicioPagoAPlazos = New ServicioPagoAPlazos(configuracion, servicioAutenticacion)
        PagoAPlazosCommand = New AsyncRelayCommand(AddressOf PedirPagoAPlazosAsync, Function() PuedePedirPagoAPlazos)
        CancelarPagoAPlazosCommand = New AsyncRelayCommand(AddressOf CancelarPagoAPlazosAsync, Function() PuedeCancelarPagoAPlazos)
    End Sub

    Private _disponibilidadPagoAPlazos As DisponibilidadPagoAPlazosModel
    ''' <summary>Lo que dice la API del pedido en pantalla (GET …/PagoAPlazos/Disponible), o Nothing si no ha contestado.</summary>
    Public Property DisponibilidadPagoAPlazos As DisponibilidadPagoAPlazosModel
        Get
            Return _disponibilidadPagoAPlazos
        End Get
        Private Set(value As DisponibilidadPagoAPlazosModel)
            If SetProperty(_disponibilidadPagoAPlazos, value) Then
                NotificarCambioPagoAPlazos()
            End If
        End Set
    End Property

    Private _estadoPagoAPlazos As EstadoPagoAPlazosModel
    ''' <summary>La solicitud del pedido en pantalla (GET …/PagoAPlazos/Estado), o Nothing.</summary>
    Public Property EstadoPagoAPlazos As EstadoPagoAPlazosModel
        Get
            Return _estadoPagoAPlazos
        End Get
        Private Set(value As EstadoPagoAPlazosModel)
            If SetProperty(_estadoPagoAPlazos, value) Then
                NotificarCambioPagoAPlazos()
            End If
        End Set
    End Property

    ''' <summary>La acción «Pago a plazos…» se pinta: la API lo ofrece (interruptor encendido y con proveedor).</summary>
    Public ReadOnly Property MostrarPagoAPlazos As Boolean
        Get
            Return ReglasPagoAPlazos.SeOfrece(DisponibilidadPagoAPlazos)
        End Get
    End Property

    ''' <summary>Se pinta y además este pedido se puede financiar ahora (si no, el botón sale deshabilitado con el motivo).</summary>
    Public ReadOnly Property PuedePedirPagoAPlazos As Boolean
        Get
            Return ReglasPagoAPlazos.PuedePedirse(DisponibilidadPagoAPlazos) AndAlso Not _trabajandoPagoAPlazos
        End Get
    End Property

    ''' <summary>ToolTip del botón: qué hace o el motivo de la API por el que no se puede.</summary>
    Public ReadOnly Property TextoAyudaPagoAPlazos As String
        Get
            Return ReglasPagoAPlazos.TextoAyuda(DisponibilidadPagoAPlazos)
        End Get
    End Property

    Public ReadOnly Property HayEstadoPagoAPlazos As Boolean
        Get
            Return ReglasPagoAPlazos.HaySolicitud(EstadoPagoAPlazos)
        End Get
    End Property

    ''' <summary>«Pago a plazos (Financiera): pendiente del cliente».</summary>
    Public ReadOnly Property TextoEstadoPagoAPlazos As String
        Get
            Return ReglasPagoAPlazos.TextoEstado(EstadoPagoAPlazos)
        End Get
    End Property

    Public ReadOnly Property DetalleEstadoPagoAPlazos As String
        Get
            Return ReglasPagoAPlazos.TextoDetalleEstado(EstadoPagoAPlazos)
        End Get
    End Property

    ''' <summary>Hay una solicitud viva (pendiente o aprobada): sale «Cancelar pago a plazos».</summary>
    Public ReadOnly Property HayPagoAPlazosVivo As Boolean
        Get
            Return ReglasPagoAPlazos.EsViva(EstadoPagoAPlazos)
        End Get
    End Property

    Public ReadOnly Property PuedeCancelarPagoAPlazos As Boolean
        Get
            Return HayPagoAPlazosVivo AndAlso Not _trabajandoPagoAPlazos
        End Get
    End Property

    Private Sub NotificarCambioPagoAPlazos()
        OnPropertyChanged(NameOf(MostrarPagoAPlazos))
        OnPropertyChanged(NameOf(PuedePedirPagoAPlazos))
        OnPropertyChanged(NameOf(TextoAyudaPagoAPlazos))
        OnPropertyChanged(NameOf(HayEstadoPagoAPlazos))
        OnPropertyChanged(NameOf(TextoEstadoPagoAPlazos))
        OnPropertyChanged(NameOf(DetalleEstadoPagoAPlazos))
        OnPropertyChanged(NameOf(HayPagoAPlazosVivo))
        OnPropertyChanged(NameOf(PuedeCancelarPagoAPlazos))
        PagoAPlazosCommand?.NotifyCanExecuteChanged()
        CancelarPagoAPlazosCommand?.NotifyCanExecuteChanged()
    End Sub

    ''' <summary>
    ''' Pregunta a la API el estado y la disponibilidad del pago a plazos del pedido en pantalla. Nunca lanza: con un 503 (script
    ''' sin lanzar) o cualquier error, como si no hubiera nada (ni acción ni estado); si falla el estado ya no se pregunta la
    ''' disponibilidad. Un pedido sin guardar no se pregunta. Si mientras tanto se abre otro pedido, la respuesta del anterior
    ''' se descarta. Friend para los tests.
    ''' </summary>
    Friend Async Function CargarPagoAPlazosAsync() As Task
        _versionPagoAPlazos += 1
        Dim version As Integer = _versionPagoAPlazos
        EstadoPagoAPlazos = Nothing
        DisponibilidadPagoAPlazos = Nothing
        Dim modelo As Nesto.Models.PedidoVentaDTO = pedido?.Model
        If ServicioPagoAPlazos Is Nothing OrElse modelo Is Nothing OrElse modelo.numero <= 0 Then
            Return
        End If
        Dim empresa As String = modelo.empresa
        Dim numero As Integer = modelo.numero

        Dim estado As EstadoPagoAPlazosModel
        Try
            estado = Await ServicioPagoAPlazos.LeerEstado(empresa, numero)
        Catch ex As Exception
            Return
        End Try
        If version <> _versionPagoAPlazos Then
            Return
        End If
        EstadoPagoAPlazos = estado

        Dim disponibilidad As DisponibilidadPagoAPlazosModel
        Try
            disponibilidad = Await ServicioPagoAPlazos.LeerDisponible(empresa, numero)
        Catch ex As Exception
            disponibilidad = Nothing
        End Try
        If version = _versionPagoAPlazos Then
            DisponibilidadPagoAPlazos = disponibilidad
        End If
    End Function

    ''' <summary>
    ''' «Pago a plazos…»: abre el diálogo (canal y, tras pedirlo, el enlace o a cuántos dispositivos se avisó) y, al cerrarse,
    ''' vuelve a leer el estado, se haya pedido o no. Friend para los tests.
    ''' </summary>
    Friend Async Function PedirPagoAPlazosAsync() As Task
        If Not PuedePedirPagoAPlazos OrElse pedido Is Nothing Then
            Return
        End If
        Dim empresa As String = pedido.empresa
        Dim numero As Integer = pedido.numero
        Dim unused As ResultadoDialogo = Await dialogService.ShowDialogAsync(PagoAPlazosDialogViewModel.NOMBRE, New ParametrosDialogo From {
            {PagoAPlazosDialogViewModel.PARAMETRO_EMPRESA, empresa},
            {PagoAPlazosDialogViewModel.PARAMETRO_PEDIDO, numero},
            {PagoAPlazosDialogViewModel.PARAMETRO_DISPONIBILIDAD, DisponibilidadPagoAPlazos}
        })
        If pedido IsNot Nothing AndAlso pedido.numero = numero Then
            Await CargarPagoAPlazosAsync()
        End If
    End Function

    ''' <summary>
    ''' «Cancelar pago a plazos»: pide el motivo (obligatorio; cerrar el cuadro = no cancelar) y llama a la API. Si la rechaza, su
    ''' mensaje sale tal cual. Si estaba aprobado, la API ha quitado el prepago: se recarga el pedido entero para que no quede
    ''' en pantalla; si no, solo el estado. Friend para los tests.
    ''' </summary>
    Friend Async Function CancelarPagoAPlazosAsync() As Task
        If Not PuedeCancelarPagoAPlazos OrElse pedido Is Nothing Then
            Return
        End If
        Dim empresa As String = pedido.empresa
        Dim numero As Integer = pedido.numero
        Dim estabaAprobado As Boolean = ReglasPagoAPlazos.EstaAprobado(EstadoPagoAPlazos)
        Dim motivo As String = dialogService.GetText(ReglasPagoAPlazos.TITULO_CANCELAR, ReglasPagoAPlazos.TextoPedirMotivoCancelacion(EstadoPagoAPlazos))
        If motivo Is Nothing Then
            Return
        End If
        If String.IsNullOrWhiteSpace(motivo) Then
            dialogService.ShowError(ReglasPagoAPlazos.TEXTO_FALTA_MOTIVO)
            Return
        End If

        _trabajandoPagoAPlazos = True
        NotificarCambioPagoAPlazos()
        Dim fallo As String = Nothing
        Try
            Dim unused As EstadoPagoAPlazosModel = Await ServicioPagoAPlazos.Cancelar(empresa, numero, motivo.Trim())
        Catch ex As Exception
            fallo = ex.Message
        Finally
            _trabajandoPagoAPlazos = False
        End Try
        NotificarCambioPagoAPlazos()

        If fallo IsNot Nothing Then
            dialogService.ShowError(fallo)
        End If
        If pedido Is Nothing OrElse pedido.numero <> numero Then
            Return ' mientras tanto se ha abierto otro pedido
        End If
        If fallo Is Nothing AndAlso estabaAprobado Then
            Await CargarPedidoAsync(New ResumenPedido With {.empresa = empresa, .numero = numero}) ' al ponerse el pedido se vuelve a leer el estado
        Else
            Await CargarPagoAPlazosAsync()
        End If
    End Function
End Class
