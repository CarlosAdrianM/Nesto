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
''' Novedades 1162 (Alfredo, 10/11/26, pedido 930294): el envío 250772 del reparto propio quedó Tramitado al imprimir la etiqueta
''' con un reembolso de 53 € que no tocaba, y en Agencias no se podía ni modificar («ya está cobrado», que no era verdad) ni anular.
''' Ahora se anula desde Tramitados (vuelve a Pendientes) y, si la API no lo deja, se enseña su motivo tal cual.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelAnularRepartoPropioTests
    Private Const REPARTO_PROPIO As Integer = Constantes.Agencias.AGENCIA_REPARTO_PROPIO
    Private Const CTT As Integer = 13

    Private servicio As IAgenciaService
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel
    Private envio As EnviosAgencia

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        viewModel = New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        envio = Tramitado(250772, REPARTO_PROPIO)
        viewModel.listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia) From {envio, Tramitado(250773, CTT)}
        viewModel.envioActual = envio
        Fake.ClearRecordedCalls(dialogService)
    End Sub

    Private Shared Function Tramitado(numero As Integer, agencia As Integer) As EnviosAgencia
        Return New EnviosAgencia With {.Numero = numero, .Agencia = agencia, .Pedido = 930294, .Estado = 1, .Reembolso = 53D,
            .CodigoBarras = "RP" & numero, .FechaPagoReembolso = New Date(2026, 11, 11), .FechaEntrega = New Date(2026, 11, 11)}
    End Function

    Private Sub ElUsuarioConfirma(ok As Boolean)
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(ok)
    End Sub

    ' ---- Modificar: el texto ----

    <TestMethod()>
    Public Async Function ModificarEnvioPorApi_RepartoPropioTramitado_DiceQueHayQueAnularloYNoQueEstaCobrado() As Task
        Await viewModel.ModificarEnvioPorApi(envio, 0, New tipoIdDescripcion(1, "Sin retorno"), 1, False, envio.FechaEntrega)

        A.CallTo(Sub() dialogService.ShowError(
            "Para cambiar un envío del reparto propio ya tramitado, anúlelo (vuelve a Pendientes), corríjalo y vuelva a imprimir la etiqueta.")) _
            .MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.ModificarDatosEnvio(A(Of Integer).Ignored, A(Of ModificarDatosEnvioDto).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub MensajeNoSePuedeModificar_OtraAgencia_ElDeSiempre()
        Assert.AreEqual("No se puede modificar este envío, porque ya está cobrado", AgenciasViewModel.MensajeNoSePuedeModificar(Tramitado(1, CTT)))
        Assert.AreEqual(AgenciasViewModel.MENSAJE_MODIFICAR_REPARTO_PROPIO_TRAMITADO, AgenciasViewModel.MensajeNoSePuedeModificar(envio))
    End Sub

    ' ---- Anular: cuándo se puede ----

    <TestMethod()>
    Public Sub PuedeAnular_SoloElRepartoPropioTramitadoConEtiqueta()
        Assert.IsTrue(AgenciasViewModel.PuedeAnularEnvioRepartoPropio(envio))
        Assert.IsTrue(viewModel.AnularEnvioRepartoPropioCommand.CanExecute(Nothing))

        Assert.IsFalse(AgenciasViewModel.PuedeAnularEnvioRepartoPropio(Tramitado(2, CTT)), "las demás agencias, como siempre")
        Dim entregado = Tramitado(3, REPARTO_PROPIO)
        entregado.Estado = 2
        Assert.IsFalse(AgenciasViewModel.PuedeAnularEnvioRepartoPropio(entregado))
        Dim sinEtiqueta = Tramitado(4, REPARTO_PROPIO)
        sinEtiqueta.CodigoBarras = Nothing
        Assert.IsFalse(AgenciasViewModel.PuedeAnularEnvioRepartoPropio(sinEtiqueta))
        Assert.IsFalse(AgenciasViewModel.PuedeAnularEnvioRepartoPropio(Nothing))

        viewModel.envioActual = viewModel.listaEnviosTramitados(1)
        Assert.IsFalse(viewModel.AnularEnvioRepartoPropioCommand.CanExecute(Nothing))
    End Sub

    ' ---- Anular: el flujo ----

    <TestMethod()>
    Public Async Function Anular_Confirmado_LlamaALaApiYLoQuitaDeTramitados() As Task
        ElUsuarioConfirma(True)

        Await viewModel.AnularEnvioRepartoPropioSeleccionado()

        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Anular envío",
            A(Of String).That.Contains("El envío volverá a etiqueta pendiente; corríjalo en Pendientes y vuelva a imprimirlo."))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.AnularEnvioRemoto(250772)).MustHaveHappenedOnceExactly()
        Assert.IsFalse(viewModel.listaEnviosTramitados.Contains(envio))
        Assert.AreEqual(250773, viewModel.envioActual.Numero)
        A.CallTo(Sub() dialogService.ShowNotification("Anular envío", A(Of String).That.Contains("ha vuelto a Pendientes"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Anular_Cancelado_NoLlamaALaApi() As Task
        ElUsuarioConfirma(False)

        Await viewModel.AnularEnvioRepartoPropioSeleccionado()

        A.CallTo(Function() servicio.AnularEnvioRemoto(A(Of Integer).Ignored)).MustNotHaveHappened()
        Assert.IsTrue(viewModel.listaEnviosTramitados.Contains(envio))
    End Function

    <TestMethod()>
    Public Async Function Anular_LaApiNoLoDeja_EnseñaSuMotivoTalCualYNoTocaLaLista() As Task
        ElUsuarioConfirma(True)
        Dim motivo = "El envío 250772 ya está cargado en la furgoneta: no se puede anular. Si al final no se entrega, el repartidor lo marcará como incidencia."
        A.CallTo(Function() servicio.AnularEnvioRemoto(250772)).Throws(New Exception(AgenciaService.MensajeErrorAnulacion(409, """" & motivo & """")))

        Await viewModel.AnularEnvioRepartoPropioSeleccionado()

        A.CallTo(Sub() dialogService.ShowError(motivo)).MustHaveHappenedOnceExactly()
        Assert.IsTrue(viewModel.listaEnviosTramitados.Contains(envio))
        Assert.AreSame(envio, viewModel.envioActual)
    End Function

    <TestMethod()>
    Public Sub MensajeErrorAnulacion_409_ElTextoDeLaApiSinComillasNiCodigo()
        Assert.AreEqual("ya está entregado", AgenciaService.MensajeErrorAnulacion(409, """ya está entregado"""))
        Assert.AreEqual("otro motivo", AgenciaService.MensajeErrorAnulacion(409, "{""Message"":""otro motivo""}"))
        Assert.AreEqual("NestoAPI rechazó la anulación (400): ""x""", AgenciaService.MensajeErrorAnulacion(400, """x"""), "los demás, como siempre")
    End Sub

    ' ---- Registrar un pendiente: el 409 de #847 ----

    <TestMethod()>
    Public Sub LeerRespuestaModificar_409RepartoPropioRepetido_LanzaLaDelPostConElMensajeDeLaApi()
        Dim mensaje = "El pedido 930294 ya tiene el envío 250772 del reparto propio (1 bulto): no se crea otro."
        Dim cuerpo = "{""Codigo"":""ENVIO_REPARTO_PROPIO_REPETIDO"",""Message"":""" & mensaje & """,""Envio"":250772,""CodigoBarras"":""RP250772"",""Bultos"":1,""Estado"":1}"

        Dim lanzada As EnvioRepartoPropioRepetidoException = Nothing
        Try
            AgenciaService.LeerRespuestaModificar(New EnviosAgencia With {.Numero = 250782}, 409, cuerpo)
        Catch ex As EnvioRepartoPropioRepetidoException
            lanzada = ex
        End Try

        Assert.IsNotNull(lanzada, "la misma que el POST: «Insertar e Imprimir» ofrece reimprimir el que ya hay")
        Assert.AreEqual(250772, lanzada.NumeroEnvio)
        Assert.AreEqual(mensaje, lanzada.Message)
    End Sub
End Class
