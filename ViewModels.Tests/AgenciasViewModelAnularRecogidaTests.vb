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
''' Carlos 20/11/26 (NR930553, parada 251104: una recogida ya procesada que ya no hacía falta y no se podía anular de ninguna manera):
''' «Anular recogida» en Agencias › Tramitados sobre la parada del reparto propio (POST api/RepartoPropio/Recogidas/{pedido}/Anular).
''' </summary>
<TestClass()>
Public Class AgenciasViewModelAnularRecogidaTests
    Private Const REPARTO_PROPIO As Integer = Constantes.Agencias.AGENCIA_REPARTO_PROPIO

    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel
    Private parada As EnviosAgencia

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        configuracion = A.Fake(Of IConfiguracion)
        dialogService = A.Fake(Of IServicioDialogos)
        A.CallTo(Function() configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(True)
        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        parada = New EnviosAgencia With {.Numero = 251104, .Agencia = REPARTO_PROPIO, .Pedido = 930553, .Estado = 1, .Nombre = "SALÓN DE PRUEBA"}
        viewModel.listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia) From {parada}
        viewModel.envioActual = parada
        Fake.ClearRecordedCalls(dialogService)
    End Sub

    Private Sub ElUsuarioEscribeYConfirma(motivo As String, confirma As Boolean)
        A.CallTo(Function() dialogService.GetText(AnuladorRecogidas.TITULO, A(Of String).Ignored)).Returns(motivo)
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(AnuladorRecogidas.TITULO, A(Of String).Ignored, True)).Returns(confirma)
    End Sub

    <TestMethod()>
    Public Async Function AnularRecogida_ConMotivoYConfirmada_LlamaYQuitaLaParadaDeTramitados() As Task
        ElUsuarioEscribeYConfirma("  Ya está procesada, no hace falta ir  ", True)
        A.CallTo(Function() servicio.AnularRecogida(930553, "Ya está procesada, no hace falta ir")).
            Returns(Task.FromResult(New AnulacionRecogidaModel With {.Pedido = 930553, .CodigoDocumento = "NR930553", .EnvioId = 251104, .Estado = "Anulada", .Mensaje = "Recogida NR930553 anulada."}))

        Await viewModel.AnularRecogidaSeleccionada()

        A.CallTo(Function() servicio.AnularRecogida(930553, "Ya está procesada, no hace falta ir")).MustHaveHappenedOnceExactly()
        Assert.IsFalse(viewModel.listaEnviosTramitados.Contains(parada))
        A.CallTo(Sub() dialogService.ShowNotification(AnuladorRecogidas.TITULO, "Recogida NR930553 anulada.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() dialogService.GetText(AnuladorRecogidas.TITULO, A(Of String).That.Contains("NR930553 (parada 251104)"))).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function AnularRecogida_SinMotivo_NoLlama() As Task
        ElUsuarioEscribeYConfirma("  ", True)

        Await viewModel.AnularRecogidaSeleccionada()

        A.CallTo(Function() servicio.AnularRecogida(A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(AnuladorRecogidas.MENSAJE_FALTA_MOTIVO)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function AnularRecogida_NoConfirma_NoLlama() As Task
        ElUsuarioEscribeYConfirma("ya no", False)

        Await viewModel.AnularRecogidaSeleccionada()

        A.CallTo(Function() servicio.AnularRecogida(A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        Assert.IsTrue(viewModel.listaEnviosTramitados.Contains(parada))
    End Function

    <TestMethod()>
    Public Async Function AnularRecogida_YaRecogida409_SuMensajeYSigueEnLaLista() As Task
        ElUsuarioEscribeYConfirma("ya no", True)
        A.CallTo(Function() servicio.AnularRecogida(930553, "ya no")).Throws(
            ErroresApiReparto.Excepcion(409, "{""Message"":""La recogida NR930553 ya está recogida: los bultos NR los lleva Andre."",""Codigo"":""RECOGIDA_YA_RECOGIDA""}", Nothing))

        Await viewModel.AnularRecogidaSeleccionada()

        A.CallTo(Sub() dialogService.ShowError("La recogida NR930553 ya está recogida: los bultos NR los lleva Andre.")).MustHaveHappenedOnceExactly()
        Assert.IsTrue(viewModel.listaEnviosTramitados.Contains(parada))
    End Function

    <TestMethod()>
    Public Async Function AnularRecogida_NoEsNotaDeRecogida404_SuMensaje() As Task
        ElUsuarioEscribeYConfirma("ya no", True)
        A.CallTo(Function() servicio.AnularRecogida(930553, "ya no")).Throws(
            ErroresApiReparto.Excepcion(404, "{""Message"":""El pedido 930553 no es una nota de recogida.""}", Nothing))

        Await viewModel.AnularRecogidaSeleccionada()

        A.CallTo(Sub() dialogService.ShowError("El pedido 930553 no es una nota de recogida.")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub AnularRecogidaCommand_SoloParadasDelRepartoPropioYConGrupo()
        Assert.IsTrue(viewModel.AnularRecogidaCommand.CanExecute(Nothing))
        Assert.IsTrue(viewModel.PuedeAnularRecogidas)

        viewModel.envioActual = New EnviosAgencia With {.Numero = 1, .Agencia = 1, .Pedido = 930553, .Estado = 1}
        Assert.IsFalse(viewModel.AnularRecogidaCommand.CanExecute(Nothing), "otra agencia")

        viewModel.envioActual = New EnviosAgencia With {.Numero = 2, .Agencia = REPARTO_PROPIO, .Pedido = 930553, .Estado = 2}
        Assert.IsFalse(viewModel.AnularRecogidaCommand.CanExecute(Nothing), "ya entregada (recogida)")

        Dim sinGrupo = New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogService, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        sinGrupo.envioActual = parada
        Assert.IsFalse(sinGrupo.AnularRecogidaCommand.CanExecute(Nothing), "solo Almacén, Administración, Dirección e Informática")
        Assert.IsFalse(sinGrupo.PuedeAnularRecogidas)
    End Sub
End Class
