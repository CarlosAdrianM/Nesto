Imports System.IO
Imports System.Reflection
Imports ControlesUsuario.Dialogs
Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.LineaPedidoVentaDTO
Imports CommunityToolkit.Mvvm.Input
Imports CommunityToolkit.Mvvm.Messaging

Public Class PickingPopupViewModel
    Inherits DialogoNestoBase

    Private Const FILTRO_RUTAS_DEFECTO As String = "(ruta='AT ' or ruta='OT ' or ruta='16 ' or ruta='FW ' or ruta='00 ')"

    Private ReadOnly servicio As IPedidoVentaService
    Private ReadOnly messenger As IMessenger
    Private ReadOnly dialogService As IServicioDialogos
    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicioInformes As IInformesService


    Public Sub New(servicio As IPedidoVentaService, messenger As IMessenger, dialogService As IServicioDialogos, configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.New(servicio, messenger, dialogService, configuracion, New InformesService(configuracion, servicioAutenticacion))
    End Sub

    ' Constructor para tests: permite inyectar un IInformesService mockeado.
    Public Sub New(servicio As IPedidoVentaService, messenger As IMessenger, dialogService As IServicioDialogos, configuracion As IConfiguracion, servicioInformes As IInformesService)
        Me.servicio = servicio
        Me.messenger = messenger
        Me.dialogService = dialogService
        Me.configuracion = configuracion
        _servicioInformes = servicioInformes

        cmdInformeKits = New RelayCommand(AddressOf OnInformeKits)
        cmdInformePicking = New RelayCommand(AddressOf OnInformePicking)
        cmdInformePacking = New RelayCommand(AddressOf OnInformePacking)
        cmdSacarPicking = New RelayCommand(Of PedidoVentaDTO)(AddressOf OnSacarPicking, AddressOf CanSacarPicking)
        Title = "Picking"
    End Sub

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        pedidoPicking = parameters.GetValue(Of PedidoVentaDTO)("pedidoPicking")
        If Not IsNothing(pedidoPicking) Then
            numeroPedidoPicking = pedidoPicking.numero
            numeroClientePicking = pedidoPicking.cliente
        Else
            numeroPedidoPicking = 0
        End If
    End Sub





    Private _esPickingCliente As Boolean
    Public Property esPickingCliente As Boolean
        Get
            Return _esPickingCliente
        End Get
        Set(value As Boolean)
            SetProperty(_esPickingCliente, value)
        End Set
    End Property

    Private _esPickingPedido As Boolean = True
    Public Property esPickingPedido As Boolean
        Get
            Return _esPickingPedido
        End Get
        Set(value As Boolean)
            SetProperty(_esPickingPedido, value)
        End Set
    End Property

    Private _esPickingRutas As Boolean
    Public Property esPickingRutas As Boolean
        Get
            Return _esPickingRutas
        End Get
        Set(value As Boolean)
            SetProperty(_esPickingRutas, value)
        End Set
    End Property

    Private _esPickingRutasPropias As Boolean
    ''' <summary>
    ''' Sugerencia 854 (decisión de Carlos 28/10/26): picking de primera hora «Solo nuestra ruta». Saca solo los pedidos con
    ''' ruta propia (AT y 16) y entrega hasta hoy, para que los repartidores salgan a las 9; el picking automático de las 11
    ''' saca después todo lo demás. Es excluyente con los otros tres tipos de picking.
    ''' </summary>
    Public Property esPickingRutasPropias As Boolean
        Get
            Return _esPickingRutasPropias
        End Get
        Set(value As Boolean)
            If SetProperty(_esPickingRutasPropias, value) AndAlso value Then
                esPickingPedido = False
                esPickingCliente = False
                esPickingRutas = False
            End If
        End Set
    End Property

    Private _estaSacandoPicking As Boolean
    Public Property estaSacandoPicking() As Boolean
        Get
            Return _estaSacandoPicking
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaSacandoPicking, value)
            ' NestoAPI#405: el BusyIndicator solo TAPA la ventana; el comando seguía habilitado y
            ' se podía volver a pulsar. Hay que reevaluar el CanExecute para que el botón quede
            ' deshabilitado de verdad mientras el picking está en marcha.
            _cmdSacarPicking?.NotifyCanExecuteChanged()
        End Set
    End Property

    Private _numeroClientePicking As String
    Public Property numeroClientePicking() As String
        Get
            Return _numeroClientePicking
        End Get
        Set(ByVal value As String)
            SetProperty(_numeroClientePicking, value)
        End Set
    End Property

    Private _numeroPedidoPicking As Integer
    Public Property numeroPedidoPicking As Integer
        Get
            Return _numeroPedidoPicking
        End Get
        Set(value As Integer)
            SetProperty(_numeroPedidoPicking, value)
        End Set
    End Property

    Private _numeroPicking As Integer
    Public Property numeroPicking As Integer
        Get
            Return _numeroPicking
        End Get
        Set(value As Integer)
            SetProperty(_numeroPicking, value)
        End Set
    End Property

    Private _pedidoPicking As PedidoVentaDTO
    Public Property pedidoPicking As PedidoVentaDTO
        Get
            Return _pedidoPicking
        End Get
        Set(value As PedidoVentaDTO)
            SetProperty(_pedidoPicking, value)
        End Set
    End Property


    Private _cmdInformeKits As RelayCommand
    Public Property cmdInformeKits As RelayCommand
        Get
            Return _cmdInformeKits
        End Get
        Private Set(value As RelayCommand)
            SetProperty(_cmdInformeKits, value)
        End Set
    End Property
    Private Async Sub OnInformeKits()
        Try
            estaSacandoPicking = True
            Await AbrirInformeKitsAsync()
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaSacandoPicking = False
        End Try
    End Sub

    ' Los mismos parámetros para el informe y para el aviso de antes del picking (NestoAPI#606, kits).
    Private Shared Function FechaInformeKits() As String
        Return DateTime.Today.AddDays(4).ToString("dd/MM/yy")
    End Function

    ''' <summary>
    ''' NestoAPI#606 (kits, 13/10/26): la fecha de entrega a la agencia cuenta lo que sale desmontando un kit en Algete el
    ''' mismo día, y eso solo es verdad si almacén saca este informe antes del picking. Se recuerda si ya se ha sacado en
    ''' esta ventana para no volver a preguntar.
    ''' </summary>
    Public Property informeKitsSacado As Boolean

    Private Async Function AbrirInformeKitsAsync() As Task
        ' Nesto#340: el render se hace en el backend (QuestPDF); aquí solo descargamos el PDF.
        Dim pdf As Byte() = Await _servicioInformes.DescargarKitsQueSePuedenMontarPdf(
            empresa:="1",
            fecha:=FechaInformeKits(),
            almacen:="ALG",
            filtroRutas:=FILTRO_RUTAS_DEFECTO)
        informeKitsSacado = True
        abrirPdf.Invoke(pdf, "InformeKitsQueSePuedenMontar.pdf")
    End Function

    ' Para los tests: sin abrir el visor de PDF.
    Friend Property abrirPdf As Action(Of Byte(), String) = Sub(pdf, nombre)
                                                                 Dim fileName As String = Path.GetTempPath + nombre
                                                                 File.WriteAllBytes(fileName, pdf)
                                                                 Process.Start(New ProcessStartInfo(fileName) With {
                                                                     .UseShellExecute = True
                                                                 })
                                                             End Sub

    ''' <summary>
    ''' NestoAPI#606 (kits): los kits que el informe dice que hay que DESMONTAR (Tipo «D»), con los mismos datos que el informe.
    ''' Si no se puede leer, no se pregunta nada (no debe impedir sacar el picking).
    ''' </summary>
    Friend Async Function KitsQueDesmontarAsync() As Task(Of List(Of String))
        Try
            Dim kits = Await _servicioInformes.LeerKitsQueSePuedenMontar("1", FechaInformeKits(), "ALG", FILTRO_RUTAS_DEFECTO)
            If kits Is Nothing Then
                Return New List(Of String)
            End If
            Return kits.Where(Function(k) String.Equals(k.Tipo?.Trim(), "D", StringComparison.OrdinalIgnoreCase)) _
                .Select(Function(k) (k.Kit?.Trim() + " " + If(k.Nombre?.Trim(), String.Empty)).Trim()) _
                .Distinct() _
                .ToList()
        Catch ex As Exception
            Return New List(Of String)
        End Try
    End Function

    ''' <summary>
    ''' NestoAPI#606 (kits): antes de sacar el picking, si hay kits que desmontar y no se ha sacado el informe en esta ventana,
    ''' se pregunta. Sí → se abre el informe y el picking NO se saca (se desmonta y se vuelve a pulsar); No → sale igualmente.
    ''' Devuelve True si hay que seguir con el picking.
    ''' </summary>
    Friend Async Function ComprobarKitsQueDesmontarAsync() As Task(Of Boolean)
        If informeKitsSacado Then
            Return True
        End If
        Dim kits As List(Of String) = Await KitsQueDesmontarAsync()
        If Not kits.Any() Then
            Return True
        End If
        Dim cuantos As String = If(kits.Count = 1, "Hay 1 kit que desmontar", $"Hay {kits.Count} kits que desmontar")
        Dim pregunta As String = $"{cuantos} antes de este picking:" + vbCrLf + String.Join(vbCrLf, kits.Select(Function(k) "  • " + k)) +
            vbCrLf + vbCrLf + "¿Sacar el informe de kits?" + vbCrLf +
            "Sí: se abre el informe y el picking NO se saca todavía (desmonta los kits y vuelve a sacarlo)." + vbCrLf +
            "No: se saca el picking igualmente."
        If Not dialogService.ShowConfirmationAnswer("Kits que desmontar", pregunta) Then
            Return True
        End If
        Await AbrirInformeKitsAsync()
        Return False
    End Function

    ''' <summary>
    ''' Sugerencia 575 (Alfredo, 13/10/26): pedidos de este picking ya enseñados en el aviso de ruta propia en esta ventana
    ''' ("empresa/pedido"), para no volver a preguntar por ellos en el siguiente picking (p. ej. si no salieron por stock).
    ''' </summary>
    Friend ReadOnly Property pedidosRutaYaRevisados As New HashSet(Of String)

    Private Shared Function ClaveRevision(s As SugerenciaRutaPicking) As String
        Return $"{s.Empresa?.Trim()}/{s.Pedido}"
    End Function

    ''' <summary>
    ''' Sugerencia 575: los pedidos de ESTE picking (el pedido, el cliente o las rutas) que van por OnTime y que seguramente
    ''' saldrán por nuestra ruta. Si no se puede leer, no se pregunta nada (no debe impedir sacar el picking).
    ''' </summary>
    Friend Async Function SugerenciasRutaAsync() As Task(Of List(Of SugerenciaRutaPicking))
        Try
            Dim sugerencias As List(Of SugerenciaRutaPicking)
            If esPickingPedido Then
                Dim empresaPicking As String = If(pedidoPicking?.empresa, Constantes.Empresas.EMPRESA_DEFECTO)
                sugerencias = Await servicio.LeerSugerenciasRutaPicking(empresaPicking, numeroPedidoPicking, Nothing)
            ElseIf esPickingCliente Then
                If String.IsNullOrWhiteSpace(numeroClientePicking) Then
                    Return New List(Of SugerenciaRutaPicking)
                End If
                sugerencias = Await servicio.LeerSugerenciasRutaPicking(Nothing, 0, numeroClientePicking)
            ElseIf esPickingRutas OrElse esPickingRutasPropias Then
                ' Sugerencia 854: en el de «Solo nuestra ruta» el aviso es el mismo que en el de rutas (pedidos de OnTime que
                ' seguramente irían por AT/16): si almacén los pasa a nuestra ruta, salen ya en este picking de primera hora.
                sugerencias = Await servicio.LeerSugerenciasRutaPicking(Nothing, 0, Nothing)
            Else
                Return New List(Of SugerenciaRutaPicking)
            End If
            Return If(sugerencias, New List(Of SugerenciaRutaPicking)) _
                .Where(Function(s) Not pedidosRutaYaRevisados.Contains(ClaveRevision(s))) _
                .ToList()
        Catch ex As Exception
            Return New List(Of SugerenciaRutaPicking)
        End Try
    End Function

    ''' <summary>
    ''' Sugerencia 575 (Alfredo, almacén; decisión de Carlos 13/10/26): antes de sacar el picking, si hay pedidos que van por
    ''' OnTime y seguramente saldrán por nuestra ruta (AT/16), se enseñan con la ruta propuesta y el motivo. Almacén decide:
    ''' - «Cambiar la ruta de los marcados y sacar el picking»: se cambia (vía API, con su usuario) y sigue el picking. Si
    '''   alguno no se ha podido cambiar, se dice cuál y se pregunta si sacar el picking igualmente.
    ''' - «Sacar el picking sin cambiar nada»: sale como está.
    ''' - «Cancelar»: el picking NO se saca.
    ''' Solo aquí, al sacar el picking: a quien mete el pedido no se le avisa de nada. Devuelve True si hay que seguir.
    ''' </summary>
    Friend Async Function ComprobarSugerenciasRutaAsync() As Task(Of Boolean)
        Dim sugerencias As List(Of SugerenciaRutaPicking) = Await SugerenciasRutaAsync()
        If Not sugerencias.Any() Then
            Return True
        End If

        Dim resultado As ResultadoDialogo = Await dialogService.ShowDialogAsync(SugerenciasRutaPickingPopupViewModel.NOMBRE,
            New ParametrosDialogo From {{"sugerencias", sugerencias}})
        If resultado Is Nothing OrElse (resultado.Result <> ResultadoBoton.OK AndAlso resultado.Result <> ResultadoBoton.No) Then
            Return False
        End If
        For Each s In sugerencias
            pedidosRutaYaRevisados.Add(ClaveRevision(s))
        Next
        If resultado.Result <> ResultadoBoton.OK Then
            Return True
        End If

        Dim cambios As List(Of SugerenciaRutaPicking) = If(resultado.Parameters.GetValue(Of List(Of SugerenciaRutaPicking))("cambios"),
                                                          sugerencias.Where(Function(s) s.Marcado).ToList())
        If Not cambios.Any() Then
            Return True
        End If

        Dim fallos As New List(Of String)
        Dim errorGeneral As String = Nothing
        Try
            Dim resultados As List(Of ResultadoCambioRutaPicking) = Await servicio.CambiarRutaPedidos(cambios)
            For Each cambio In cambios
                Dim r = resultados?.FirstOrDefault(Function(x) x.Pedido = cambio.Pedido AndAlso x.Empresa?.Trim() = cambio.Empresa?.Trim())
                If r Is Nothing OrElse Not r.Cambiado Then
                    fallos.Add($"  • {cambio.Pedido}: {If(r?.Error, "no se ha cambiado")}")
                End If
            Next
        Catch ex As Exception
            errorGeneral = ex.Message
        End Try

        If errorGeneral Is Nothing AndAlso Not fallos.Any() Then
            Return True
        End If
        Dim detalle As String = If(errorGeneral, String.Join(vbCrLf, fallos))
        Return dialogService.ShowConfirmationAnswer("Cambio de ruta",
            "No se ha podido cambiar la ruta de todos los pedidos marcados:" + vbCrLf + detalle + vbCrLf + vbCrLf +
            "¿Sacar el picking igualmente? (los que no se han cambiado salen por la ruta que tenían)")
    End Function




    Private _cmdInformePacking As RelayCommand
    Public Property cmdInformePacking As RelayCommand
        Get
            Return _cmdInformePacking
        End Get
        Private Set(value As RelayCommand)
            SetProperty(_cmdInformePacking, value)
        End Set
    End Property
    ' Nesto#340 (retirada de flags 20/08/26): MotorPdfPicking llevaba semanas al 100% en
    ' QuestPDF (defecto incluido) — el RDLC local era código muerto. El PDF lo genera SIEMPRE
    ' NestoAPI.
    ' Extraídos para poder testear la interacción con IInformesService sin tocar el Process.Start.
    Public Async Function ObtenerPdfPickingAsync() As Task(Of Byte())
        If numeroPicking <= 0 Then
            numeroPicking = Await _servicioInformes.LeerUltimoPicking()
        End If
        Return Await _servicioInformes.DescargarPickingPdf(numeroPicking)
    End Function

    Public Async Function ObtenerPdfPackingAsync() As Task(Of Byte())
        If numeroPicking <= 0 Then
            numeroPicking = Await _servicioInformes.LeerUltimoPicking()
        End If
        Return Await _servicioInformes.DescargarPackingPdf(numeroPicking)
    End Function

    Private Async Sub OnInformePacking()
        Try
            estaSacandoPicking = True
            Dim pdf As Byte() = Await ObtenerPdfPackingAsync()
            Dim fileName As String = Path.GetTempPath + "InformePacking.pdf"
            File.WriteAllBytes(fileName, pdf)
            Process.Start(New ProcessStartInfo(fileName) With {
                .UseShellExecute = True
            })
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaSacandoPicking = False
        End Try
    End Sub



    Private _cmdInformePicking As RelayCommand
    Public Property cmdInformePicking As RelayCommand
        Get
            Return _cmdInformePicking
        End Get
        Private Set(value As RelayCommand)
            SetProperty(_cmdInformePicking, value)
        End Set
    End Property
    Private Async Sub OnInformePicking()
        Try
            estaSacandoPicking = True
            Dim pdf As Byte() = Await ObtenerPdfPickingAsync()
            Dim fileName As String = Path.GetTempPath + "InformePicking.pdf"
            File.WriteAllBytes(fileName, pdf)
            Process.Start(New ProcessStartInfo(fileName) With {
                .UseShellExecute = True
            })
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaSacandoPicking = False
        End Try

    End Sub



    Private _cmdSacarPicking As RelayCommand(Of PedidoVentaDTO)
    Public Property cmdSacarPicking As RelayCommand(Of PedidoVentaDTO)
        Get
            Return _cmdSacarPicking
        End Get
        Private Set(value As RelayCommand(Of PedidoVentaDTO))
            SetProperty(_cmdSacarPicking, value)
        End Set
    End Property
    ''' <summary>
    ''' NestoAPI#405: mientras se está sacando un picking NO se puede volver a pedir otro.
    '''
    ''' El 25/08/2026 el picking tardó 2 min 47 s (lo normal son 5-7 s), el usuario pensó que no
    ''' había pasado nada y pulsó otra vez 3 segundos después. Las dos ejecuciones se solaparon y
    ''' cada una reservó sus ubicaciones: el packing salió con el DOBLE de unidades.
    '''
    ''' Esto es solo la primera capa, y la más floja: no cubre abrir otra ventana de Nesto ni a
    ''' dos personas distintas. De eso se encarga el applock del servidor (GestorPicking), que es
    ''' quien de verdad garantiza que solo se saque un picking a la vez.
    '''
    ''' El orden de las condiciones importa: la comprobación local va PRIMERO para no consultar a
    ''' Active Directory en cada reevaluación del CanExecute (Nesto#450).
    ''' </summary>
    Private Function CanSacarPicking(arg As PedidoVentaDTO) As Boolean
        Return Not estaSacandoPicking AndAlso configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
    End Function
    Private Async Sub OnSacarPicking(pedidoPicking As PedidoVentaDTO)
        Await SacarPickingAsync(pedidoPicking)
    End Sub

    ''' <summary>
    ''' Saca el picking elegido. 01/10/26 (Alfredo, cliente 5057 «LOS LUNES CIERRA»): si el pedido no sale porque el
    ''' cliente cierra el día de la entrega, en el picking de UN pedido se pregunta «¿Aún así quieres asignarle
    ''' picking?» y, si se confirma, se pide otra vez ignorando el cierre. En los de cliente y rutas solo se avisa.
    ''' </summary>
    Friend Async Function SacarPickingAsync(pedidoPicking As PedidoVentaDTO) As Task
        Try
            estaSacandoPicking = True
            If Not Await ComprobarKitsQueDesmontarAsync() Then
                Return
            End If
            If Not Await ComprobarSugerenciasRutaAsync() Then
                Return
            End If
            If esPickingPedido Then
                Dim empresaPicking As String
                If Not IsNothing(pedidoPicking) Then
                    empresaPicking = pedidoPicking.empresa
                Else
                    empresaPicking = Constantes.Empresas.EMPRESA_DEFECTO
                End If
                ' VB no admite Await dentro de un Catch: se guarda el aviso y se decide fuera.
                Dim cierre As PickingClienteCerradoException = Nothing
                Try
                    Await servicio.sacarPickingPedido(empresaPicking, numeroPedidoPicking, False)
                Catch ex As PickingClienteCerradoException
                    cierre = ex
                End Try
                If cierre IsNot Nothing Then
                    If Not dialogService.ShowConfirmationAnswer("Picking pedido " + numeroPedidoPicking.ToString,
                                                                cierre.Message + vbCrLf + vbCrLf + "¿Aún así quieres asignarle picking?") Then
                        Return
                    End If
                    Await servicio.sacarPickingPedido(empresaPicking, numeroPedidoPicking, True)
                End If
            ElseIf esPickingCliente Then
                Await servicio.sacarPickingPedido(numeroClientePicking)
            ElseIf esPickingRutasPropias Then
                Await servicio.sacarPickingRutasPropias()
            ElseIf esPickingRutas Then
                Await servicio.sacarPickingPedido()
            Else
                Throw New Exception("No hay ningún tipo de picking seleccionado")
            End If
            Dim textoMensaje As String
            If esPickingPedido Then
                textoMensaje = "Se ha asignado el picking correctamente al pedido " + numeroPedidoPicking.ToString
            ElseIf esPickingCliente Then
                textoMensaje = "Se ha asignado el picking correctamente al cliente " + numeroClientePicking
            ElseIf esPickingRutasPropias Then
                textoMensaje = "Se ha asignado el picking correctamente a nuestra ruta (AT y 16)"
            ElseIf esPickingRutas Then
                textoMensaje = "Se ha asignado el picking correctamente a las rutas"
            Else
                Throw New Exception("Tiene que haber algún tipo de picking seleccionado")
            End If
            dialogService.ShowNotification("Picking", textoMensaje)
            numeroPicking = Await _servicioInformes.LeerUltimoPicking()
            messenger.Send(New SacarPickingMensaje(1))
        Catch ex As Exception
            Dim tituloError As String
            If esPickingPedido Then
                tituloError = "Error Picking pedido " + numeroPedidoPicking.ToString
            ElseIf esPickingCliente Then
                tituloError = "Error Picking cliente " + numeroClientePicking
            ElseIf esPickingRutasPropias Then
                tituloError = "Error Picking Solo nuestra ruta"
            ElseIf esPickingRutas Then
                tituloError = "Error Picking Rutas"
            Else
                tituloError = "Error Picking sin tipo"
            End If
            Dim textoError As String
            If IsNothing(ex.InnerException) Then
                textoError = ex.Message
            Else
                textoError = ex.Message + vbCr + ex.InnerException.Message
            End If
            dialogService.ShowNotification(tituloError, textoError)
        Finally
            estaSacandoPicking = False
        End Try
    End Function

End Class
