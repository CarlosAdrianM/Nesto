Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Events
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports Unity

''' <summary>
''' Tests del wrapper ConfirmarSiPlazoNoPermitido en DetallePedidoViewModel (Issue #254).
''' Cuando el plazo del pedido ya no está permitido al cliente, el VM debe pedir
''' confirmación antes de seguir con albarán/factura, en lugar de proceder en
''' silencio (que era el comportamiento previo al fix del SelectorPlazosPago,
''' que además producía falso "el pedido ha cambiado").
''' </summary>
<TestClass()>
Public Class DetallePedidoTiendaOnlineTests

    ' Nesto#410: pedidos con líneas de tienda online (Constantes.FormasVenta.FORMAS_ONLINE)
    ' se pueden facturar/albaranear aunque el usuario no sea de almacén/tiendas.

    <TestMethod()>
    Public Sub TieneLineaTiendaOnline_ConLineaQRU_True()
        Assert.IsTrue(DetallePedidoViewModel.TieneLineaTiendaOnline({"VAR", "QRU"}))
    End Sub

    <TestMethod()>
    Public Sub TieneLineaTiendaOnline_TodasLasFormasOnline_True()
        For Each forma In {"QRU", "WEB", "STK", "BLT"}
            Assert.IsTrue(DetallePedidoViewModel.TieneLineaTiendaOnline({forma}), $"Debe reconocer {forma}")
        Next
    End Sub

    <TestMethod()>
    Public Sub TieneLineaTiendaOnline_ConPaddingDeBD_True()
        ' Los char de BD llegan con espacios de relleno
        Assert.IsTrue(DetallePedidoViewModel.TieneLineaTiendaOnline({"WEB "}))
    End Sub

    <TestMethod()>
    Public Sub TieneLineaTiendaOnline_SoloFormasNormales_False()
        Assert.IsFalse(DetallePedidoViewModel.TieneLineaTiendaOnline({"VAR", "APC"}))
    End Sub

    <TestMethod()>
    Public Sub TieneLineaTiendaOnline_SinLineasONothing_False()
        Assert.IsFalse(DetallePedidoViewModel.TieneLineaTiendaOnline(Nothing))
        Assert.IsFalse(DetallePedidoViewModel.TieneLineaTiendaOnline(New List(Of String)))
        Assert.IsFalse(DetallePedidoViewModel.TieneLineaTiendaOnline({CType(Nothing, String)}))
    End Sub

End Class

<TestClass()>
Public Class DetallePedidoViewModelConfirmacionTests

    Private regionManager As IRegionManager
    Private configuracion As IConfiguracion
    Private servicio As IPedidoVentaService
    Private eventAggregator As IEventAggregator
    Private dialogService As IDialogService
    Private container As IUnityContainer
    Private servicioAutenticacion As IServicioAutenticacion

    <TestInitialize()>
    Public Sub Initialize()
        regionManager = A.Fake(Of IRegionManager)
        configuracion = A.Fake(Of IConfiguracion)
        servicio = A.Fake(Of IPedidoVentaService)
        eventAggregator = A.Fake(Of IEventAggregator)
        dialogService = A.Fake(Of IDialogService)
        container = A.Fake(Of IUnityContainer)
        servicioAutenticacion = A.Fake(Of IServicioAutenticacion)
    End Sub

    Private Function CrearViewModel() As DetallePedidoViewModel
        Return New DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container, servicioAutenticacion)
    End Function

    ''' <summary>
    ''' Cuando dialogService.ShowDialog se llama por la extensión ShowConfirmationAnswer,
    ''' simula que el usuario pulsa el botón correspondiente devolviendo OK o Cancel.
    ''' </summary>
    Private Sub ConfigurarRespuestaConfirmacion(respuestaOk As Boolean)
        A.CallTo(Sub() dialogService.ShowDialog(
                    A(Of String).Ignored,
                    A(Of IDialogParameters).Ignored,
                    A(Of Action(Of IDialogResult)).Ignored)) _
         .Invokes(Sub(nombre As String, parametros As IDialogParameters, callback As Action(Of IDialogResult))
                      ' Nesto#421: ShowError llama a ShowDialog SIN callback; guard para que un
                      ' test que pase por un error no reviente el host con NullReferenceException.
                      If callback Is Nothing Then
                          Return
                      End If
                      Dim resultado = A.Fake(Of IDialogResult)
                      A.CallTo(Function() resultado.Result).Returns(If(respuestaOk, ButtonResult.OK, ButtonResult.Cancel))
                      callback(resultado)
                  End Sub)
    End Sub

    ' Nesto#413 (remate de Nesto#410, caso real Laura con pedido AMZ/STK): la VISIBILIDAD de los
    ' botones de facturación debe incluir los pedidos con líneas de tienda online, no solo el
    ' grupo almacén/tiendas. El habilitado fino lo deciden los Can* de cada comando.

    <TestMethod()>
    Public Sub DebeMostrarBotonesFacturacion_UsuarioFueraDelGrupoConPedidoOnline_True()
        Assert.IsTrue(DetallePedidoViewModel.DebeMostrarBotonesFacturacion(esGrupoQuePuedeFacturar:=False, tieneLineaTiendaOnline:=True))
    End Sub

    <TestMethod()>
    Public Sub DebeMostrarBotonesFacturacion_UsuarioDelGrupo_TrueSiempre()
        Assert.IsTrue(DetallePedidoViewModel.DebeMostrarBotonesFacturacion(esGrupoQuePuedeFacturar:=True, tieneLineaTiendaOnline:=False))
    End Sub

    <TestMethod()>
    Public Sub DebeMostrarBotonesFacturacion_NiGrupoNiOnline_False()
        Assert.IsFalse(DetallePedidoViewModel.DebeMostrarBotonesFacturacion(esGrupoQuePuedeFacturar:=False, tieneLineaTiendaOnline:=False))
    End Sub

    <TestMethod()>
    Public Sub MostrarBotonesFacturacion_UsuarioDelGrupoSinPedido_True()
        ' El grupo almacén/tiendas ve los botones siempre, haya o no pedido cargado (como antes).
        Dim vm = CrearViewModel()
        vm.EsGrupoQuePuedeFacturar = True

        Assert.IsTrue(vm.MostrarBotonesFacturacion)
    End Sub

    <TestMethod()>
    Public Sub MostrarBotonesFacturacion_FueraDelGrupoSinPedido_False()
        Dim vm = CrearViewModel()
        vm.EsGrupoQuePuedeFacturar = False

        Assert.IsFalse(vm.MostrarBotonesFacturacion)
    End Sub

    <TestMethod()>
    Public Sub ConfirmarSiPlazoNoPermitido_PlazoPermitido_DevuelveTrueSinPreguntar()
        ' Arrange
        Dim vm = CrearViewModel()
        vm.PlazoNoPermitido = False

        ' Act
        Dim resultado = vm.ConfirmarSiPlazoNoPermitido()

        ' Assert
        Assert.IsTrue(resultado, "Si el plazo está permitido, debe devolver True sin preguntar al usuario.")
        A.CallTo(Sub() dialogService.ShowDialog(
                    A(Of String).Ignored,
                    A(Of IDialogParameters).Ignored,
                    A(Of Action(Of IDialogResult)).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub ConfirmarSiPlazoNoPermitido_PlazoNoPermitidoYUsuarioConfirma_DevuelveTrue()
        ' Arrange
        Dim vm = CrearViewModel()
        vm.PlazoNoPermitido = True
        ConfigurarRespuestaConfirmacion(respuestaOk:=True)

        ' Act
        Dim resultado = vm.ConfirmarSiPlazoNoPermitido()

        ' Assert
        Assert.IsTrue(resultado, "Si el usuario confirma, debe devolver True para que continúe la facturación.")
    End Sub

    <TestMethod()>
    Public Sub ConfirmarSiPlazoNoPermitido_PlazoNoPermitidoYUsuarioCancela_DevuelveFalse()
        ' Arrange
        Dim vm = CrearViewModel()
        vm.PlazoNoPermitido = True
        ConfigurarRespuestaConfirmacion(respuestaOk:=False)

        ' Act
        Dim resultado = vm.ConfirmarSiPlazoNoPermitido()

        ' Assert
        Assert.IsFalse(resultado, "Si el usuario cancela, debe devolver False para abortar la facturación.")
    End Sub

    ' Nesto#421 (caso real 922687): al crear albarán y factura, el albarán se creó pero la
    ' factura falló y la pantalla seguía pintando las líneas en estado 1 y activas. El
    ' reintento decía "no hay líneas para albaranear" y desconcertaba en pleno mostrador.

    <TestMethod()>
    Public Sub CrearAlbaranYFactura_SiLaFacturaFalla_RecargaElPedidoYAvisaQueElAlbaranSiSeCreo()
        ' Arrange: todas las confirmaciones responden OK y se capturan los mensajes de diálogo
        Dim mensajesDialogo As New List(Of String)
        A.CallTo(Sub() dialogService.ShowDialog(
                    A(Of String).Ignored,
                    A(Of IDialogParameters).Ignored,
                    A(Of Action(Of IDialogResult)).Ignored)) _
         .Invokes(Sub(nombre As String, parametros As IDialogParameters, callback As Action(Of IDialogResult))
                      If parametros IsNot Nothing AndAlso parametros.ContainsKey("message") Then
                          mensajesDialogo.Add(parametros.GetValue(Of String)("message"))
                      End If
                      ' ShowError llama a ShowDialog SIN callback: no hay nada que responder.
                      If callback Is Nothing Then
                          Return
                      End If
                      Dim resultado = A.Fake(Of IDialogResult)
                      A.CallTo(Function() resultado.Result).Returns(ButtonResult.OK)
                      callback(resultado)
                  End Sub)

        Dim vm = CrearViewModel()
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .numero = 922687})
        A.CallTo(Function() servicio.CrearAlbaranVenta("1", 922687)).Returns(Task.FromResult(725978))
        A.CallTo(Function() servicio.CrearFacturaVenta("1", 922687)) _
            .Throws(New Exception("Hay lineas que no tienen el visto bueno dado."))

        ' Act
        vm.CrearAlbaranYFacturaVentaCommand.Execute()

        ' Assert: recarga el pedido (para pintar las líneas en su estado real: albarán, bloqueadas)
        A.CallTo(Function() servicio.cargarPedido("1", 922687)).MustHaveHappened()
        ' ...y el error dice explícitamente que el albarán SÍ se creó y cómo reintentar
        Assert.IsTrue(mensajesDialogo.Any(Function(m) m.Contains("725978") AndAlso m.Contains("NO se pudo crear")),
            "El mensaje debe decir que el albarán 725978 se creó y la factura no. Mensajes: " & String.Join(" || ", mensajesDialogo))
    End Sub

    ' Nesto#420: el aviso ReembolsoEnvioSinAjustar del PUT ofrece restar la comisión quitada
    ' del reembolso del envío, siempre con el visto bueno explícito del usuario.

    Private Shared Function AvisoReembolso(ajustable As Boolean) As List(Of AvisoPedidoModel)
        Return New List(Of AvisoPedidoModel) From {
            New AvisoPedidoModel With {
                .Tipo = "ReembolsoEnvioSinAjustar",
                .Mensaje = "Se ha quitado la comisión contra reembolso (3,15 €)...",
                .Datos = Newtonsoft.Json.Linq.JObject.FromObject(New With {
                    .Envio = 245812, .Reembolso = 73.86D, .ComisionQuitada = 3.15D, .Ajustable = ajustable})
            }
        }
    End Function

    <TestMethod()>
    Public Async Function ProcesarAvisos_ReembolsoAjustableYUsuarioConfirma_RestaLaComision() As Task
        ConfigurarRespuestaConfirmacion(respuestaOk:=True)
        A.CallTo(Function() servicio.RestarReembolsoEnvio(245812, 3.15D)).Returns(Task.FromResult(70.71D))
        Dim vm = CrearViewModel()

        Await vm.ProcesarAvisosModificacion(AvisoReembolso(ajustable:=True))

        A.CallTo(Function() servicio.RestarReembolsoEnvio(245812, 3.15D)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function ProcesarAvisos_UsuarioRechaza_NoTocaElReembolso() As Task
        ConfigurarRespuestaConfirmacion(respuestaOk:=False)
        Dim vm = CrearViewModel()

        Await vm.ProcesarAvisosModificacion(AvisoReembolso(ajustable:=True))

        A.CallTo(Function() servicio.RestarReembolsoEnvio(A(Of Integer).Ignored, A(Of Decimal).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ProcesarAvisos_EnvioTramitadoNoAjustable_SoloInformaSinAccion() As Task
        ' Carlos 21/07/26: un envío ya tramitado no se toca (la agencia ya tiene el importe);
        ' en todo caso se abona la comisión después. El aviso llega con Ajustable=False.
        ConfigurarRespuestaConfirmacion(respuestaOk:=True)
        Dim vm = CrearViewModel()

        Await vm.ProcesarAvisosModificacion(AvisoReembolso(ajustable:=False))

        A.CallTo(Function() servicio.RestarReembolsoEnvio(A(Of Integer).Ignored, A(Of Decimal).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ProcesarAvisos_TiposDesconocidosONothing_NoHaceNada() As Task
        Dim vm = CrearViewModel()

        Await vm.ProcesarAvisosModificacion(Nothing)
        Await vm.ProcesarAvisosModificacion(New List(Of AvisoPedidoModel) From {
            New AvisoPedidoModel With {.Tipo = "AvisoFuturoDesconocido", .Mensaje = "..."}})

        A.CallTo(Function() servicio.RestarReembolsoEnvio(A(Of Integer).Ignored, A(Of Decimal).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub CrearAlbaranYFactura_SiFallaElAlbaran_NoRecargaNiFactura()
        ' Si el albarán ni siquiera se creó, no hay nada que recargar ni factura que intentar.
        ConfigurarRespuestaConfirmacion(respuestaOk:=True)
        Dim vm = CrearViewModel()
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .numero = 922687})
        A.CallTo(Function() servicio.CrearAlbaranVenta("1", 922687)).Throws(New Exception("boom"))

        vm.CrearAlbaranYFacturaVentaCommand.Execute()

        A.CallTo(Function() servicio.CrearFacturaVenta(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.cargarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
    End Sub
End Class
