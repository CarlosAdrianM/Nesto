Imports ControlesUsuario.Dialogs
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
''' NestoAPI#699 (corte 2): pestaña Devueltos de Agencias. «El cliente lo sigue queriendo: volver a enviar…» abre el diálogo con
''' la dirección del envío (editable) y la casilla de portes, y llama a POST api/EnviosAgencias/{id}/Reenviar; «No lo quiere:
''' abonar» pide confirmación y llama a POST …/Abonar. Una decisión por envío.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelDevueltosTests
    Private servicio As IAgenciaService
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel
    Private devuelto As EnvioDevueltoModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        viewModel = New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogService, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.empresaSeleccionada = New Empresas With {.Número = "1  "}

        devuelto = New EnvioDevueltoModel With {
            .Numero = 251300, .Pedido = 925137, .Nombre = "Peluquería Ejemplo", .Estado = EnvioDevueltoModel.ESTADO_DEVUELTO,
            .DetalleEstado = "DEVOLUCIÓN", .Direccion = "Calle Vieja 1", .CodPostal = "28001", .Poblacion = "Madrid",
            .Provincia = "Madrid", .Telefono = "910000000", .Movil = "600000000", .Observaciones = "Llamar antes",
            .Decision = EnvioDevueltoModel.SIN_DECIDIR}
        viewModel.listaDevueltos = New ObservableCollection(Of EnvioDevueltoModel) From {devuelto}
        viewModel.DevueltoSeleccionado = devuelto
        A.CallTo(Function() servicio.CargarListaDevueltos(A(Of String).Ignored)).Returns(Task.FromResult(New List(Of EnvioDevueltoModel) From {devuelto}))
    End Sub

    Private Sub ElDialogoDevuelve(boton As ResultadoBoton, Optional direccion As String = "Calle Nueva 7", Optional portes As Boolean = False)
        Dim parametros As New ParametrosDialogo From {
            {ReenviarEnvioDialogViewModel.PARAMETRO_DIRECCION, direccion},
            {ReenviarEnvioDialogViewModel.PARAMETRO_COD_POSTAL, "28100"},
            {ReenviarEnvioDialogViewModel.PARAMETRO_POBLACION, "Alcobendas"},
            {ReenviarEnvioDialogViewModel.PARAMETRO_PROVINCIA, "Madrid"},
            {ReenviarEnvioDialogViewModel.PARAMETRO_TELEFONO, "911111111"},
            {ReenviarEnvioDialogViewModel.PARAMETRO_MOVIL, "600000000"},
            {ReenviarEnvioDialogViewModel.PARAMETRO_OBSERVACIONES, ""},
            {ReenviarEnvioDialogViewModel.PARAMETRO_INCLUIR_PORTES, portes}
        }
        A.CallTo(Function() dialogService.ShowDialogAsync(ReenviarEnvioDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored)).
            Returns(Task.FromResult(New ResultadoDialogo(boton, parametros)))
    End Sub

    <TestMethod()>
    Public Async Function Reenviar_AbreElDialogoConLaDireccionDelEnvio() As Task
        ElDialogoDevuelve(ResultadoBoton.Cancel)

        Await viewModel.ReenviarDevueltoSeleccionado()

        A.CallTo(Function() dialogService.ShowDialogAsync(ReenviarEnvioDialogViewModel.NOMBRE, A(Of ParametrosDialogo).That.Matches(
            Function(p) p.GetValue(Of Integer)(ReenviarEnvioDialogViewModel.PARAMETRO_ENVIO) = 251300 AndAlso
                        p.GetValue(Of String)(ReenviarEnvioDialogViewModel.PARAMETRO_PEDIDO) = "925137" AndAlso
                        p.GetValue(Of String)(ReenviarEnvioDialogViewModel.PARAMETRO_DIRECCION) = "Calle Vieja 1" AndAlso
                        p.GetValue(Of String)(ReenviarEnvioDialogViewModel.PARAMETRO_TELEFONO) = "910000000"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Reenviar_Cancelado_NoLlamaALaApi() As Task
        ElDialogoDevuelve(ResultadoBoton.Cancel)

        Await viewModel.ReenviarDevueltoSeleccionado()

        A.CallTo(Function() servicio.ReenviarEnvioDevuelto(A(Of Integer).Ignored, A(Of ReenvioEnvioPeticion).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Reenviar_Aceptado_MandaLoCorregidoYAvisaDelEnvioNuevo() As Task
        ElDialogoDevuelve(ResultadoBoton.OK, portes:=True)
        A.CallTo(Function() servicio.ReenviarEnvioDevuelto(251300, A(Of ReenvioEnvioPeticion).Ignored)).Returns(Task.FromResult(New DecisionReenvioModel With {
            .EnvioOrigen = 251300, .Decision = EnvioDevueltoModel.REENVIAR, .EnvioNuevo = 300000,
            .Mensaje = "Envío nuevo 300000 creado: imprima la etiqueta cuando llegue la caja."}))

        Await viewModel.ReenviarDevueltoSeleccionado()

        A.CallTo(Function() servicio.ReenviarEnvioDevuelto(251300, A(Of ReenvioEnvioPeticion).That.Matches(
            Function(p) p.Direccion = "Calle Nueva 7" AndAlso p.CodPostal = "28100" AndAlso p.Telefono = "911111111" AndAlso p.IncluirPortes))).
            MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Volver a enviar", A(Of String).That.Contains("Envío nuevo 300000 creado: imprima la etiqueta cuando llegue la caja."))).
            MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CargarListaDevueltos("1  ")).MustHaveHappened()
        Assert.IsFalse(viewModel.EstaOcupado)
    End Function

    <TestMethod()>
    Public Async Function Reenviar_LaApiRechaza_EnsenaElMotivo() As Task
        ElDialogoDevuelve(ResultadoBoton.OK)
        A.CallTo(Function() servicio.ReenviarEnvioDevuelto(A(Of Integer).Ignored, A(Of ReenvioEnvioPeticion).Ignored)).
            Throws(New Exception("El envío 251300 ya se volvió a enviar en el envío 300000."))

        Await viewModel.ReenviarDevueltoSeleccionado()

        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("ya se volvió a enviar"))).MustHaveHappenedOnceExactly()
        Assert.IsFalse(viewModel.EstaOcupado)
    End Function

    <TestMethod()>
    Public Async Function Abonar_Confirmado_LlamaYRemiteACopiarFactura() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Abonar", A(Of String).Ignored)).Returns(True)
        A.CallTo(Function() servicio.AbonarEnvioDevuelto(251300)).Returns(Task.FromResult(New DecisionReenvioModel With {
            .Decision = EnvioDevueltoModel.ABONAR,
            .Mensaje = "Marcado para abonar: hágalo con Copiar factura (abono + rehacer si hiciera falta)."}))

        Await viewModel.AbonarDevueltoSeleccionado()

        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Abonar", A(Of String).That.Contains("¿Confirma que el cliente no quiere el pedido 925137"))).
            MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.AbonarEnvioDevuelto(251300)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Abonar", A(Of String).That.Contains("Copiar factura"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Abonar_NoConfirmado_NoLlama() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(False)

        Await viewModel.AbonarDevueltoSeleccionado()

        A.CallTo(Function() servicio.AbonarEnvioDevuelto(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub Comandos_SoloConUnoSinDecidir()
        Assert.IsTrue(viewModel.ReenviarDevueltoCommand.CanExecute(Nothing))
        Assert.IsTrue(viewModel.AbonarDevueltoCommand.CanExecute(Nothing))

        viewModel.DevueltoSeleccionado = New EnvioDevueltoModel With {.Numero = 2, .Decision = EnvioDevueltoModel.REENVIAR, .EnvioNuevo = 300000}
        Assert.IsFalse(viewModel.ReenviarDevueltoCommand.CanExecute(Nothing), "Ya decidido")
        Assert.IsFalse(viewModel.AbonarDevueltoCommand.CanExecute(Nothing))

        viewModel.DevueltoSeleccionado = Nothing
        Assert.IsFalse(viewModel.ReenviarDevueltoCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Async Function YaDecidido_NoAbreNadaNiLlama() As Task
        viewModel.DevueltoSeleccionado = New EnvioDevueltoModel With {.Numero = 2, .Decision = EnvioDevueltoModel.ABONAR}

        Await viewModel.ReenviarDevueltoSeleccionado()
        Await viewModel.AbonarDevueltoSeleccionado()

        A.CallTo(Function() dialogService.ShowDialogAsync(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.AbonarEnvioDevuelto(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CargarDevueltos_ConservaLaSeleccion() As Task
        Dim otro As New EnvioDevueltoModel With {.Numero = 1, .Decision = EnvioDevueltoModel.SIN_DECIDIR}
        Dim mismo As New EnvioDevueltoModel With {.Numero = 251300, .Decision = EnvioDevueltoModel.ABONAR}
        A.CallTo(Function() servicio.CargarListaDevueltos("1  ")).Returns(Task.FromResult(New List(Of EnvioDevueltoModel) From {otro, mismo}))

        Await viewModel.CargarDevueltos()

        Assert.AreEqual(2, viewModel.listaDevueltos.Count)
        Assert.AreSame(mismo, viewModel.DevueltoSeleccionado)
    End Function

    <TestMethod()>
    Public Async Function CargarDevueltos_FallaLaApi_AvisaYDejaLaListaVacia() As Task
        A.CallTo(Function() servicio.CargarListaDevueltos(A(Of String).Ignored)).Throws(New Exception("Falta lanzar el script"))

        Await viewModel.CargarDevueltos()

        Assert.AreEqual(0, viewModel.listaDevueltos.Count)
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("Falta lanzar el script"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub TextoDecision_LoQueVeElAlmacen()
        Assert.AreEqual("Sin decidir", New EnvioDevueltoModel With {.Decision = "SinDecidir"}.TextoDecision)
        Assert.AreEqual("Abonar", New EnvioDevueltoModel With {.Decision = "Abonar"}.TextoDecision)
        Assert.AreEqual("REENVIAR: envío 300000 listo para imprimir la etiqueta",
                        New EnvioDevueltoModel With {.Decision = "Reenviar", .EnvioNuevo = 300000}.TextoDecision)
        Assert.AreEqual("Reenviado en el envío 300000 (con portes)",
                        New EnvioDevueltoModel With {.Decision = "Reenviar", .EnvioNuevo = 300000, .EnvioNuevoConEtiqueta = True, .PortesIncluidos = True}.TextoDecision)
    End Sub

    <TestMethod()>
    Public Sub Contrato_RutasYCuerpoComoLaApi()
        Assert.AreEqual("EnviosAgencias/Devueltos?empresa=1", AgenciaService.RutaDevueltos("1  "))
        Assert.AreEqual("EnviosAgencias/251300/Reenviar", AgenciaService.RutaReenviar(251300))
        Assert.AreEqual("EnviosAgencias/251300/Abonar", AgenciaService.RutaAbonarDevuelto(251300))
        Dim cuerpo = JObject.Parse(AgenciaService.CuerpoReenviar(New ReenvioEnvioPeticion With {.Direccion = "Calle Nueva 7", .IncluirPortes = True}))
        Assert.AreEqual("Calle Nueva 7", CStr(cuerpo("Direccion")), "Mismos nombres que ReenviarEnvioDTO en NestoAPI")
        Assert.IsTrue(CBool(cuerpo("IncluirPortes")))
    End Sub

    <TestMethod()>
    Public Sub MensajeErrorApi_ElTextoParaElUsuario()
        Assert.AreEqual("Ya decidido", AgenciaService.MensajeErrorApi("""Ya decidido"""), "409: Content(Conflict, texto)")
        Assert.AreEqual("Falta el script", AgenciaService.MensajeErrorApi("{""Message"":""Falta el script""}"), "400: BadRequest(texto)")
        Assert.AreEqual("<html>", AgenciaService.MensajeErrorApi("<html>"))
    End Sub
End Class
