Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta.Services

''' <summary>
''' Nesto#640 (NestoAPI#864): ofrecer el pago a plazos de un pedido ya creado. Enseña con quién se financia, cuánto y el aviso
''' de la API, y deja elegir el canal: la app del cliente (solo si la tiene) o un enlace (con el correo opcional). «Solicitar»
''' hace el POST sin cerrar: si vuelve un enlace se enseña con «Copiar enlace» (la API no lo manda: lo manda el usuario) y, si
''' fue por la app, a cuántos dispositivos se avisó. Si la API lo rechaza, su mensaje sale tal cual y se puede reintentar.
''' Cierra con OK si se ha abierto la solicitud; si no, con Cancel.
''' </summary>
Public Class PagoAPlazosDialogViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "PagoAPlazosDialog"
    Public Const PARAMETRO_EMPRESA As String = "empresa"
    Public Const PARAMETRO_PEDIDO As String = "pedido"
    Public Const PARAMETRO_DISPONIBILIDAD As String = "disponibilidad"

    Private ReadOnly _servicio As IServicioPagoAPlazos
    Private _empresa As String
    Private _pedido As Integer
    Private _disponibilidad As DisponibilidadPagoAPlazosModel

    Public Sub New(servicio As IServicioPagoAPlazos)
        _servicio = servicio
        Title = ReglasPagoAPlazos.TITULO
        SolicitarCommand = New AsyncRelayCommand(AddressOf SolicitarAsync, Function() PuedeSolicitar)
        CopiarEnlaceCommand = New RelayCommand(AddressOf CopiarEnlace, Function() HayEnlace)
        CopiarInvitacionCommand = New RelayCommand(AddressOf CopiarInvitacion, Function() HayInvitacionApp)
        CerrarCommand = New RelayCommand(AddressOf Cerrar)
    End Sub

    ''' <summary>Dónde se copia el enlace. Se cambia en los tests para no tocar el portapapeles de verdad.</summary>
    Public Property Portapapeles As IPortapapelesTexto = New PortapapelesTextoWpf()

    Public ReadOnly Property SolicitarCommand As AsyncRelayCommand
    Public ReadOnly Property CopiarEnlaceCommand As RelayCommand
    Public ReadOnly Property CopiarInvitacionCommand As RelayCommand
    Public ReadOnly Property CerrarCommand As RelayCommand

#Region "Lo que se ofrece"
    Public ReadOnly Property NombreProveedor As String
        Get
            Return ReglasPagoAPlazos.NombreProveedor(_disponibilidad?.NombreProveedor)
        End Get
    End Property

    Public ReadOnly Property TextoOferta As String
        Get
            Return $"Financiar {ReglasPagoAPlazos.Euros(If(_disponibilidad?.Importe, 0D))} con {NombreProveedor}"
        End Get
    End Property

    Public ReadOnly Property Aviso As String
        Get
            Return If(_disponibilidad?.Aviso, String.Empty).Trim()
        End Get
    End Property

    Public ReadOnly Property HayAviso As Boolean
        Get
            Return Aviso.Length > 0
        End Get
    End Property

    Public ReadOnly Property EsPruebas As Boolean
        Get
            Return _disponibilidad IsNot Nothing AndAlso _disponibilidad.Pruebas
        End Get
    End Property

    Public ReadOnly Property TextoPruebas As String
        Get
            Return ReglasPagoAPlazos.TEXTO_PRUEBAS
        End Get
    End Property

    Public ReadOnly Property ClienteTieneApp As Boolean
        Get
            Return _disponibilidad IsNot Nothing AndAlso _disponibilidad.ClienteTieneApp
        End Get
    End Property
#End Region

#Region "El canal"
    Private _canal As String = ReglasPagoAPlazos.CANAL_ENLACE
    ''' <summary>«App» o «Enlace».</summary>
    Public Property Canal As String
        Get
            Return _canal
        End Get
        Private Set(value As String)
            If SetProperty(_canal, value) Then
                OnPropertyChanged(NameOf(EsPorApp))
                OnPropertyChanged(NameOf(EsPorEnlace))
            End If
        End Set
    End Property

    Public Property EsPorApp As Boolean
        Get
            Return Canal = ReglasPagoAPlazos.CANAL_APP
        End Get
        Set(value As Boolean)
            If value AndAlso ClienteTieneApp Then
                Canal = ReglasPagoAPlazos.CANAL_APP
            End If
        End Set
    End Property

    Public Property EsPorEnlace As Boolean
        Get
            Return Canal = ReglasPagoAPlazos.CANAL_ENLACE
        End Get
        Set(value As Boolean)
            If value Then
                Canal = ReglasPagoAPlazos.CANAL_ENLACE
            End If
        End Set
    End Property

    Private _correo As String
    ''' <summary>El correo del cliente para la financiera. Vacío: el de su ficha.</summary>
    Public Property Correo As String
        Get
            Return _correo
        End Get
        Set(value As String)
            Dim unused = SetProperty(_correo, value)
        End Set
    End Property
