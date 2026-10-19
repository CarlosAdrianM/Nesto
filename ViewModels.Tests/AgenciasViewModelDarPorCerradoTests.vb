Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports Newtonsoft.Json.Linq
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' NestoAPI#676 (Sugerencia 673, Aida): «Dar por cerrado» en la pestaña Incidentados. Pide el motivo, llama a la API
''' (POST api/EnviosAgencias/{id}/DarPorCerrado) y quita el envío de la lista.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelDarPorCerradoTests
    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IServicioDialogos
    Private servicioPedidos As IPedidoVentaService
    Private servicioAutenticacion As IServicioAutenticacion
    Private viewModel As AgenciasViewModel
    Private incidentado As EnviosAgencia
    Private otroIncidentado As EnviosAgencia

    <TestInitialize()>
    Public Sub Initialize()
        configuracion = A.Fake(Of IConfiguracion)
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        servicioPedidos = A.Fake(Of IPedidoVentaService)
        servicioAutenticacion = A.Fake(Of IServicioAutenticacion)
        viewModel = New AgenciasViewModel(servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)

        incidentado = New EnviosAgencia With {.Numero = 251300, .Pedido = 921456, .Estado = CShort(3), .DetalleEstado = "AUSENTE"}
        otroIncidentado = New EnviosAgencia With {.Numero = 251301, .Pedido = 921457, .Estado = CShort(3)}
        viewModel.listaIncidentados = New ObservableCollection(Of EnviosAgencia) From {incidentado, otroIncidentado}
        viewModel.envioActual = incidentado
    End Sub

    Private Sub ElUsuarioEscribe(motivo As String)
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored)).Returns(motivo)
    End Sub

    <TestMethod()>
    Public Async Function DarPorCerrado_ConMotivo_LlamaALaApiYLoQuitaDeLaLista() As Task
        ElUsuarioEscribe("  El cliente confirma que lo recibió  ")

        Await viewModel.DarPorCerradoSeleccionado()

        A.CallTo(Function() servicio.DarPorCerradoEnvio(251300, "El cliente confirma que lo recibió")).MustHaveHappenedOnceExactly()
        Assert.AreEqual(1, viewModel.listaIncidentados.Count)
        Assert.IsFalse(viewModel.listaIncidentados.Contains(incidentado))
        Assert.AreSame(otroIncidentado, viewModel.envioActual, "Queda seleccionado el siguiente de la lista")
        A.CallTo(Sub() dialogService.ShowNotification(A(Of String).Ignored, A(Of String).That.Contains("921456"))).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function DarPorCerrado_PideElMotivoDeUsted() As Task
        ElUsuarioEscribe(Nothing)

        Await viewModel.DarPorCerradoSeleccionado()

        A.CallTo(Function() dialogService.GetText("Dar por cerrado", A(Of String).That.Contains("¿Por qué da por cerrado el envío del pedido 921456?"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function DarPorCerrado_Cancelado_NoLlamaNiTocaLaLista() As Task
        ElUsuarioEscribe(Nothing)

        Await viewModel.DarPorCerradoSeleccionado()

        A.CallTo(Function() servicio.DarPorCerradoEnvio(A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        Assert.AreEqual(2, viewModel.listaIncidentados.Count)
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function DarPorCerrado_MotivoVacio_AvisaDeUstedYNoLlama() As Task
        ElUsuarioEscribe("   ")

        Await viewModel.DarPorCerradoSeleccionado()

        A.CallTo(Function() servicio.DarPorCerradoEnvio(A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError("Indique el motivo por el que da por cerrado el envío.")).MustHaveHappenedOnceExactly()
        Assert.AreEqual(2, viewModel.listaIncidentados.Count)
    End Function

    <TestMethod()>
    Public Async Function DarPorCerrado_LaApiRechaza_SigueEnLaListaYSeEnsenaElMotivo() As Task
        ElUsuarioEscribe("Ya no está")
        A.CallTo(Function() servicio.DarPorCerradoEnvio(251300, "Ya no está")).
            Throws(New Exception("NestoAPI no ha dado por cerrado el envío (400): Solo se puede dar por cerrado un envío incidentado."))

        Await viewModel.DarPorCerradoSeleccionado()

        Assert.AreEqual(2, viewModel.listaIncidentados.Count)
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("Solo se puede dar por cerrado un envío incidentado"))).MustHaveHappenedOnceExactly()
        Assert.IsFalse(viewModel.EstaOcupado)
    End Function

    <TestMethod()>
    Public Sub DarPorCerradoCommand_SoloConUnIncidentadoSeleccionado()
        Assert.IsTrue(viewModel.DarPorCerradoCommand.CanExecute(Nothing))

        viewModel.envioActual = New EnviosAgencia With {.Numero = 1, .Estado = CShort(2)}
        Assert.IsFalse(viewModel.DarPorCerradoCommand.CanExecute(Nothing), "Un entregado no se da por cerrado")

        viewModel.envioActual = Nothing
        Assert.IsFalse(viewModel.DarPorCerradoCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Async Function DarPorCerrado_SinEnvioSeleccionado_NoPreguntaNiLlama() As Task
        viewModel.envioActual = Nothing

        Await viewModel.DarPorCerradoSeleccionado()

        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.DarPorCerradoEnvio(A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub RutaYCuerpoDarPorCerrado_ContratoConElEndpoint()
        Assert.AreEqual("EnviosAgencias/251300/DarPorCerrado", AgenciaService.RutaDarPorCerrado(251300))
        Dim cuerpo = JObject.Parse(AgenciaService.CuerpoDarPorCerrado("El cliente lo tiene"))
        Assert.AreEqual("El cliente lo tiene", CStr(cuerpo("Motivo")), "Mismo nombre que DarPorCerradoEnvioDTO.Motivo en NestoAPI")
    End Sub

    <TestMethod()>
    Public Sub EstadoCerradoAMano_EsElCincoComoEnLaApi()
        ' Espejo de Constantes.Agencias.ESTADO_CERRADO_A_MANO de NestoAPI: las rejillas lo pintan por número.
        Assert.AreEqual(5, Constantes.Agencias.ESTADO_CERRADO_A_MANO_ENVIO)
        Assert.AreEqual(CShort(3), AgenciasViewModel.ESTADO_INCIDENTADO)
    End Sub

    <TestMethod()>
    Public Sub NombreEstado_CerradoAMano_NoDiceEntregado()
        Assert.AreEqual("Cerrado a mano", AgenciasViewModel.NombreEstado(CShort(5)))
        Assert.AreEqual("Entregado", AgenciasViewModel.NombreEstado(CShort(2)))
        Assert.AreEqual("Devuelto", AgenciasViewModel.NombreEstado(CShort(4)))
    End Sub

    <TestMethod()>
    Public Async Function DarPorCerrado_AvisaDeQueSaleDeIncidentados() As Task
        ElUsuarioEscribe(Nothing)

        Await viewModel.DarPorCerradoSeleccionado()

        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).That.Contains("Saldrá de Incidentados"))).MustHaveHappenedOnceExactly()
    End Function
End Class
