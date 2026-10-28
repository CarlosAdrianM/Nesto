Imports ControlesUsuario.Dialogs
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels

''' <summary>
''' Nesto#564 / NestoAPI#725 (Carlos, 28/10/26, envío 249990): «Modificar Envío» de En curso sobre un envío ya registrado en
''' CTT/Innovatrans. Si el PUT contesta 409 CAMBIO_SIN_AGENCIA, aviso en rojo dos veces (la segunda con quién se habló en la
''' agencia y cuándo) y, solo entonces, el PUT de nuevo con la confirmación. Si no se confirma, no se guarda nada.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelCambioSinAgenciaTests
    Private Const CONFIRMACION As String = "Marta de CTT Alcobendas, 28/10 a las 12:30"

    Private servicio As IAgenciaService
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel
    Private envio As EnviosAgencia
    Private dialogosAbiertos As List(Of ParametrosDialogo)

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        viewModel = New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogService,
                                          A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        envio = New EnviosAgencia With {.Numero = 249990, .Agencia = 13, .Retorno = 1, .Estado = 0, .CodigoBarras = "0082800082809772552452"}
        dialogosAbiertos = New List(Of ParametrosDialogo)
    End Sub

    Private Sub ElServidorContesta409()
        A.CallTo(Sub() servicio.Modificar(envio)).Throws(
            New CambioSinAgenciaException(249990, {"Retorno"}, "El envío 249990 ya está registrado en la agencia..."))
    End Sub

    ''' <summary>Respuestas del usuario a los diálogos en rojo, en orden (Nothing = «No, cancelar»; "" = Sí sin texto).</summary>
    Private Sub ElUsuarioContesta(ParamArray respuestas As String())
        ' ElUsuarioContesta(Nothing) llega como ParamArray Nothing: es un único «No».
        Dim cola As New Queue(Of String)(If(respuestas, New String() {Nothing}))
        A.CallTo(Sub() dialogService.ShowDialog(CambioSinAgenciaDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
            .Invokes(Sub(nombre As String, parametros As ParametrosDialogo, callback As Action(Of ResultadoDialogo))
                         dialogosAbiertos.Add(parametros)
                         Dim respuesta As String = If(cola.Count > 0, cola.Dequeue(), Nothing)
                         If respuesta Is Nothing Then
                             callback?.Invoke(New ResultadoDialogo(ResultadoBoton.Cancel))
                         Else
                             callback?.Invoke(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {
                                 {CambioSinAgenciaDialogViewModel.PARAMETRO_TEXTO, respuesta}}))
                         End If
                     End Sub)
    End Sub

    <TestMethod()>
    Public Sub SinConflicto_GuardaSinPreguntar()
        Dim guardado = viewModel.GuardarConfirmandoConLaAgencia(envio)

        Assert.IsTrue(guardado)
        A.CallTo(Sub() servicio.Modificar(envio)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() servicio.ModificarConfirmadoConLaAgencia(A(Of EnviosAgencia).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub Conflicto409_EnseñaElAvisoEnRojoConLosCampos()
        ElServidorContesta409()
        ElUsuarioContesta(Nothing)

        Dim unused = viewModel.GuardarConfirmandoConLaAgencia(envio)

        Assert.AreEqual(1, dialogosAbiertos.Count)
        Dim mensaje As String = dialogosAbiertos(0).GetValue(Of String)(CambioSinAgenciaDialogViewModel.PARAMETRO_MENSAJE)
        StringAssert.Contains(mensaje, "campos: Retorno")
        StringAssert.Contains(mensaje, "NUESTRO sistema pero NO en el de la agencia")
        StringAssert.Contains(mensaje, "Modificar datos y reenviar a la agencia")
        Assert.IsFalse(dialogosAbiertos(0).GetValue(Of Boolean)(CambioSinAgenciaDialogViewModel.PARAMETRO_PEDIR_TEXTO), "la primera solo pregunta")
    End Sub

    <TestMethod()>
    Public Sub Conflicto409_CancelaLaPrimera_NoHaySegundoPut()
        ElServidorContesta409()
        ElUsuarioContesta(Nothing)

        Dim guardado = viewModel.GuardarConfirmandoConLaAgencia(envio)

        Assert.IsFalse(guardado)
        A.CallTo(Sub() servicio.ModificarConfirmadoConLaAgencia(A(Of EnviosAgencia).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub Conflicto409_CancelaLaSegunda_NoHaySegundoPut()
        ElServidorContesta409()
        ElUsuarioContesta("", Nothing)

        Dim guardado = viewModel.GuardarConfirmandoConLaAgencia(envio)

        Assert.IsFalse(guardado)
        Assert.AreEqual(2, dialogosAbiertos.Count)
        Assert.IsTrue(dialogosAbiertos(1).GetValue(Of Boolean)(CambioSinAgenciaDialogViewModel.PARAMETRO_PEDIR_TEXTO), "la segunda pide con quién se habló")
        A.CallTo(Sub() servicio.ModificarConfirmadoConLaAgencia(A(Of EnviosAgencia).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub Conflicto409_TextoDemasiadoCorto_NoHaySegundoPut()
        ElServidorContesta409()
        ElUsuarioContesta("", " CTT ")

        Assert.IsFalse(viewModel.GuardarConfirmandoConLaAgencia(envio))
        A.CallTo(Sub() servicio.ModificarConfirmadoConLaAgencia(A(Of EnviosAgencia).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub Conflicto409_ConfirmadoDosVeces_SegundoPutConElTextoYAviso()
        ElServidorContesta409()
        ElUsuarioContesta("", CONFIRMACION)

        Dim guardado = viewModel.GuardarConfirmandoConLaAgencia(envio)

        Assert.IsTrue(guardado)
        A.CallTo(Sub() servicio.ModificarConfirmadoConLaAgencia(envio, CONFIRMACION)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Modificar Envío", A(Of String).That.Contains(CONFIRMACION))).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod()>
    Public Sub BotonModificarEnvio_Conflicto409Cancelado_NoEnseñaErrorNiGuarda()
        ElServidorContesta409()
        ElUsuarioContesta(Nothing)
        A.CallTo(Sub() dialogService.ShowConfirmation(A(Of String).Ignored, A(Of String).Ignored, A(Of Action(Of ResultadoDialogo)).Ignored)) _
            .Invokes(Sub(titulo As String, mensaje As String, callback As Action(Of ResultadoDialogo))
                         callback?.Invoke(New ResultadoDialogo(ResultadoBoton.OK))
                     End Sub)
        viewModel.envioActual = envio

        viewModel.cmdModificar.Execute(Nothing)

        A.CallTo(Sub() servicio.Modificar(envio)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(1, dialogosAbiertos.Count)
        A.CallTo(Sub() servicio.ModificarConfirmadoConLaAgencia(A(Of EnviosAgencia).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Sub

    ' ---- El contrato con la API ----

    <TestMethod()>
    Public Sub DesdeRespuesta_409CambioSinAgencia_LeeCamposYMensaje()
        Dim cuerpo As String = "{""Message"":""El envío 249990 ya está registrado..."",""Codigo"":""CAMBIO_SIN_AGENCIA""," &
            """Campos"":[""Retorno"",""Reembolso""],""Mensaje"":""El envío 249990 ya está registrado en la agencia.""}"

        Dim ex = CambioSinAgenciaException.DesdeRespuesta(249990, 409, cuerpo)

        Assert.IsNotNull(ex)
        Assert.AreEqual(249990, ex.NumeroEnvio)
        CollectionAssert.AreEqual({"Retorno", "Reembolso"}, ex.Campos.ToArray())
        Assert.AreEqual("El envío 249990 ya está registrado en la agencia.", ex.Message)
    End Sub

    <TestMethod()>
    Public Sub DesdeRespuesta_OtrosErrores_Nothing()
        Assert.IsNull(CambioSinAgenciaException.DesdeRespuesta(249990, 400, "{""Codigo"":""CAMBIO_SIN_AGENCIA""}"))
        Assert.IsNull(CambioSinAgenciaException.DesdeRespuesta(249990, 409, "{""Codigo"":""OTRO""}"))
        Assert.IsNull(CambioSinAgenciaException.DesdeRespuesta(249990, 409, "El pedido está retenido"))
        Assert.IsNull(CambioSinAgenciaException.DesdeRespuesta(249990, 409, ""))
    End Sub

    <TestMethod()>
    Public Sub RutaModificar_SinConfirmacion_LaDeSiempre_YConConfirmacion_ConElTextoEscapado()
        Assert.AreEqual("EnviosAgencias/249990", AgenciaService.RutaModificar(249990, Nothing))
        Assert.AreEqual("EnviosAgencias/249990?confirmadoConLaAgencia=true&confirmacionAgencia=Marta%20de%20CTT%2C%2028%2F10%20%26%2012%3A30",
                        AgenciaService.RutaModificar(249990, " Marta de CTT, 28/10 & 12:30 "))
    End Sub
End Class
