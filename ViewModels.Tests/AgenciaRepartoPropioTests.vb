Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Services
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports System.Threading.Tasks

''' <summary>
''' Nesto#571 (NestoAPI#739): «Reparto propio» (agencia 14) como una agencia más, por la API como CTT/Innovatrans, y la
''' prueba de entrega del envío entregado (desde el 10/11/26, GET api/EnviosAgencias/{id}/PruebaEntrega). La API va con fakes.
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

    ' ---- Ver prueba de entrega en Agencias (desde el 10/11/26, de cualquier agencia por GET api/EnviosAgencias/{id}/PruebaEntrega) ----

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

    Private Shared Function Prueba(firma As Byte()) As PruebaEntregaEnvio
        Return New PruebaEntregaEnvio With {.Numero = 251500, .Agencia = 14, .NombreAgencia = "Reparto propio", .Pedido = 930001, .Disponible = True,
            .FechaHora = New Date(2026, 10, 29, 12, 34, 0), .Receptor = "Ana López", .DniReceptor = "12345678Z", .CobroReembolso = "Tarjeta",
            .ImporteCobrado = 80D, .Usuario = "Pedro", .Documento = firma, .TipoDocumento = If(firma Is Nothing, Nothing, "image/png"),
            .Mensaje = If(firma Is Nothing, "Sin firma (se entregó con DNI).", Nothing)}
    End Function

    <TestMethod()>
    Public Sub VerPruebaEntrega_EnLosEntregadosDeCualquierAgencia()
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        Assert.IsTrue(viewModel.VerPruebaEntregaCommand.CanExecute(Nothing))

        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_TRAMITADO_ENVIO))
        Assert.IsFalse(viewModel.VerPruebaEntregaCommand.CanExecute(Nothing), "Aún no está entregado")

        viewModel.envioActual = Envio(13, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        Assert.IsTrue(viewModel.VerPruebaEntregaCommand.CanExecute(Nothing), "CTT también: la API la pide a la agencia")

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
    Public Async Function VerPruebaEntrega_NoDisponible_EnsenaElMotivoDeLaApi() As Task
        viewModel.envioActual = Envio(1, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        A.CallTo(Function() servicio.LeerPruebaEntrega(251500)).Returns(Task.FromResult(
            New PruebaEntregaEnvio With {.Numero = 251500, .Disponible = False, .Mensaje = "Esta agencia no da prueba de entrega."}))

        Await viewModel.VerPruebaEntregaSeleccionada()

        A.CallTo(Sub() dialogService.ShowNotification("Prueba de entrega", "Esta agencia no da prueba de entrega.")).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function VerPruebaEntrega_ConFirma_AbreElDialogoConTodo() As Task
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        Dim png As Byte() = {137, 80, 78, 71}
        A.CallTo(Function() servicio.LeerPruebaEntrega(251500)).Returns(Task.FromResult(Prueba(png)))
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
        Assert.IsFalse(viewModel.EstaOcupado, "No se queda ocupado")
    End Function

    <TestMethod()>
    Public Async Function VerPruebaEntrega_SinFirma_AbreElDialogoConElAviso() As Task
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        A.CallTo(Function() servicio.LeerPruebaEntrega(251500)).Returns(Task.FromResult(Prueba(Nothing)))
        Dim parametros As ParametrosDialogo = Nothing
        A.CallTo(Sub() dialogService.ShowDialog(PruebaEntregaDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
            .Invokes(Sub(nombre As String, p As ParametrosDialogo, cb As Action(Of ResultadoDialogo)) parametros = p)

        Await viewModel.VerPruebaEntregaSeleccionada()

        Assert.IsNull(parametros.GetValue(Of Byte())(PruebaEntregaDialogViewModel.PARAMETRO_FIRMA))
        Assert.AreEqual("Sin firma (se entregó con DNI).", parametros.GetValue(Of String)(PruebaEntregaDialogViewModel.PARAMETRO_AVISO))
    End Function

    <TestMethod()>
    Public Async Function VerPruebaEntrega_FallaLaApi_SeEnsenaElMotivo() As Task
        viewModel.envioActual = Envio(14, CShort(Constantes.Agencias.ESTADO_ENTREGADO_ENVIO))
        A.CallTo(Function() servicio.LeerPruebaEntrega(251500)).Throws(New Exception("Su usuario no tiene permiso para ver la prueba de entrega"))

        Await viewModel.VerPruebaEntregaSeleccionada()

        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("no tiene permiso"))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(viewModel.EstaOcupado)
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

    ' ---- NestoAPI#841: día sin reparto propio ----

    Private Async Function HoySinFurgoneta(sinReparto As Boolean) As Task
        Dim festivos = A.Fake(Of IServicioFestivos)
        A.CallTo(Function() festivos.EsDiaSinRepartoPropio(A(Of Date).Ignored)).Returns(Task.FromResult(sinReparto))
        viewModel.ServicioFestivos = festivos
        Await viewModel.ComprobarRepartoPropioAsync()
    End Function

    Private Shared Function Agencia(numero As Integer, nombre As String) As AgenciasTransporte
        Return New AgenciasTransporte With {.Empresa = "1  ", .Numero = numero, .Nombre = nombre}
    End Function

    <TestMethod()>
    Public Async Function DiaSinRepartoPropio_ConElRepartoPropioElegido_Avisa() As Task
        Await HoySinFurgoneta(True)

        viewModel.agenciaSeleccionada = Agencia(14, "Reparto propio")

        Assert.IsTrue(viewModel.HayAvisoRepartoPropio)
        Assert.AreEqual("Hoy no hay reparto propio: las entregas de nuestra ruta se mueven al siguiente día de reparto.", viewModel.AvisoRepartoPropio)
    End Function

    <TestMethod()>
    Public Async Function DiaSinRepartoPropio_ConOtraAgencia_NoAvisa() As Task
        Await HoySinFurgoneta(True)

        viewModel.agenciaSeleccionada = Agencia(13, "CTT")

        Assert.IsFalse(viewModel.HayAvisoRepartoPropio)
    End Function

    <TestMethod()>
    Public Async Function DiaNormal_ConElRepartoPropio_NoAvisa() As Task
        Await HoySinFurgoneta(False)

        viewModel.agenciaSeleccionada = Agencia(14, "Reparto propio")

        Assert.IsFalse(viewModel.HayAvisoRepartoPropio)
    End Function

    <TestMethod()>
    Public Async Function DiaSinRepartoPropio_SiLaApiFalla_NoAvisa() As Task
        Dim festivos = A.Fake(Of IServicioFestivos)
        A.CallTo(Function() festivos.EsDiaSinRepartoPropio(A(Of Date).Ignored)).Throws(New Exception("caída"))
        viewModel.ServicioFestivos = festivos
        viewModel.agenciaSeleccionada = Agencia(14, "Reparto propio")

        Await viewModel.ComprobarRepartoPropioAsync()

        Assert.IsFalse(viewModel.HayAvisoRepartoPropio)
    End Function
End Class
