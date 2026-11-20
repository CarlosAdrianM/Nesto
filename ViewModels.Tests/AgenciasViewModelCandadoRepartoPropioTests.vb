Imports ControlesUsuario.Services
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' Carlos 20/11/26 («en el reparto propio manda el bulto»): Administración dio por cerrado a mano un envío que un repartidor acababa
''' de cargar y la parada desapareció; y se borró otro con la etiqueta ya impresa. La API lo impide con 409 { Message, Codigo }
''' (EN_FURGONETA, CIERRE_NO_PERMITIDO); Nesto enseña su Message tal cual en todas las acciones, deja la pantalla como estaba y no
''' ofrece «Dar por cerrado» para la agencia 14. Además, «Añadir nota» a la historia de un envío.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelCandadoRepartoPropioTests
    Private Const REPARTO_PROPIO As Integer = Constantes.Agencias.AGENCIA_REPARTO_PROPIO
    Private Const GLS As Integer = 1
    Private Const MENSAJE_FURGONETA As String = "No se puede: el envío 251169 lo lleva Andre en la furgoneta desde las 9:42."
    Private Const CUERPO_FURGONETA As String = "{""Message"":""" & MENSAJE_FURGONETA & """,""Codigo"":""EN_FURGONETA"",""Repartidor"":""Andre"",""Desde"":""2026-11-20T09:42:00""}"
    Private Const CUERPO_CIERRE As String = "{""Message"":""Un envío del reparto propio no se cierra a mano."",""Codigo"":""CIERRE_NO_PERMITIDO""}"

    Private servicio As IAgenciaService
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        viewModel = New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
    End Sub

    Private Shared Function Envio(numero As Integer, agencia As Integer, estado As Short) As EnviosAgencia
        Return New EnviosAgencia With {.Numero = numero, .Agencia = agencia, .Pedido = 930553, .Estado = estado, .CodigoBarras = "RP" & numero}
    End Function

    Private Shared Function Bloqueado() As EnvioRepartoPropioBloqueadoException
        Return EnvioRepartoPropioBloqueadoException.DesdeRespuesta(409, CUERPO_FURGONETA)
    End Function

    ' ---- a) «Dar por cerrado» no se ofrece para la 14 ----

    <TestMethod()>
    Public Sub DarPorCerrado_IncidentadoDelRepartoPropio_NoSeOfreceYSeDiceElMotivo()
        viewModel.envioActual = Envio(251169, REPARTO_PROPIO, AgenciasViewModel.ESTADO_INCIDENTADO)

        Assert.IsFalse(viewModel.DarPorCerradoCommand.CanExecute(Nothing))
        Assert.AreEqual("Un envío del reparto propio no se da por cerrado: se entrega en Ariadna o se anula con sus bultos de vuelta en el almacén.",
                        viewModel.MotivoNoDarPorCerrado)
    End Sub

    <TestMethod()>
    Public Sub DarPorCerrado_IncidentadoDeOtraAgencia_SeOfreceSinMotivo()
        viewModel.envioActual = Envio(251170, GLS, AgenciasViewModel.ESTADO_INCIDENTADO)

        Assert.IsTrue(viewModel.DarPorCerradoCommand.CanExecute(Nothing))
        Assert.AreEqual(String.Empty, viewModel.MotivoNoDarPorCerrado)
    End Sub

    <TestMethod()>
    Public Async Function DarPorCerrado_RepartoPropio_NiPreguntaNiLlama() As Task
        viewModel.envioActual = Envio(251169, REPARTO_PROPIO, AgenciasViewModel.ESTADO_INCIDENTADO)

        Await viewModel.DarPorCerradoSeleccionado()

        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.DarPorCerradoEnvio(A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(AgenciasViewModel.MOTIVO_NO_CERRAR_REPARTO_PROPIO)).MustHaveHappenedOnceExactly()
    End Function

    ' ---- b) el 409 de la API, tal cual, en cada acción ----

    <TestMethod()>
    Public Sub DesdeRespuesta_EnFurgoneta_LeeMensajeRepartidorYDesde()
        Dim ex = EnvioRepartoPropioBloqueadoException.DesdeRespuesta(409, CUERPO_FURGONETA)

        Assert.IsNotNull(ex)
        Assert.AreEqual(MENSAJE_FURGONETA, ex.Message)
        Assert.AreEqual("EN_FURGONETA", ex.Codigo)
        Assert.AreEqual("Andre", ex.Repartidor)
        Assert.AreEqual(New Date(2026, 11, 20, 9, 42, 0), ex.Desde.Value)
    End Sub

    <TestMethod()>
    Public Sub DesdeRespuesta_CierreNoPermitido_SinRepartidor()
        Dim ex = EnvioRepartoPropioBloqueadoException.DesdeRespuesta(409, CUERPO_CIERRE)

        Assert.AreEqual("CIERRE_NO_PERMITIDO", ex.Codigo)
        Assert.AreEqual("Un envío del reparto propio no se cierra a mano.", ex.Message)
        Assert.IsNull(ex.Repartidor)
        Assert.IsFalse(ex.Desde.HasValue)
    End Sub

    <TestMethod()>
    Public Sub DesdeRespuesta_OtroCodigoOtroEstadoONoJson_Nothing()
        Assert.IsNull(EnvioRepartoPropioBloqueadoException.DesdeRespuesta(400, CUERPO_FURGONETA), "solo el 409")
        Assert.IsNull(EnvioRepartoPropioBloqueadoException.DesdeRespuesta(409, "{""Message"":""x"",""Codigo"":""ESPERANDO_CAJA""}"))
        Assert.IsNull(EnvioRepartoPropioBloqueadoException.DesdeRespuesta(409, """ya está entregado"""))
        Assert.IsNull(EnvioRepartoPropioBloqueadoException.DesdeRespuesta(409, "<html>error</html>"))
        Assert.IsNull(EnvioRepartoPropioBloqueadoException.DesdeRespuesta(409, "{""Codigo"":""EN_FURGONETA""}"), "sin Message no hay qué enseñar")
    End Sub

    <TestMethod()>
    Public Sub LeerRespuestaModificar_CambiarEstadoOAgenciaEnFurgoneta_LanzaConElMensaje()
        Dim envio14 = Envio(251169, REPARTO_PROPIO, 1)

        Dim ex = Assert.ThrowsException(Of EnvioRepartoPropioBloqueadoException)(
            Function() AgenciaService.LeerRespuestaModificar(envio14, 409, CUERPO_FURGONETA))

        Assert.AreEqual(MENSAJE_FURGONETA, ex.Message)
    End Sub

    <TestMethod()>
    Public Sub LeerRespuestaModificar_400ConMessage_SoloElTextoSinJson()
        Dim ex = Assert.ThrowsException(Of Exception)(
            Function() AgenciaService.LeerRespuestaModificar(Envio(1, GLS, 1), 400, "{""Message"":""El envío ya está facturado.""}"))

        Assert.AreEqual("El envío ya está facturado.", ex.Message)
    End Sub

    <TestMethod()>
    Public Sub TextoErrorOperacion_MensajeEnUn4xx_ElMensaje_EnUn5xx_ConElDetalle()
        Assert.AreEqual("No se puede modificar este envío, porque ya está cobrado",
                        AgenciaService.TextoErrorOperacion(400, "{""Message"":""No se puede modificar este envío, porque ya está cobrado""}", "NestoAPI rechazó la modificación del envío"))
        Assert.AreEqual("NestoAPI rechazó la modificación del envío (500): {""Message"":""An error has occurred.""}",
                        AgenciaService.TextoErrorOperacion(500, "{""Message"":""An error has occurred.""}", "NestoAPI rechazó la modificación del envío"))
        Assert.AreEqual("No se pudo borrar el envío 5 (409): ""texto""", AgenciaService.TextoErrorOperacion(409, """texto""", "No se pudo borrar el envío 5"))
    End Sub

    <TestMethod()>
    Public Sub MensajeErrorModificacion_400ConMessage_YaNoEnsenaElJson()
        Assert.AreEqual("No se puede.", AgenciaService.MensajeErrorModificacion(400, "{""Message"":""No se puede.""}", "la modificación del envío"))
    End Sub

    <TestMethod()>
    Public Sub LanzarSiRepartoPropioBloqueado_SoloConElCodigoDelRepartoPropio()
        Assert.ThrowsException(Of EnvioRepartoPropioBloqueadoException)(Sub() AgenciaService.LanzarSiRepartoPropioBloqueado(409, CUERPO_CIERRE))
        AgenciaService.LanzarSiRepartoPropioBloqueado(409, "{""Message"":""otro"",""Codigo"":""OTRO""}")
        AgenciaService.LanzarSiRepartoPropioBloqueado(500, CUERPO_FURGONETA)
    End Sub

    <TestMethod()>
    Public Async Function DarPorCerrado_LaApiDiceEnFurgoneta_SoloSuMensajeYSigueEnLaLista() As Task
        Dim incidentado = Envio(251300, GLS, AgenciasViewModel.ESTADO_INCIDENTADO)
        viewModel.listaIncidentados = New ObservableCollection(Of EnviosAgencia) From {incidentado}
        viewModel.envioActual = incidentado
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored)).Returns("ya lo tiene")
        A.CallTo(Function() servicio.DarPorCerradoEnvio(251300, "ya lo tiene")).Throws(Bloqueado())

        Await viewModel.DarPorCerradoSeleccionado()

        A.CallTo(Sub() dialogService.ShowError(MENSAJE_FURGONETA)).MustHaveHappenedOnceExactly()
        Assert.IsTrue(viewModel.listaIncidentados.Contains(incidentado))
    End Function

    <TestMethod()>
    Public Async Function Anular_LaApiDiceEnFurgoneta_SoloSuMensajeYSigueEnTramitados() As Task
        Dim tramitado = Envio(251169, REPARTO_PROPIO, 1)
        viewModel.listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia) From {tramitado}
        viewModel.envioActual = tramitado
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)
        A.CallTo(Function() servicio.AnularEnvioRemoto(251169)).Throws(Bloqueado())

        Await viewModel.AnularEnvioRepartoPropioSeleccionado()

        A.CallTo(Sub() dialogService.ShowError(MENSAJE_FURGONETA)).MustHaveHappenedOnceExactly()
        Assert.IsTrue(viewModel.listaEnviosTramitados.Contains(tramitado))
    End Function

    <TestMethod()>
    Public Async Function ModificarEstado_LaApiDiceEnFurgoneta_SoloSuMensajeYNoTocaElEnvio() As Task
        Dim tramitado = Envio(251171, GLS, 1)
        tramitado.Reembolso = 10D
        viewModel.listaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {New tipoIdDescripcion(0, "Sin retorno")}
        A.CallTo(Function() servicio.ModificarDatosEnvio(A(Of Integer).Ignored, A(Of ModificarDatosEnvioDto).Ignored)).Throws(Bloqueado())

        Await viewModel.ModificarEnvioPorApi(tramitado, 10, New tipoIdDescripcion(0, "Sin retorno"), 2, False, Nothing)

        A.CallTo(Sub() dialogService.ShowError(MENSAJE_FURGONETA)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(CShort(1), tramitado.Estado)
    End Function

    <TestMethod()>
    Public Sub Borrar_LaApiDiceEnFurgoneta_ElMensajeDeBorradoEsElDeLaApi()
        Assert.AreEqual(MENSAJE_FURGONETA, ErroresBorradoEnvio.MensajeParaUsuario(Bloqueado()))
    End Sub

    <TestMethod()>
    Public Sub MensajeErrorEnvio_ElDelRepartoPropioSinPrefijo()
        Assert.AreEqual(MENSAJE_FURGONETA, AgenciasViewModel.MensajeErrorEnvio("No se pudo: ", Bloqueado()))
        Assert.AreEqual("No se pudo: otro", AgenciasViewModel.MensajeErrorEnvio("No se pudo: ", New Exception("otro")))
    End Sub

    ' ---- c) «Añadir nota» ----

    <TestMethod()>
    Public Async Function AnadirNota_ConTexto_LlamaRecortadoYRecargaLaHistoria() As Task
        Dim tramitado = Envio(251104, REPARTO_PROPIO, 1)
        viewModel.envioActual = tramitado
        A.CallTo(Function() dialogService.GetText(AnotadorEnvios.TITULO, A(Of String).Ignored)).Returns("  El cliente pide que se llame antes  ")
        A.CallTo(Function() servicio.AnadirNotaEnvio(251104, "El cliente pide que se llame antes")).
            Returns(Task.FromResult(New NotaEnvioModel With {.Numero = 77, .Texto = "El cliente pide que se llame antes", .Usuario = "carlos"}))
        Dim historia As New ObservableCollection(Of EnviosHistoria) From {New EnviosHistoria With {.Numero = 77, .Observaciones = "El cliente pide que se llame antes"}}
        A.CallTo(Function() servicio.CargarListaHistoriaEnvio(251104)).Returns(historia)

        Await viewModel.AnadirNotaEnvioSeleccionado()

        A.CallTo(Function() servicio.AnadirNotaEnvio(251104, "El cliente pide que se llame antes")).MustHaveHappenedOnceExactly()
        Assert.AreSame(historia, viewModel.listaHistoriaEnvio)
        A.CallTo(Sub() dialogService.ShowNotification(AnotadorEnvios.TITULO, A(Of String).That.Contains("251104"))).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function AnadirNota_VaciaOLarga_AvisaYNoLlama() As Task
        viewModel.envioActual = Envio(251104, REPARTO_PROPIO, 1)
        A.CallTo(Function() dialogService.GetText(AnotadorEnvios.TITULO, A(Of String).Ignored)).ReturnsNextFromSequence("   ", New String("x"c, 501))

        Await viewModel.AnadirNotaEnvioSeleccionado()
        Await viewModel.AnadirNotaEnvioSeleccionado()

        A.CallTo(Function() servicio.AnadirNotaEnvio(A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(AnotadorEnvios.MENSAJE_VACIA)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowError(AnotadorEnvios.MENSAJE_LARGA)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function AnadirNota_ServidorSinActualizar_LoDiceClaroYNoRecarga() As Task
        viewModel.envioActual = Envio(251104, REPARTO_PROPIO, 1)
        A.CallTo(Function() dialogService.GetText(AnotadorEnvios.TITULO, A(Of String).Ignored)).Returns("nota")
        A.CallTo(Function() servicio.AnadirNotaEnvio(251104, "nota")).Throws(ErroresApiReparto.Excepcion(404, "{""Message"":""No HTTP resource was found that matches the request URI.""}", Nothing))
        Fake.ClearRecordedCalls(servicio)

        Await viewModel.AnadirNotaEnvioSeleccionado()

        A.CallTo(Sub() dialogService.ShowError(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CargarListaHistoriaEnvio(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub AnadirNotaCommand_SoloConUnEnvioGuardadoSeleccionado()
        Assert.IsFalse(viewModel.AnadirNotaEnvioCommand.CanExecute(Nothing))
        viewModel.envioActual = Envio(251104, GLS, 1)
        Assert.IsTrue(viewModel.AnadirNotaEnvioCommand.CanExecute(Nothing))
    End Sub
End Class