#End Region

#Region "Solicitar"
    Private _trabajando As Boolean
    Public Property Trabajando As Boolean
        Get
            Return _trabajando
        End Get
        Private Set(value As Boolean)
            If SetProperty(_trabajando, value) Then
                NotificarEstado()
            End If
        End Set
    End Property

    Private _solicitud As SolicitudPagoAPlazosModel
    ''' <summary>La respuesta del POST, o Nothing mientras no se haya pedido.</summary>
    Public Property Solicitud As SolicitudPagoAPlazosModel
        Get
            Return _solicitud
        End Get
        Private Set(value As SolicitudPagoAPlazosModel)
            If SetProperty(_solicitud, value) Then
                NotificarEstado()
            End If
        End Set
    End Property

    Public ReadOnly Property Solicitada As Boolean
        Get
            Return Solicitud IsNot Nothing
        End Get
    End Property

    ''' <summary>Antes de pedirlo se elige el canal; después solo se ve el resultado.</summary>
    Public ReadOnly Property Eligiendo As Boolean
        Get
            Return Not Solicitada
        End Get
    End Property

    Public ReadOnly Property PuedeSolicitar As Boolean
        Get
            Return Eligiendo AndAlso Not Trabajando AndAlso _pedido > 0
        End Get
    End Property

    Private _error As String = String.Empty
    ''' <summary>El mensaje de la API tal cual, si no ha dejado abrir la solicitud.</summary>
    Public Property TextoError As String
        Get
            Return _error
        End Get
        Private Set(value As String)
            If SetProperty(_error, value) Then
                OnPropertyChanged(NameOf(HayError))
            End If
        End Set
    End Property

    Public ReadOnly Property HayError As Boolean
        Get
            Return Not String.IsNullOrEmpty(TextoError)
        End Get
    End Property

    Public ReadOnly Property TextoResultado As String
        Get
            Return ReglasPagoAPlazos.TextoResultado(Solicitud)
        End Get
    End Property

    Public ReadOnly Property AvisoResultado As String
        Get
            Return If(Solicitud?.Aviso, String.Empty).Trim()
        End Get
    End Property

    Public ReadOnly Property HayAvisoResultado As Boolean
        Get
            Return AvisoResultado.Length > 0
        End Get
    End Property

    Public ReadOnly Property UrlPago As String
        Get
            Return If(Solicitud?.UrlPago, String.Empty).Trim()
        End Get
    End Property

    Public ReadOnly Property HayEnlace As Boolean
        Get
            Return UrlPago.Length > 0
        End Get
    End Property

    ''' <summary>
    ''' Lo que se copia con «Copiar enlace»: el mensaje completo de la API (el enlace y, si el cliente no tiene la app, la
    ''' invitación debajo) o, si no lo trae (API anterior), solo el enlace.
    ''' </summary>
    Public ReadOnly Property TextoParaElCliente As String
        Get
            Dim mensaje As String = If(Solicitud?.MensajeCliente, String.Empty).Trim()
            Return If(mensaje.Length > 0, mensaje, UrlPago)
        End Get
    End Property

    ''' <summary>
    ''' La invitación a instalar la app (mensaje listo para pegar): la de la respuesta del POST o, antes de pedirlo, la de la
    ''' disponibilidad. Vacía si el cliente ya tiene la app.
    ''' </summary>
    Public ReadOnly Property InvitacionApp As String
        Get
            Dim deLaSolicitud As String = If(Solicitud?.InvitacionApp, String.Empty).Trim()
            Return If(deLaSolicitud.Length > 0, deLaSolicitud, If(_disponibilidad?.InvitacionApp, String.Empty).Trim())
        End Get
    End Property

    Public ReadOnly Property HayInvitacionApp As Boolean
        Get
            Return InvitacionApp.Length > 0
        End Get
    End Property

    Public ReadOnly Property TextoCopiarInvitacion As String
        Get
            Return ReglasPagoAPlazos.TEXTO_COPIAR_INVITACION
        End Get
    End Property

    Public ReadOnly Property AyudaCopiarInvitacion As String
        Get
            Return ReglasPagoAPlazos.AYUDA_COPIAR_INVITACION
        End Get
    End Property

    Private _textoInvitacionCopiada As String = String.Empty
    Public Property TextoInvitacionCopiada As String
        Get
            Return _textoInvitacionCopiada
        End Get
        Private Set(value As String)
            Dim unused = SetProperty(_textoInvitacionCopiada, value)
        End Set
    End Property

    Private _textoCopiado As String = String.Empty
    Public Property TextoCopiado As String
        Get
            Return _textoCopiado
        End Get
        Private Set(value As String)
            Dim unused = SetProperty(_textoCopiado, value)
        End Set
    End Property

    Public ReadOnly Property TextoBotonCerrar As String
        Get
            Return If(Solicitada, "Cerrar", "Cancelar")
        End Get
    End Property

    Private Sub NotificarEstado()
        OnPropertyChanged(NameOf(Solicitada))
        OnPropertyChanged(NameOf(Eligiendo))
        OnPropertyChanged(NameOf(PuedeSolicitar))
        OnPropertyChanged(NameOf(TextoResultado))
        OnPropertyChanged(NameOf(AvisoResultado))
        OnPropertyChanged(NameOf(HayAvisoResultado))
        OnPropertyChanged(NameOf(UrlPago))
        OnPropertyChanged(NameOf(HayEnlace))
        OnPropertyChanged(NameOf(TextoBotonCerrar))
        OnPropertyChanged(NameOf(TextoParaElCliente))
        OnPropertyChanged(NameOf(InvitacionApp))
        OnPropertyChanged(NameOf(HayInvitacionApp))
        SolicitarCommand.NotifyCanExecuteChanged()
        CopiarEnlaceCommand.NotifyCanExecuteChanged()
        CopiarInvitacionCommand.NotifyCanExecuteChanged()
    End Sub

    Friend Async Function SolicitarAsync() As Task
        If Not PuedeSolicitar Then
            Return
        End If
        TextoError = String.Empty
        Trabajando = True
        Try
            ' Ojo: VB no distingue mayúsculas; la variable no puede llamarse como la propiedad Solicitud
            Dim respuesta As SolicitudPagoAPlazosModel = Await _servicio.Solicitar(_empresa, _pedido, Canal, Correo)
            If respuesta Is Nothing Then
                TextoError = "La API no ha contestado nada. Compruebe el estado del pago a plazos en el pedido antes de pedirlo otra vez."
            Else
                Solicitud = respuesta
            End If
        Catch ex As Exception
            TextoError = ex.Message
        Finally
            Trabajando = False
        End Try
    End Function

    Private Sub CopiarEnlace()
        If Not HayEnlace Then
            Return
        End If
        Try
            Dim texto As String = TextoParaElCliente
            Portapapeles.CopiarTexto(texto)
            TextoCopiado = If(texto = UrlPago, ReglasPagoAPlazos.TEXTO_ENLACE_COPIADO, ReglasPagoAPlazos.TEXTO_MENSAJE_COPIADO)
        Catch ex As Exception
            TextoCopiado = "No se ha podido copiar el enlace. Selecciónelo y cópielo a mano."
        End Try
    End Sub

    Private Sub CopiarInvitacion()
        If Not HayInvitacionApp Then
            Return
        End If
        Try
            Portapapeles.CopiarTexto(InvitacionApp)
            TextoInvitacionCopiada = ReglasPagoAPlazos.TEXTO_INVITACION_COPIADA
        Catch ex As Exception
            TextoInvitacionCopiada = "No se ha podido copiar la invitación. Inténtelo de nuevo, por favor."
        End Try
    End Sub

    Private Sub Cerrar()
        RaiseRequestClose(If(Solicitada, ResultadoBoton.OK, ResultadoBoton.Cancel))
    End Sub
#End Region

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        If parameters Is Nothing Then
            Return
        End If
        If parameters.ContainsKey(PARAMETRO_EMPRESA) Then
            _empresa = parameters.GetValue(Of String)(PARAMETRO_EMPRESA)
        End If
        If parameters.ContainsKey(PARAMETRO_PEDIDO) Then
            _pedido = parameters.GetValue(Of Integer)(PARAMETRO_PEDIDO)
        End If
        If parameters.ContainsKey(PARAMETRO_DISPONIBILIDAD) Then
            _disponibilidad = parameters.GetValue(Of DisponibilidadPagoAPlazosModel)(PARAMETRO_DISPONIBILIDAD)
        End If
        If _pedido > 0 Then
            Title = $"{ReglasPagoAPlazos.TITULO} del pedido {_pedido}"
        End If
        Canal = ReglasPagoAPlazos.CanalInicial(_disponibilidad)
        OnPropertyChanged(NameOf(NombreProveedor))
        OnPropertyChanged(NameOf(TextoOferta))
        OnPropertyChanged(NameOf(Aviso))
        OnPropertyChanged(NameOf(HayAviso))
        OnPropertyChanged(NameOf(EsPruebas))
        OnPropertyChanged(NameOf(ClienteTieneApp))
        OnPropertyChanged(NameOf(EsPorApp))
        OnPropertyChanged(NameOf(EsPorEnlace))
        NotificarEstado()
    End Sub
End Class
