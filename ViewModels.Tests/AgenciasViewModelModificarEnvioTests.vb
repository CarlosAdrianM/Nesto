Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports Newtonsoft.Json
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
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel
    Private envio As EnviosAgencia

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        viewModel = New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        viewModel.listaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(1, "Sin retorno"), New tipoIdDescripcion(3, "Retorno obligatorio")}
        viewModel.observacionesModificacion = "llamó el cliente"
        envio = New EnviosAgencia With {.Numero = 247975, .Reembolso = 121.5D, .Retorno = 1, .Estado = 1, .FechaEntrega = New Date(2026, 9, 16)}
    End Sub

    Private Sub ElUsuarioContesta(ok As Boolean)
        A.CallTo(Sub() dialogService.ShowConfirmation(A(Of String).Ignored, A(Of String).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
         .Invokes(Sub(titulo As String, mensaje As String, callback As Action(Of ResultadoDialogo))
                      callback?.Invoke(New ResultadoDialogo(If(ok, ResultadoBoton.OK, ResultadoBoton.Cancel)))
                  End Sub)
    End Sub

    <TestMethod()>
    Public Sub MensajeConfirmarModificarEnvio_CambiaElReembolso_AvisoNeutroSinAfirmarQueSoloSeCambiaEnNesto()
        ' NestoAPI#597: lo que pasa con la agencia lo decide el servidor; antes de guardar el aviso es neutro.
        Dim mensaje = AgenciasViewModel.MensajeConfirmarModificarEnvio("15191 ", "CALLE MAYOR 1", 100D, 0D)

        StringAssert.Contains(mensaje, "15191")
        StringAssert.Contains(mensaje, "Según la agencia, el cambio se reenviará, viajará en el cierre o habrá que pedírselo")
        Assert.IsFalse(mensaje.Contains("solo se cambia en Nesto"), "ya no es verdad para todas las agencias")
        Assert.IsFalse(mensaje.Contains("NO se avisa a la agencia"))
    End Sub

    <TestMethod()>
    Public Sub MensajeConfirmarModificarEnvio_CambiaElRetorno_TambienAvisa()
        Dim mensaje = AgenciasViewModel.MensajeConfirmarModificarEnvio("15191", "CALLE MAYOR 1", 100D, 100D, 0, 1)

        StringAssert.Contains(mensaje, AgenciasViewModel.AVISO_PREVIO_CAMBIO_AGENCIA)
    End Sub

    <TestMethod()>
    Public Sub MensajeConfirmarModificarEnvio_MismoReembolsoYRetorno_SinAviso()
        Dim mensaje = AgenciasViewModel.MensajeConfirmarModificarEnvio("15191", "CALLE MAYOR 1", 100D, 100D, 1, 1)

        Assert.IsFalse(mensaje.Contains(AgenciasViewModel.AVISO_PREVIO_CAMBIO_AGENCIA))
    End Sub

    <TestMethod()>
    Public Async Function ModificarEnvioPorApi_ReenviadoConEtiquetaNueva_LaImprimeEnseñaElAvisoYGuardaElAlbaran() As Task
        Dim impresa As ResultadoModificacionEnvioDto = Nothing
        viewModel.ImprimirEtiquetaNueva = Function(e, r)
                                              impresa = r
                                              Return Task.CompletedTask
                                          End Function
        Dim aviso = "Reenviado a CTT con albarán nuevo 0082800081239999 (el 0082800081234567 queda anulado): pega la etiqueta nueva en el paquete"
        A.CallTo(Function() servicio.ModificarDatosEnvio(247975, A(Of ModificarDatosEnvioDto).Ignored)) _
            .Returns(Task.FromResult(New ResultadoModificacionEnvioDto With {
                .Numero = 247975, .Mensaje = "Envío 247975 modificado (Retorno, CodigoBarras).", .Aviso = aviso,
                .ReenviadoAAgencia = True, .Albaran = "0082800081239999", .EtiquetaCodificacion = "base64", .EtiquetaContenido = "XlhBfkNJMTUw"}))

        Await viewModel.ModificarEnvioPorApi(envio, 121.5, New tipoIdDescripcion(3, "Retorno obligatorio"), 1, False, envio.FechaEntrega)

        Assert.IsNotNull(impresa, "con etiqueta nueva se manda a la Zebra")
        Assert.AreEqual("XlhBfkNJMTUw", impresa.EtiquetaContenido)
        Assert.AreEqual("0082800081239999", envio.CodigoBarras)
        A.CallTo(Sub() dialogService.ShowNotification("Modificar Envío", aviso)).MustHaveHappenedOnceExactly()
        StringAssert.Contains(viewModel.mensajeError, aviso)
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ModificarEnvioPorApi_SinEtiqueta_NoImprimeYEnseñaElAviso() As Task
        Dim impresiones As Integer = 0
        viewModel.ImprimirEtiquetaNueva = Function(e, r)
                                              impresiones += 1
                                              Return Task.CompletedTask
                                          End Function
        A.CallTo(Function() servicio.ModificarDatosEnvio(247975, A(Of ModificarDatosEnvioDto).Ignored)) _
            .Returns(Task.FromResult(New ResultadoModificacionEnvioDto With {
                .Numero = 247975, .Mensaje = "Envío 247975 modificado (Retorno).", .Aviso = "El cambio viajará a GLS en el cierre del día"}))

        Await viewModel.ModificarEnvioPorApi(envio, 121.5, New tipoIdDescripcion(3, "Retorno obligatorio"), 1, False, envio.FechaEntrega)

        Assert.AreEqual(0, impresiones)
        A.CallTo(Sub() dialogService.ShowNotification("Modificar Envío", "El cambio viajará a GLS en el cierre del día")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function ModificarEnvioPorApi_FallaLaImpresion_AvisaDeReimprimirSinDecirQueNoSeGrabo() As Task
        viewModel.ImprimirEtiquetaNueva = Function(e, r) Task.FromException(New Exception("impresora apagada"))
        A.CallTo(Function() servicio.ModificarDatosEnvio(247975, A(Of ModificarDatosEnvioDto).Ignored)) _
            .Returns(Task.FromResult(New ResultadoModificacionEnvioDto With {
                .Numero = 247975, .Mensaje = "ok", .Aviso = "Reenviado a CTT", .Albaran = "0082800081239999", .EtiquetaContenido = "XlhB"}))
        Dim textoError As String = Nothing
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).Invokes(Sub(t As String) textoError = t)

        Await viewModel.ModificarEnvioPorApi(envio, 121.5, New tipoIdDescripcion(3, "Retorno obligatorio"), 1, False, envio.FechaEntrega)

        StringAssert.Contains(textoError, "reimprímela")
        Assert.IsFalse(textoError.Contains("no se han grabado"))
        Assert.AreEqual(CByte(3), envio.Retorno, "el servidor ya lo guardó")
    End Function

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
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowConfirmation(A(Of String).Ignored, A(Of String).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub ContratoConElServidor_RutaYNombresDelJson()
        Assert.AreEqual("EnviosAgencias/247975/ModificarDatos", AgenciaService.RutaModificarDatosEnvio(247975))
        Dim peticion = GetType(ModificarDatosEnvioDto).GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n).ToArray()
        CollectionAssert.AreEqual({"Estado", "FechaEntrega", "Observaciones", "Reembolso", "Rehusar", "Retorno", "RetornoAnteriorDescripcion"}, peticion)
        Dim respuesta = GetType(ResultadoModificacionEnvioDto).GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n).ToArray()
        CollectionAssert.AreEqual({"Albaran", "Asiento", "Aviso", "Bultos", "CamposModificados", "EtiquetaCodificacion", "EtiquetaContenido",
                                   "EtiquetaTipo", "Mensaje", "Numero", "ReenviadoAAgencia", "Rehusado", "Reimpresion"}, respuesta)
        Dim dto = JsonConvert.DeserializeObject(Of ResultadoModificacionEnvioDto)("{""Numero"":1,""CamposModificados"":[""Reembolso""],""Asiento"":88140,""Rehusado"":true,""Mensaje"":""ok""}")
        Assert.AreEqual(88140, dto.Asiento)
        Assert.IsTrue(dto.Rehusado)
        Assert.AreEqual("Reembolso", dto.CamposModificados.Single())
    End Sub

End Class
