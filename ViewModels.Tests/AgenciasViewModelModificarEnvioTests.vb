Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports Newtonsoft.Json
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' Nesto#340 (Agencias, slice A4.4): «Modificar» y «Rehusar» de
''' la pestaña Tramitados ya no abren un NestoEntities: el servidor guarda, contabiliza y rehúsa, y
''' aquí se refleja. Incluye el contrato del JSON con el servidor (ModificacionEnvioServiceTests).
''' </summary>
<TestClass()>
Public Class AgenciasViewModelModificarEnvioTests
    Private servicio As IAgenciaService
    Private dialogService As IDialogService
    Private viewModel As AgenciasViewModel
    Private envio As EnviosAgencia

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IDialogService)
        viewModel = New AgenciasViewModel(A.Fake(Of RegionManager), servicio, A.Fake(Of IConfiguracion), dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.listaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(1, "Sin retorno"), New tipoIdDescripcion(3, "Retorno obligatorio")}
        viewModel.observacionesModificacion = "llamó el cliente"
        envio = New EnviosAgencia With {.Numero = 247975, .Reembolso = 121.5D, .Retorno = 1, .Estado = 1, .FechaEntrega = New Date(2026, 9, 16)}
    End Sub

    Private Sub ElUsuarioContesta(ok As Boolean)
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)) _
         .Invokes(Sub(nombre As String, parametros As IDialogParameters, callback As Action(Of IDialogResult))
                      If callback Is Nothing Then
                          Return
                      End If
                      Dim resultado = A.Fake(Of IDialogResult)
                      A.CallTo(Function() resultado.Result).Returns(If(ok, ButtonResult.OK, ButtonResult.Cancel))
                      callback(resultado)
                  End Sub)
    End Sub

    <TestMethod()>
    Public Sub MensajeConfirmarModificarEnvio_CambiaElReembolso_AvisaDeQueNoSeInformaALaAgencia()
        ' NestoAPI#512: en un envío tramitado el cambio de reembolso solo afecta a Nesto.
        Dim mensaje = AgenciasViewModel.MensajeConfirmarModificarEnvio("15191 ", "CALLE MAYOR 1", 100D, 0D)

        StringAssert.Contains(mensaje, "15191")
        StringAssert.Contains(mensaje, "NO se avisa a la agencia")
    End Sub

    <TestMethod()>
    Public Sub MensajeConfirmarModificarEnvio_MismoReembolso_SinAviso()
        Dim mensaje = AgenciasViewModel.MensajeConfirmarModificarEnvio("15191", "CALLE MAYOR 1", 100D, 100D)

        Assert.IsFalse(mensaje.Contains("NO se avisa a la agencia"))
    End Sub

    <TestMethod()>
    Public Async Function ModificarEnvioPorApi_MandaLosDatosYReflejaElResultado() As Task
        Dim enviado As ModificarDatosEnvioDto = Nothing
        A.CallTo(Function() servicio.ModificarDatosEnvio(247975, A(Of ModificarDatosEnvioDto).Ignored)) _
            .Invokes(Sub(n As Integer, d As ModificarDatosEnvioDto) enviado = d) _
            .Returns(Task.FromResult(New ResultadoModificacionEnvioDto With {.Numero = 247975, .Mensaje = "Envío 247975 modificado (Reembolso)."}))

        Await viewModel.ModificarEnvioPorApi(envio, 80, New tipoIdDescripcion(3, "Retorno obligatorio"), 2, False, New Date(2026, 9, 21))

        Assert.IsNotNull(enviado)
        Assert.AreEqual(80D, enviado.Reembolso)
        Assert.AreEqual(CShort(3), enviado.Retorno)
        Assert.AreEqual("Sin retorno", enviado.RetornoAnteriorDescripcion, "la descripción del retorno ANTERIOR, que solo la sabe el cliente")
        Assert.AreEqual(CShort(2), enviado.Estado)
        Assert.AreEqual(New Date(2026, 9, 21), enviado.FechaEntrega.Value)
        Assert.IsFalse(enviado.Rehusar)
        Assert.AreEqual("llamó el cliente", enviado.Observaciones)

        Assert.AreEqual(80D, envio.Reembolso)
        Assert.AreEqual(CByte(3), envio.Retorno)
        Assert.AreEqual(CShort(2), envio.Estado)
        Assert.AreEqual(New Date(2026, 9, 21), envio.FechaEntrega.Value)
        Assert.AreEqual("Envío 247975 modificado (Reembolso).", viewModel.mensajeError)
    End Function

    <TestMethod()>
    Public Async Function ModificarEnvioPorApi_YaCobrado_NoLlamaAlServidor() As Task
        envio.FechaPagoReembolso = New Date(2026, 9, 17)

        Await viewModel.ModificarEnvioPorApi(envio, 80, New tipoIdDescripcion(1, "Sin retorno"), 1, False, envio.FechaEntrega)

        A.CallTo(Function() servicio.ModificarDatosEnvio(A(Of Integer).Ignored, A(Of ModificarDatosEnvioDto).Ignored)).MustNotHaveHappened()
        Assert.AreEqual(121.5D, envio.Reembolso)
    End Function

    <TestMethod()>
    Public Async Function ModificarEnvioPorApi_ImporteDesproporcionadoYElUsuarioCancela_NoLlamaAlServidor() As Task
        ElUsuarioContesta(False)

        Await viewModel.ModificarEnvioPorApi(envio, 5000, New tipoIdDescripcion(1, "Sin retorno"), 1, False, envio.FechaEntrega)

        A.CallTo(Function() servicio.ModificarDatosEnvio(A(Of Integer).Ignored, A(Of ModificarDatosEnvioDto).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ModificarEnvioPorApi_ElServidorRechaza_NoTocaElEnvio() As Task
        A.CallTo(Function() servicio.ModificarDatosEnvio(A(Of Integer).Ignored, A(Of ModificarDatosEnvioDto).Ignored)) _
            .Throws(New Exception("NestoAPI rechazó la modificación del envío (400): No se puede modificar este envío, porque ya está cobrado"))

        Await viewModel.ModificarEnvioPorApi(envio, 0, New tipoIdDescripcion(3, "Retorno obligatorio"), 1, True, envio.FechaEntrega)

        Assert.AreEqual(121.5D, envio.Reembolso)
        Assert.AreEqual(CByte(1), envio.Retorno)
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub ContratoConElServidor_RutaYNombresDelJson()
        Assert.AreEqual("EnviosAgencias/247975/ModificarDatos", AgenciaService.RutaModificarDatosEnvio(247975))
        Dim peticion = GetType(ModificarDatosEnvioDto).GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n).ToArray()
        CollectionAssert.AreEqual({"Estado", "FechaEntrega", "Observaciones", "Reembolso", "Rehusar", "Retorno", "RetornoAnteriorDescripcion"}, peticion)
        Dim respuesta = GetType(ResultadoModificacionEnvioDto).GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n).ToArray()
        CollectionAssert.AreEqual({"Asiento", "CamposModificados", "Mensaje", "Numero", "Rehusado"}, respuesta)
        Dim dto = JsonConvert.DeserializeObject(Of ResultadoModificacionEnvioDto)("{""Numero"":1,""CamposModificados"":[""Reembolso""],""Asiento"":88140,""Rehusado"":true,""Mensaje"":""ok""}")
        Assert.AreEqual(88140, dto.Asiento)
        Assert.IsTrue(dto.Rehusado)
        Assert.AreEqual("Reembolso", dto.CamposModificados.Single())
    End Sub

End Class
