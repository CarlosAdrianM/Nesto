Imports ControlesUsuario.Dialogs
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
''' Nesto#625 (NestoAPI#726): gestión en tránsito de los envíos de CTT en Agencias (parar en vuelo, crear retorno, cambiar la
''' fecha y la dirección de entrega). Los botones se activan según GET api/EnviosAgencias/{id}/Gestiones; cada operación
''' llama a su endpoint, enseña el mensaje de la API y refresca la historia y las gestiones.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelGestionTransitoTests
    Private servicio As IAgenciaService
    Private dialogService As IServicioDialogos
    Private viewModel As AgenciasViewModel
    Private envio As EnviosAgencia

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IServicioDialogos)
        viewModel = New AgenciasViewModel(servicio, A.Fake(Of IConfiguracion), dialogService, A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
        envio = New EnviosAgencia With {
            .Numero = 251300, .Pedido = 929000, .Agencia = 13, .Estado = CShort(1), .CodigoBarras = "0082800082809800827792",
            .Nombre = "Peluquería Ejemplo", .Direccion = "Calle Vieja 1", .CodPostal = "28001", .Poblacion = "Madrid",
            .Telefono = "910000000", .Movil = "600111222", .Email = "cliente@ejemplo.es"
        }
    End Sub

    Private Async Function SeleccionarConGestiones(gestiones As GestionesEnvioDto) As Task
        A.CallTo(Function() servicio.LeerGestionesEnvio(envio.Numero)).Returns(Task.FromResult(gestiones))
        viewModel.envioActual = envio
        Await viewModel.CargaGestiones
    End Function

    Private Shared Function Todas() As GestionesEnvioDto
        Return New GestionesEnvioDto With {.PararEnVuelo = True, .Retorno = True, .CambiarFecha = True, .CambiarDireccion = True}
    End Function

    ' ===== Botones según Gestiones =====

    <TestMethod()>
    Public Async Function Gestiones_CadaBotonSegunSuBooleano() As Task
        Await SeleccionarConGestiones(New GestionesEnvioDto With {.PararEnVuelo = True, .CambiarFecha = True})

        Assert.IsTrue(viewModel.PararEnVueloCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CrearRetornoCommand.CanExecute(Nothing))
        Assert.IsTrue(viewModel.CambiarFechaEntregaCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CambiarDireccionEntregaCommand.CanExecute(Nothing))
        Assert.AreEqual(String.Empty, viewModel.MotivoSinGestiones, "Si se puede alguna, no se enseña el motivo")
    End Function

    <TestMethod()>
    Public Async Function Gestiones_NingunaPosible_EnsenaElMotivo() As Task
        Await SeleccionarConGestiones(New GestionesEnvioDto With {.Motivo = "  Solo los envíos de CTT ya recogidos.  "})

        Assert.IsFalse(viewModel.PararEnVueloCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CrearRetornoCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CambiarFechaEntregaCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CambiarDireccionEntregaCommand.CanExecute(Nothing))
        Assert.AreEqual("Solo los envíos de CTT ya recogidos.", viewModel.MotivoSinGestiones)
    End Function

    <TestMethod()>
    Public Async Function Gestiones_SinNumeroDeLaAgencia_NoSePreguntaALaApi() As Task
        envio.CodigoBarras = Nothing

        Await SeleccionarConGestiones(Todas())

        A.CallTo(Function() servicio.LeerGestionesEnvio(A(Of Integer).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(viewModel.PararEnVueloCommand.CanExecute(Nothing))
    End Function

    <TestMethod()>
    Public Async Function Gestiones_LaApiFalla_TodoDesactivadoSinAvisar() As Task
        A.CallTo(Function() servicio.LeerGestionesEnvio(envio.Numero)).Throws(New Exception("404"))
        viewModel.envioActual = envio
        Await viewModel.CargaGestiones

        Assert.IsFalse(viewModel.PararEnVueloCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CambiarDireccionEntregaCommand.CanExecute(Nothing))
        A.CallTo(Sub() dialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Gestiones_SinEnvioSeleccionado_TodoDesactivado() As Task
        Await SeleccionarConGestiones(Todas())

        viewModel.envioActual = Nothing
        Await viewModel.CargaGestiones

        Assert.IsFalse(viewModel.PararEnVueloCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CrearRetornoCommand.CanExecute(Nothing))
    End Function

    ' ===== Parar en vuelo =====

    <TestMethod()>
    Public Async Function PararEnVuelo_ConfirmacionCancelada_NoLlamaALaApi() As Task
        Await SeleccionarConGestiones(Todas())
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(False)

        Await viewModel.PararEnVueloSeleccionado()

        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Parar en vuelo",
            A(Of String).That.Contains("¿Seguro que quiere parar en vuelo el envío 251300"), True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored,
            A(Of String).That.Contains("La agencia dejará de intentar la entrega."), A(Of Boolean).Ignored)).MustHaveHappened()
        A.CallTo(Function() servicio.PararEnVueloEnvio(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function PararEnVuelo_Confirmado_LlamaEnsenaElMensajeYRefresca() As Task
        Await SeleccionarConGestiones(Todas())
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        A.CallTo(Function() servicio.PararEnVueloEnvio(251300)).Returns(Task.FromResult(New MensajeGestionEnvioDto With {.Mensaje = "Parada al vuelo pedida a CTT."}))
        Dim historia As New ObservableCollection(Of EnviosHistoria) From {New EnviosHistoria With {.Observaciones = "Parar en vuelo"}}
        A.CallTo(Function() servicio.CargarListaHistoriaEnvio(251300)).Returns(historia)

        Await viewModel.PararEnVueloSeleccionado()

        A.CallTo(Function() servicio.PararEnVueloEnvio(251300)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Parar en vuelo", "Parada al vuelo pedida a CTT.")).MustHaveHappenedOnceExactly()
        Assert.AreSame(historia, viewModel.listaHistoriaEnvio, "Se recarga la historia (la API escribe una línea)")
        A.CallTo(Function() servicio.LeerGestionesEnvio(251300)).MustHaveHappenedTwiceExactly()
        Assert.IsFalse(viewModel.GestionEnCurso)
        Assert.IsFalse(viewModel.EstaOcupado)
    End Function

    <TestMethod()>
    Public Async Function PararEnVuelo_MientrasEstaEnCurso_BotonesDesactivados() As Task
        Await SeleccionarConGestiones(Todas())
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        Dim respuesta As New TaskCompletionSource(Of MensajeGestionEnvioDto)
        A.CallTo(Function() servicio.PararEnVueloEnvio(251300)).Returns(respuesta.Task)

        Dim llamada As Task = viewModel.PararEnVueloSeleccionado()

        Assert.IsTrue(viewModel.GestionEnCurso)
        Assert.IsFalse(viewModel.PararEnVueloCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CrearRetornoCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CambiarFechaEntregaCommand.CanExecute(Nothing))
        Assert.IsFalse(viewModel.CambiarDireccionEntregaCommand.CanExecute(Nothing))

        respuesta.SetResult(New MensajeGestionEnvioDto With {.Mensaje = "Hecho"})
        Await llamada

        Assert.IsFalse(viewModel.GestionEnCurso)
        Assert.IsTrue(viewModel.PararEnVueloCommand.CanExecute(Nothing))
    End Function

    <TestMethod()>
    Public Async Function PararEnVuelo_Error400_EnsenaElMensajeTalCual() As Task
        Await SeleccionarConGestiones(Todas())
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        A.CallTo(Function() servicio.PararEnVueloEnvio(251300)).Throws(New Exception("CTT no permite parar en vuelo un envío entregado."))

        Await viewModel.PararEnVueloSeleccionado()

        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("CTT no permite parar en vuelo un envío entregado."))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        Assert.IsFalse(viewModel.GestionEnCurso)
        Assert.IsFalse(viewModel.EstaOcupado)
    End Function

    ' ===== Crear retorno =====

    <TestMethod()>
    Public Async Function CrearRetorno_ConfirmacionCancelada_NoLlamaALaApi() As Task
        Await SeleccionarConGestiones(Todas())
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(False)

        Await viewModel.CrearRetornoSeleccionado()

        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Crear retorno", A(Of String).That.Contains("¿Seguro que quiere crear el retorno"), True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CrearRetornoEnvio(A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CrearRetorno_Confirmado_EnsenaElCodigoNuevoYRefresca() As Task
        Await SeleccionarConGestiones(Todas())
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(True)
        A.CallTo(Function() servicio.CrearRetornoEnvio(251300)).Returns(Task.FromResult(New RetornoEnvioDto With {.CodigoRetorno = "0082800031029800000086", .EnvioRetornoId = 251999}))

        Await viewModel.CrearRetornoSeleccionado()

        A.CallTo(Function() servicio.CrearRetornoEnvio(251300)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Crear retorno", A(Of String).That.Contains("0082800031029800000086"))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Crear retorno", A(Of String).That.Contains("251999"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CargarListaHistoriaEnvio(251300)).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Sub MensajeRetornoCreado_SinEnvioEnNesto_SoloElCodigo()
        Dim mensaje As String = AgenciasViewModel.MensajeRetornoCreado(New RetornoEnvioDto With {.CodigoRetorno = "0082800031029800000086"})

        StringAssert.Contains(mensaje, "0082800031029800000086")
        Assert.IsFalse(mensaje.Contains("En Nesto es el envío"))
    End Sub

    ' ===== Cambiar fecha =====

    <TestMethod()>
    Public Async Function CambiarFecha_ProponeElSiguienteLaborableYMandaLaElegida() As Task
        Await SeleccionarConGestiones(Todas())
        Dim nueva As New Date(2099, 11, 6)
        A.CallTo(Function() dialogService.GetDate(A(Of String).Ignored, A(Of String).Ignored, A(Of Date?).Ignored)).Returns(nueva)
        A.CallTo(Function() servicio.CambiarFechaEntregaEnvio(251300, nueva)).Returns(Task.FromResult(New MensajeGestionEnvioDto With {.Mensaje = "Fecha cambiada."}))

        Await viewModel.CambiarFechaEntregaSeleccionado()

        A.CallTo(Function() dialogService.GetDate("Cambiar fecha de entrega", A(Of String).Ignored,
            AgenciasViewModel.SiguienteDiaLaborable(Date.Today))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CambiarFechaEntregaEnvio(251300, nueva)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Cambiar fecha de entrega", "Fecha cambiada.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CargarListaHistoriaEnvio(251300)).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CambiarFecha_Cancelada_NoLlamaALaApi() As Task
        Await SeleccionarConGestiones(Todas())
        A.CallTo(Function() dialogService.GetDate(A(Of String).Ignored, A(Of String).Ignored, A(Of Date?).Ignored)).Returns(Nothing)

        Await viewModel.CambiarFechaEntregaSeleccionado()

        A.CallTo(Function() servicio.CambiarFechaEntregaEnvio(A(Of Integer).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CambiarFecha_Pasada_AvisaYNoLlama() As Task
        Await SeleccionarConGestiones(Todas())
        A.CallTo(Function() dialogService.GetDate(A(Of String).Ignored, A(Of String).Ignored, A(Of Date?).Ignored)).Returns(Date.Today.AddDays(-1))

        Await viewModel.CambiarFechaEntregaSeleccionado()

        A.CallTo(Function() servicio.CambiarFechaEntregaEnvio(A(Of Integer).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError("La nueva fecha de entrega no puede ser anterior a hoy.")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub SiguienteDiaLaborable_SaltaElFinDeSemana()
        Assert.AreEqual(New Date(2026, 11, 6), AgenciasViewModel.SiguienteDiaLaborable(New Date(2026, 11, 5)), "jueves → viernes")
        Assert.AreEqual(New Date(2026, 11, 9), AgenciasViewModel.SiguienteDiaLaborable(New Date(2026, 11, 6)), "viernes → lunes")
        Assert.AreEqual(New Date(2026, 11, 9), AgenciasViewModel.SiguienteDiaLaborable(New Date(2026, 11, 7)), "sábado → lunes")
        Assert.AreEqual(New Date(2026, 11, 9), AgenciasViewModel.SiguienteDiaLaborable(New Date(2026, 11, 8, 18, 30, 0)), "domingo → lunes")
    End Sub

    ' ===== Cambiar dirección =====

    Private Sub ElDialogoDevuelve(boton As ResultadoBoton, Optional telefono As String = "600999888", Optional correo As String = "nuevo@ejemplo.es")
        Dim parametros As New ParametrosDialogo From {
            {DireccionEntregaEnvioDialogViewModel.PARAMETRO_NOMBRE, "Peluquería Nueva"},
            {DireccionEntregaEnvioDialogViewModel.PARAMETRO_DIRECCION, "Calle Nueva 7"},
            {DireccionEntregaEnvioDialogViewModel.PARAMETRO_COD_POSTAL, "28002"},
            {DireccionEntregaEnvioDialogViewModel.PARAMETRO_POBLACION, "Madrid"},
            {DireccionEntregaEnvioDialogViewModel.PARAMETRO_PAIS, "ES"},
            {DireccionEntregaEnvioDialogViewModel.PARAMETRO_TELEFONO, telefono},
            {DireccionEntregaEnvioDialogViewModel.PARAMETRO_CORREO, correo}
        }
        A.CallTo(Function() dialogService.ShowDialogAsync(DireccionEntregaEnvioDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored)).
            Returns(Task.FromResult(New ResultadoDialogo(boton, parametros)))
    End Sub

    <TestMethod()>
    Public Async Function CambiarDireccion_AbreElDialogoConLosDatosDelEnvio() As Task
        Await SeleccionarConGestiones(Todas())
        ElDialogoDevuelve(ResultadoBoton.Cancel)

        Await viewModel.CambiarDireccionEntregaSeleccionado()

        A.CallTo(Function() dialogService.ShowDialogAsync(DireccionEntregaEnvioDialogViewModel.NOMBRE, A(Of ParametrosDialogo).That.Matches(
            Function(p) p.GetValue(Of Integer)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_ENVIO) = 251300 AndAlso
                p.GetValue(Of String)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_PEDIDO) = "929000" AndAlso
                p.GetValue(Of String)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_NOMBRE) = "Peluquería Ejemplo" AndAlso
                p.GetValue(Of String)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_DIRECCION) = "Calle Vieja 1" AndAlso
                p.GetValue(Of String)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_COD_POSTAL) = "28001" AndAlso
                p.GetValue(Of String)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_POBLACION) = "Madrid" AndAlso
                p.GetValue(Of String)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_PAIS) = "ES" AndAlso
                p.GetValue(Of String)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_TELEFONO) = "600111222" AndAlso
                p.GetValue(Of String)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_CORREO) = "cliente@ejemplo.es"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CambiarDireccionEntregaEnvio(A(Of Integer).Ignored, A(Of DireccionEntregaEnvioPeticion).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub ParametrosDireccionEntrega_SinMovil_ElTelefonoFijo()
        envio.Movil = "  "

        Dim parametros As ParametrosDialogo = AgenciasViewModel.ParametrosDireccionEntrega(envio)

        Assert.AreEqual("910000000", parametros.GetValue(Of String)(DireccionEntregaEnvioDialogViewModel.PARAMETRO_TELEFONO))
    End Sub

    <TestMethod()>
    Public Async Function CambiarDireccion_Aceptada_MandaElCuerpoYRefresca() As Task
        Await SeleccionarConGestiones(Todas())
        ElDialogoDevuelve(ResultadoBoton.OK)
        A.CallTo(Function() servicio.CambiarDireccionEntregaEnvio(251300, A(Of DireccionEntregaEnvioPeticion).Ignored)).
            Returns(Task.FromResult(New MensajeGestionEnvioDto With {.Mensaje = "Dirección cambiada en CTT."}))

        Await viewModel.CambiarDireccionEntregaSeleccionado()

        A.CallTo(Function() servicio.CambiarDireccionEntregaEnvio(251300, A(Of DireccionEntregaEnvioPeticion).That.Matches(
            Function(d) d.Nombre = "Peluquería Nueva" AndAlso d.Direccion = "Calle Nueva 7" AndAlso d.CodigoPostal = "28002" AndAlso
                d.Poblacion = "Madrid" AndAlso d.Pais = "ES" AndAlso d.Telefono = "600999888" AndAlso d.Correo = "nuevo@ejemplo.es"))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Cambiar dirección de entrega", "Dirección cambiada en CTT.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CargarListaHistoriaEnvio(251300)).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CambiarDireccion_SinCorreo_AvisaYNoLlama() As Task
        Await SeleccionarConGestiones(Todas())
        ElDialogoDevuelve(ResultadoBoton.OK, correo:="")

        Await viewModel.CambiarDireccionEntregaSeleccionado()

        A.CallTo(Function() servicio.CambiarDireccionEntregaEnvio(A(Of Integer).Ignored, A(Of DireccionEntregaEnvioPeticion).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("correo electrónico válido"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function CambiarDireccion_SinTelefono_AvisaYNoLlama() As Task
        Await SeleccionarConGestiones(Todas())
        ElDialogoDevuelve(ResultadoBoton.OK, telefono:="  ")

        Await viewModel.CambiarDireccionEntregaSeleccionado()

        A.CallTo(Function() servicio.CambiarDireccionEntregaEnvio(A(Of Integer).Ignored, A(Of DireccionEntregaEnvioPeticion).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("teléfono de contacto"))).MustHaveHappenedOnceExactly()
    End Function

    ' ===== Servicio: contrato con la API =====

    <TestMethod()>
    Public Sub Servicio_RutasDeLosEndpoints()
        Assert.AreEqual("EnviosAgencias/251300/Gestiones", AgenciaService.RutaGestionEnvio(251300, AgenciaService.GESTION_GESTIONES))
        Assert.AreEqual("EnviosAgencias/251300/PararEnVuelo", AgenciaService.RutaGestionEnvio(251300, AgenciaService.GESTION_PARAR_EN_VUELO))
        Assert.AreEqual("EnviosAgencias/251300/Retorno", AgenciaService.RutaGestionEnvio(251300, AgenciaService.GESTION_RETORNO))
        Assert.AreEqual("EnviosAgencias/251300/FechaEntrega", AgenciaService.RutaGestionEnvio(251300, AgenciaService.GESTION_FECHA_ENTREGA))
        Assert.AreEqual("EnviosAgencias/251300/DireccionEntrega", AgenciaService.RutaGestionEnvio(251300, AgenciaService.GESTION_DIRECCION_ENTREGA))
    End Sub

    <TestMethod()>
    Public Sub Servicio_CuerpoFechaEntrega_SoloLaFecha()
        Assert.AreEqual("{""Fecha"":""2026-11-06""}", AgenciaService.CuerpoFechaEntrega(New Date(2026, 11, 6, 15, 30, 0)))
    End Sub

    <TestMethod()>
    Public Sub Servicio_CuerpoDireccionEntrega_MismosNombresQueElServidor()
        Dim json As String = JsonConvert.SerializeObject(New DireccionEntregaEnvioPeticion With {
            .Nombre = "N", .Direccion = "D", .CodigoPostal = "28001", .Poblacion = "P", .Pais = "ES", .Telefono = "600111222", .Correo = "a@b.es"})

        Assert.AreEqual("{""Nombre"":""N"",""Direccion"":""D"",""CodigoPostal"":""28001"",""Poblacion"":""P"",""Pais"":""ES"",""Telefono"":""600111222"",""Correo"":""a@b.es""}", json)
    End Sub

    <TestMethod()>
    Public Sub Servicio_DeserializaGestionesYRetorno()
        Dim gestiones = JsonConvert.DeserializeObject(Of GestionesEnvioDto)("{""PararEnVuelo"":true,""Retorno"":false,""CambiarFecha"":true,""CambiarDireccion"":false,""Motivo"":null}")
        Assert.IsTrue(gestiones.PararEnVuelo)
        Assert.IsFalse(gestiones.Retorno)
        Assert.IsTrue(gestiones.CambiarFecha)
        Assert.IsFalse(gestiones.CambiarDireccion)

        Dim retorno = JsonConvert.DeserializeObject(Of RetornoEnvioDto)("{""CodigoRetorno"":""0082800031029800000086"",""EnvioRetornoId"":null}")
        Assert.AreEqual("0082800031029800000086", retorno.CodigoRetorno)
        Assert.IsFalse(retorno.EnvioRetornoId.HasValue)
    End Sub

    <TestMethod()>
    Public Sub Servicio_Error400_ElMensajeDeLaApiTalCual()
        Assert.AreEqual("CTT no lo permite en el estado actual del envío.",
            AgenciaService.MensajeErrorGestion(400, "{""Message"":""CTT no lo permite en el estado actual del envío.""}"))
        Assert.AreEqual("El envío no existe.", AgenciaService.MensajeErrorGestion(404, ""))
        StringAssert.Contains(AgenciaService.MensajeErrorGestion(500, "{""Message"":""Boom""}"), "500")
    End Sub
End Class
