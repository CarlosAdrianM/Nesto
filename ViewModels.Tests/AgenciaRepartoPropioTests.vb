Imports ControlesUsuario.Dialogs
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Threading.Tasks

''' <summary>
''' Nesto#571 (NestoAPI#739): «Reparto propio» (agencia 14) como una agencia más, por la API como CTT/Innovatrans, y la
''' prueba de entrega del envío entregado (GET api/RepartoPropio/Envios/{id}/PruebaEntrega y …/Firma). La API va con fakes.
''' </summary>
<TestClass()>
Public Class AgenciaRepartoPropioTests

    ' ---- La agencia ----

    <TestMethod()>
    Public Sub RepartoPropio_IdentidadYPlaza()
        Dim agencia As New AgenciaRepartoPropio()
        Assert.AreEqual("Reparto propio", agencia.NombreAgencia, "Clave del factory = AgenciasTransporte.Nombre")
        Assert.AreEqual(14, agencia.AgenciaId)
        Assert.AreEqual(14, Constantes.Agencias.AGENCIA_REPARTO_PROPIO)
        Assert.IsTrue(agencia.ListaServicios.All(Function(s) s.AgenciaId = 14))

        Dim nemonico As String = Nothing, nombrePlaza As String = Nothing, telefono As String = Nothing, email As String = Nothing
        agencia.calcularPlaza("28110", 34, nemonico, nombrePlaza, telefono, email)
        Assert.AreEqual("RP", nemonico)
        Assert.AreEqual("Reparto propio", nombrePlaza)
    End Sub

    <TestMethod()>
    Public Sub RepartoPropio_RegistrarAlImprimirPorLaApi()
        Dim agencia As New AgenciaRepartoPropio()
        Assert.AreEqual(TipoFlujoTramitacion.RegistrarAlImprimir, agencia.FlujoTramitacion)
        Assert.IsInstanceOfType(agencia, GetType(IAgenciaConGestionRemota))
        Assert.IsFalse(agencia.PermiteEditarCodigoBarras)
        Assert.IsFalse(agencia.DimensionesBultosObligatorias)
    End Sub

    <TestMethod()>
    Public Sub RepartoPropio_ImprimeEnLaMismaZebraQueInnovatrans()
        Assert.AreEqual(New AgenciaInnovatrans().ClaveImpresora, New AgenciaRepartoPropio().ClaveImpresora)
    End Sub

    <TestMethod()>
    Public Sub RepartoPropio_UnServicioSinRetornoYSoloEspana()
        Dim agencia As New AgenciaRepartoPropio()
        Assert.AreEqual(1, agencia.ListaServicios.Count)
        Assert.AreEqual(agencia.ServicioDefecto, agencia.ListaServicios.Single().ServicioId)
        Assert.AreEqual(1, agencia.ListaTiposRetorno.Count)
        Assert.AreEqual(34, agencia.ListaPaises.Single().Id)
    End Sub

    <TestMethod()>
    Public Sub RepartoPropio_SinEnlaceDeSeguimientoNiNumeroCliente()
        Dim agencia As IAgencia = New AgenciaRepartoPropio()
        Dim envio As New EnviosAgencia With {.Numero = 251500, .Agencia = 14, .CodigoBarras = "RP251500"}
        Assert.AreEqual(String.Empty, AgenciasViewModel.CalcularEnlaceSeguimiento(envio, agencia))
        Assert.AreEqual(String.Empty, agencia.NumeroCliente)
    End Sub

    ' ---- Contrato con la API ----

    <TestMethod()>
    Public Sub PruebaEntrega_RutasDelContrato()
        Assert.AreEqual("RepartoPropio/Envios/251500/PruebaEntrega", AgenciaService.RutaPruebaEntregaReparto(251500))
        Assert.AreEqual("RepartoPropio/Envios/251500/PruebaEntrega/Firma", AgenciaService.RutaFirmaPruebaEntregaReparto(251500))
    End Sub

    <TestMethod()>
    Public Sub PruebaEntrega_MismosNombresQueElServidor()
        CollectionAssert.AreEqual({"CobroReembolso", "DniReceptor", "EnvioId", "FechaHora", "ImporteCobrado", "Latitud", "Longitud",
                                   "Receptor", "TieneFirma", "Usuario"},
                                  GetType(PruebaEntregaRepartoDto).GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n, StringComparer.Ordinal).ToArray())
    End Sub

    <TestMethod()>
    Public Sub PruebaEntrega_SeLeeElJson_Y204EsNothing()
        Dim cuerpo As String = "{""EnvioId"":251500,""FechaHora"":""2026-10-29T12:34:00"",""Receptor"":""Ana López"",""DniReceptor"":""12345678Z""," &
            """CobroReembolso"":""Efectivo"",""ImporteCobrado"":123.45,""Usuario"":""NUEVAVISION\\Pedro"",""TieneFirma"":true,""Latitud"":40.6,""Longitud"":-3.5}"
        Dim prueba = AgenciaService.PruebaEntregaDeRespuesta(Net.HttpStatusCode.OK, cuerpo)
        Assert.AreEqual(251500, prueba.EnvioId)
        Assert.AreEqual(New Date(2026, 10, 29, 12, 34, 0), prueba.FechaHora)
        Assert.AreEqual("Ana López", prueba.Receptor)
        Assert.AreEqual("12345678Z", prueba.DniReceptor)
        Assert.AreEqual("Efectivo", prueba.CobroReembolso)
        Assert.AreEqual(123.45D, prueba.ImporteCobrado)
        Assert.AreEqual("NUEVAVISION\Pedro", prueba.Usuario)
        Assert.IsTrue(prueba.TieneFirma)

        Assert.IsNull(AgenciaService.PruebaEntregaDeRespuesta(Net.HttpStatusCode.NoContent, String.Empty))
    End Sub

    ' ---- Ver prueba de entrega en Agencias ----

    Private servicio As IAgenciaService
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        viewModel = New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogService, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
    End Sub

    Private Shared Function Envio(agencia As Integer, estado As Short) As EnviosAgencia
        Return New EnviosAgencia With {.Numero = 251500, .Pedido = 930001, .Agencia = agencia, .Estado = estado, .CodigoBarras = "RP251500"}
    End Function

    Private Shared Function Prueba(tieneFirma As Boolean) As PruebaEntregaRepartoDto
        Return New PruebaEntregaRepartoDto With {.EnvioId = 251500, .FechaHora = New Date(2026, 10, 29, 12, 34, 0), .Receptor = "Ana López",
            .DniReceptor = "12345678Z", .CobroReembolso = "Tarjeta", .ImporteCobrado = 80D, .Usuario = "Pedro", .TieneFirma = tieneFirma}
    End Function

    <TestMethod()>
    Public Sub VerPruebaEntrega_SoloEnEntregadosDelRepartoPropio()
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        Assert.IsTrue(viewModel.VerPruebaEntregaCommand.CanExecute(Nothing))

        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_TRAMITADO_ENVIO))
        Assert.IsFalse(viewModel.VerPruebaEntregaCommand.CanExecute(Nothing), "Aún no está entregado")

        viewModel.envioActual = Envio(13, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        Assert.IsFalse(viewModel.VerPruebaEntregaCommand.CanExecute(Nothing), "CTT no tiene prueba de entrega nuestra")

        viewModel.envioActual = Nothing
        Assert.IsFalse(viewModel.VerPruebaEntregaCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub ActualizarEstado_NoSeOfreceEnElRepartoPropio()
        ' Su estado lo pone Ariadna (entrega o incidencia): no hay agencia a la que preguntar.
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_INCIDENTADO_ENVIO))
        Assert.IsFalse(viewModel.cmdActualizarSeguimiento.CanExecute(Nothing))
        Assert.IsTrue(viewModel.DarPorCerradoCommand.CanExecute(Nothing), "Dar por cerrado vale como en las demás agencias")

        viewModel.envioActual = Envio(13, CShort(Constantes.Agencias.ESTADO_INCIDENTADO_ENVIO))
        Assert.IsTrue(viewModel.cmdActualizarSeguimiento.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Async Function VerPruebaEntrega_Sin_AvisaDeUsted() As Task
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        A.CallTo(Function() servicio.LeerPruebaEntregaReparto(251500)).Returns(Task.FromResult(Of PruebaEntregaRepartoDto)(Nothing))

        Await viewModel.VerPruebaEntregaSeleccionada()

        A.CallTo(Sub() dialogService.ShowNotification("Prueba de entrega", "Este envío no tiene prueba de entrega.")).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.LeerFirmaPruebaEntregaReparto(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function VerPruebaEntrega_ConFirma_AbreElDialogoConTodo() As Task
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        Dim png As Byte() = {137, 80, 78, 71}
        A.CallTo(Function() servicio.LeerPruebaEntregaReparto(251500)).Returns(Task.FromResult(Prueba(tieneFirma:=True)))
        A.CallTo(Function() servicio.LeerFirmaPruebaEntregaReparto(251500)).Returns(Task.FromResult(png))
        Dim parametros As ParametrosDialogo = Nothing
        A.CallTo(Sub() dialogService.ShowDialog(PruebaEntregaDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
            .Invokes(Sub(nombre As String, p As ParametrosDialogo, cb As Action(Of ResultadoDialogo)) parametros = p)

        Await viewModel.VerPruebaEntregaSeleccionada()

        Assert.IsNotNull(parametros, "Se abre el diálogo de la prueba de entrega")
        Assert.AreEqual(251500, parametros.GetValue(Of Integer)(PruebaEntregaDialogViewModel.PARAMETRO_ENVIO))
        Assert.AreEqual("930001", parametros.GetValue(Of String)(PruebaEntregaDialogViewModel.PARAMETRO_PEDIDO))
        Assert.AreEqual("Ana López", parametros.GetValue(Of String)(PruebaEntregaDialogViewModel.PARAMETRO_RECEPTOR))
        Assert.AreEqual("12345678Z", parametros.GetValue(Of String)(PruebaEntregaDialogViewModel.PARAMETRO_DNI))
        Assert.AreEqual("Tarjeta", parametros.GetValue(Of String)(PruebaEntregaDialogViewModel.PARAMETRO_COBRO_REEMBOLSO))
        Assert.AreEqual(80D, parametros.GetValue(Of Decimal?)(PruebaEntregaDialogViewModel.PARAMETRO_IMPORTE_COBRADO))
        Assert.AreEqual("Pedro", parametros.GetValue(Of String)(PruebaEntregaDialogViewModel.PARAMETRO_USUARIO))
        Assert.AreSame(png, parametros.GetValue(Of Byte())(PruebaEntregaDialogViewModel.PARAMETRO_FIRMA))
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function VerPruebaEntrega_SinFirma_NoLaPide() As Task
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        A.CallTo(Function() servicio.LeerPruebaEntregaReparto(251500)).Returns(Task.FromResult(Prueba(tieneFirma:=False)))

        Await viewModel.VerPruebaEntregaSeleccionada()

        A.CallTo(Function() servicio.LeerFirmaPruebaEntregaReparto(A(Of Integer).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowDialog(PruebaEntregaDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function VerPruebaEntrega_FallaLaFirma_SeEnsenaLoDemas() As Task
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        A.CallTo(Function() servicio.LeerPruebaEntregaReparto(251500)).Returns(Task.FromResult(Prueba(tieneFirma:=True)))
        A.CallTo(Function() servicio.LeerFirmaPruebaEntregaReparto(251500)).Throws(New Exception("500"))

        Await viewModel.VerPruebaEntregaSeleccionada()

        A.CallTo(Sub() dialogService.ShowDialog(PruebaEntregaDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function VerPruebaEntrega_FallaLaApi_SeEnsenaElMotivo() As Task
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        A.CallTo(Function() servicio.LeerPruebaEntregaReparto(251500)).Throws(New Exception("NestoAPI no ha devuelto la prueba de entrega (403): sin permiso"))

        Await viewModel.VerPruebaEntregaSeleccionada()

        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("sin permiso"))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
    End Function

    ' ---- Nesto#573 (NestoAPI#748): «Abrir seguimiento» con el enlace que firma la API ----

    Private Const URL_SEGUIMIENTO As String = "https://api.nuevavision.es/seguimiento/AAPWbBzS0rOv1Sx4cQ3Rd0F2"

    <TestMethod()>
    Public Async Function AbrirSeguimiento_RepartoPropio_PideElEnlaceALaApiYActivaElBoton() As Task
        A.CallTo(Function() servicio.LeerEnlaceSeguimiento(251500)).Returns(Task.FromResult(URL_SEGUIMIENTO))

        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_TRAMITADO_ENVIO))
        Await viewModel.CargaEnlaceSeguimiento

        Assert.AreEqual(URL_SEGUIMIENTO, viewModel.EnlaceSeguimientoEnvio)
        Assert.IsTrue(viewModel.AbrirEnlaceSeguimientoCommand.CanExecute(Nothing))
    End Function

    <TestMethod()>
    Public Async Function AbrirSeguimiento_RepartoPropio_SiLaApiFallaElBotonSigueDesactivado() As Task
        ' API caída o aún sin publicar el endpoint: no rompe nada ni enseña errores.
        A.CallTo(Function() servicio.LeerEnlaceSeguimiento(251500)).ThrowsAsync(New Exception("NestoAPI no ha devuelto el enlace (500)"))

        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_TRAMITADO_ENVIO))
        Await viewModel.CargaEnlaceSeguimiento

        Assert.AreEqual(String.Empty, viewModel.EnlaceSeguimientoEnvio)
        Assert.IsFalse(viewModel.AbrirEnlaceSeguimientoCommand.CanExecute(Nothing))
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function AbrirSeguimiento_RepartoPropio_SinEnlaceEnLaApi_BotonDesactivado() As Task
        A.CallTo(Function() servicio.LeerEnlaceSeguimiento(251500)).Returns(Task.FromResult(Of String)(Nothing))

        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_TRAMITADO_ENVIO))
        Await viewModel.CargaEnlaceSeguimiento

        Assert.IsFalse(viewModel.AbrirEnlaceSeguimientoCommand.CanExecute(Nothing))
    End Function

    <TestMethod()>
    Public Async Function AbrirSeguimiento_RespuestaTardiaDeOtroEnvio_NoPisaElEnlace() As Task
        Dim lenta As New TaskCompletionSource(Of String)()
        A.CallTo(Function() servicio.LeerEnlaceSeguimiento(251500)).Returns(lenta.Task)

        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_TRAMITADO_ENVIO))
        Dim primera As Task = viewModel.CargaEnlaceSeguimiento
        ' El usuario cambia a un envío de CTT antes de que conteste la API
        viewModel.envioActual = New EnviosAgencia With {.Numero = 251501, .Agencia = 13, .CodigoBarras = Nothing}
        lenta.SetResult(URL_SEGUIMIENTO)
        Await primera

        Assert.AreNotEqual(URL_SEGUIMIENTO, viewModel.EnlaceSeguimientoEnvio)
    End Function

    <TestMethod()>
    Public Sub AbrirSeguimiento_OtraAgencia_NoLlamaALaApi()
        viewModel.envioActual = Envio(13, CShort(Constantes.Agencias.ESTADO_TRAMITADO_ENVIO))
        A.CallTo(Function() servicio.LeerEnlaceSeguimiento(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Async Function AbrirSeguimiento_PestanaRetrasados_RepartoPropio_ActivaElBoton() As Task
        A.CallTo(Function() servicio.LeerEnlaceSeguimiento(251500)).Returns(Task.FromResult(URL_SEGUIMIENTO))

        viewModel.envioRetrasadoActual = New EnvioRetrasadoModel With {.Numero = 251500, .Empresa = "1", .Agencia = 14, .CodigoBarras = "RP251500"}
        Await viewModel.CargaEnlaceSeguimiento

        Assert.AreEqual(URL_SEGUIMIENTO, viewModel.EnlaceSeguimientoEnvio)
        Assert.IsTrue(viewModel.AbrirEnlaceSeguimientoCommand.CanExecute(Nothing))
    End Function

    <TestMethod()>
    Public Sub EnlaceSeguimiento_ContratoConLaApi()
        Assert.AreEqual("EnviosAgencias/251500/EnlaceSeguimiento", AgenciaService.RutaEnlaceSeguimiento(251500))
        CollectionAssert.AreEqual({"Numero", "Url"},
                                  GetType(EnlaceSeguimientoEnvioDto).GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n, StringComparer.Ordinal).ToArray())
        Assert.AreEqual(URL_SEGUIMIENTO, AgenciaService.EnlaceSeguimientoDeRespuesta("{""Numero"":251500,""Url"":""" & URL_SEGUIMIENTO & """}"))
        Assert.IsNull(AgenciaService.EnlaceSeguimientoDeRespuesta("{""Numero"":251500,""Url"":null}"))
        Assert.IsNull(AgenciaService.EnlaceSeguimientoDeRespuesta(String.Empty))
    End Sub
End Class
