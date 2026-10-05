Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports ControlesUsuario.Models
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels

' Nesto#259 (Manuel, 05/10/26): en la ficha comercial se marcan facturas, se escribe el correo (se propone el de
' facturas del cliente), se confirma y se mandan por la API en un solo correo.
<TestClass()>
Public Class ClientesViewModelEnvioFacturasTests

    Private _configuracion As IConfiguracion
    Private _dialogService As IServicioDialogos
    Private _servicio As IClienteComercialService

    <TestInitialize()>
    Public Sub Initialize()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogService = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IClienteComercialService)()
        A.CallTo(Function() _dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)
    End Sub

    Private Function CrearViewModel() As ClientesViewModel
        Return New ClientesViewModel(_configuracion, _dialogService, _servicio, A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)())
    End Function

    Private Shared Function Facturas(ParamArray numeros As String()) As ObservableCollection(Of FacturaClienteDTO)
        Return New ObservableCollection(Of FacturaClienteDTO)(numeros.Select(Function(n) New FacturaClienteDTO With {.Empresa = "1", .Cliente = "15191", .Documento = n}))
    End Function

    <TestMethod()>
    Public Async Function ProponerCorreo_SinCorreoEscrito_PoneElDeFacturasDelCliente() As Task
        A.CallTo(Function() _servicio.LeerCorreoFacturas("1", "NV11111")).Returns(Task.FromResult("cliente@correo.es"))
        Dim vm = CrearViewModel()
        vm.FacturasSeleccionadas = Facturas("NV11111")

        Await vm.ProponerCorreoFacturasAsync()

        Assert.AreEqual("cliente@correo.es", vm.CorreoEnvioFacturas)
    End Function

    <TestMethod()>
    Public Async Function ProponerCorreo_SiYaHayUnoEscrito_NoLoPisa() As Task
        A.CallTo(Function() _servicio.LeerCorreoFacturas("1", "NV11111")).Returns(Task.FromResult("cliente@correo.es"))
        Dim vm = CrearViewModel()
        vm.CorreoEnvioFacturas = "gestoria@correo.es"
        vm.FacturasSeleccionadas = Facturas("NV11111")

        Await vm.ProponerCorreoFacturasAsync()

        Assert.AreEqual("gestoria@correo.es", vm.CorreoEnvioFacturas)
    End Function

    <TestMethod()>
    Public Async Function ProponerCorreo_SiSeMarcanFacturasDeOtroCliente_CambiaElPropuestoPeroNoElEscrito() As Task
        A.CallTo(Function() _servicio.LeerCorreoFacturas("1", "NV11111")).Returns(Task.FromResult("cliente@correo.es"))
        A.CallTo(Function() _servicio.LeerCorreoFacturas("1", "NV99999")).Returns(Task.FromResult("otro@correo.es"))
        Dim vm = CrearViewModel()
        vm.FacturasSeleccionadas = Facturas("NV11111")
        Await vm.ProponerCorreoFacturasAsync()

        vm.FacturasSeleccionadas = New ObservableCollection(Of FacturaClienteDTO) From {New FacturaClienteDTO With {.Empresa = "1", .Cliente = "20000", .Documento = "NV99999"}}
        Await vm.ProponerCorreoFacturasAsync()

        Assert.AreEqual("otro@correo.es", vm.CorreoEnvioFacturas, "El propuesto era del cliente anterior")
    End Function

    <TestMethod()>
    Public Async Function Enviar_ConfirmaYMandaLasFacturasMarcadasAlCorreoEscrito() As Task
        Dim vm = CrearViewModel()
        vm.FacturasSeleccionadas = Facturas("NV11111", "NV22222")
        vm.CorreoEnvioFacturas = "gestoria@correo.es; cliente@correo.es"
        A.CallTo(Function() _servicio.EnviarFacturasPorCorreo(A(Of String).Ignored, A(Of List(Of String)).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(New ResultadoEnvioFacturasCorreo With {.Enviado = True, .Mensaje = "Enviadas NV11111, NV22222 a gestoria@correo.es, cliente@correo.es."}))

        Await vm.EnviarFacturasPorCorreoAsync()

        A.CallTo(Function() _dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).That.Contains("2 facturas a gestoria@correo.es; cliente@correo.es"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.EnviarFacturasPorCorreo("1", A(Of List(Of String)).That.Matches(Function(l) l.SequenceEqual({"NV11111", "NV22222"})), "gestoria@correo.es; cliente@correo.es")).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogService.ShowNotification(A(Of String).Ignored, A(Of String).That.Contains("Enviadas"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Enviar_SiNoSeConfirma_NoMandaNada() As Task
        A.CallTo(Function() _dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(False)
        Dim vm = CrearViewModel()
        vm.FacturasSeleccionadas = Facturas("NV11111")
        vm.CorreoEnvioFacturas = "cliente@correo.es"

        Await vm.EnviarFacturasPorCorreoAsync()

        A.CallTo(Function() _servicio.EnviarFacturasPorCorreo(A(Of String).Ignored, A(Of List(Of String)).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Enviar_SinCorreo_LoPideSinPreguntarNiMandar() As Task
        Dim vm = CrearViewModel()
        vm.FacturasSeleccionadas = Facturas("NV11111")
        vm.CorreoEnvioFacturas = "  "

        Await vm.EnviarFacturasPorCorreoAsync()

        A.CallTo(Sub() _dialogService.ShowError(A(Of String).That.Contains("correo"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.EnviarFacturasPorCorreo(A(Of String).Ignored, A(Of List(Of String)).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Enviar_SiLaApiLoRechaza_EnsenaSuMotivo() As Task
        Dim vm = CrearViewModel()
        vm.FacturasSeleccionadas = Facturas("NV11111")
        vm.CorreoEnvioFacturas = "clientecorreo.es"
        A.CallTo(Function() _servicio.EnviarFacturasPorCorreo(A(Of String).Ignored, A(Of List(Of String)).Ignored, A(Of String).Ignored)) _
            .Throws(New Exception("No es un correo válido: clientecorreo.es."))

        Await vm.EnviarFacturasPorCorreoAsync()

        A.CallTo(Sub() _dialogService.ShowError(A(Of String).That.Contains("clientecorreo.es"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Enviar_SiElCorreoNoSale_LoDice() As Task
        Dim vm = CrearViewModel()
        vm.FacturasSeleccionadas = Facturas("NV11111")
        vm.CorreoEnvioFacturas = "cliente@correo.es"
        A.CallTo(Function() _servicio.EnviarFacturasPorCorreo(A(Of String).Ignored, A(Of List(Of String)).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(New ResultadoEnvioFacturasCorreo With {.Enviado = False, .Mensaje = "El correo no se ha podido enviar."}))

        Await vm.EnviarFacturasPorCorreoAsync()

        A.CallTo(Sub() _dialogService.ShowError(A(Of String).That.Contains("no se ha podido enviar"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub PuedeEnviar_SoloConFacturasMarcadas()
        Dim vm = CrearViewModel()
        Assert.IsFalse(vm.EnviarFacturasPorCorreoCommand.CanExecute(Nothing))

        vm.FacturasSeleccionadas = Facturas("NV11111")

        Assert.IsTrue(vm.EnviarFacturasPorCorreoCommand.CanExecute(Nothing))
    End Sub
End Class
