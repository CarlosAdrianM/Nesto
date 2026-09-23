Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks

''' <summary>
''' Nesto#415 / Nesto#340 (Agencias, slice A4.3): «Contabilizar»
''' de la pestaña Reembolsos ya no abre un NestoEntities: el servidor contabiliza y aquí solo se
''' refleja (fecha de pago, fuera de la lista, selección vacía).
''' </summary>
<TestClass()>
Public Class AgenciasViewModelPagoReembolsosTests
    Private regionManager As IRegionManager
    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IDialogService
    Private servicioPedidos As IPedidoVentaService
    Private servicioAutenticacion As IServicioAutenticacion
    Private viewModel As AgenciasViewModel
    Private pagado1 As EnviosAgencia
    Private pagado2 As EnviosAgencia
    Private pendiente As EnviosAgencia

    <TestInitialize()>
    Public Sub Initialize()
        configuracion = A.Fake(Of IConfiguracion)
        regionManager = A.Fake(Of RegionManager)
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IDialogService)
        servicioPedidos = A.Fake(Of IPedidoVentaService)
        servicioAutenticacion = A.Fake(Of IServicioAutenticacion)
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)

        pagado1 = New EnviosAgencia With {.Numero = 248001, .Pedido = 925100, .Cliente = "15191     ", .Reembolso = 121.5}
        pagado2 = New EnviosAgencia With {.Numero = 248002, .Pedido = 925101, .Cliente = "29268     ", .Reembolso = 30}
        pendiente = New EnviosAgencia With {.Numero = 248003, .Pedido = 925102, .Cliente = "1         ", .Reembolso = 50}
        viewModel.listaReembolsos = New ObservableCollection(Of EnviosAgencia) From {pagado1, pagado2, pendiente}
        viewModel.listaReembolsosSeleccionados = New ObservableCollection(Of EnviosAgencia) From {pagado1, pagado2}
    End Sub

    <TestMethod()>
    Public Async Function PagarReembolsos_Exito_MandaLosDatosRecortadosYRefleja() As Task
        Dim enviado As PagoReembolsosDto = Nothing
        A.CallTo(Function() servicio.PagarReembolsos(A(Of PagoReembolsosDto).Ignored)) _
            .Invokes(Sub(datos As PagoReembolsosDto) enviado = datos) _
            .Returns(Task.FromResult(New ResultadoPagoReembolsosDto With {.Asiento = 88131, .Envios = 2, .Importe = 151.5D}))

        Await viewModel.PagarReembolsosSeleccionadosPorApi("1  ", "12345     ", 12, viewModel.listaReembolsosSeleccionados.ToList())

        Assert.IsNotNull(enviado)
        Assert.AreEqual("1", enviado.Empresa, "Empresa es char(3): viaja recortada")
        Assert.AreEqual("12345", enviado.Cliente, "Cliente es char(10): viaja recortado")
        Assert.AreEqual(12, enviado.Agencia)
        CollectionAssert.AreEqual({248001, 248002}, enviado.NumerosEnvio)

        Assert.AreEqual(Today, pagado1.FechaPagoReembolso.Value)
        Assert.AreEqual(Today, pagado2.FechaPagoReembolso.Value)
        Assert.IsNull(pendiente.FechaPagoReembolso)
        Assert.AreEqual(1, viewModel.listaReembolsos.Count, "los pagados salen de la lista; antes se quedaban hasta recargar")
        Assert.IsTrue(viewModel.listaReembolsos.Contains(pendiente))
        Assert.AreEqual(0, viewModel.listaReembolsosSeleccionados.Count)
    End Function

    <TestMethod()>
    Public Async Function PagarReembolsos_ElServidorRechaza_NoTocaNadaYEnsenaElMotivo() As Task
        A.CallTo(Function() servicio.PagarReembolsos(A(Of PagoReembolsosDto).Ignored)) _
            .Throws(New Exception("NestoAPI rechazó el pago de reembolsos (400): El reembolso del envío 248001 (pedido 925100) ya se pagó el 17/09/2026."))

        Await viewModel.PagarReembolsosSeleccionadosPorApi("1", "12345", 12, viewModel.listaReembolsosSeleccionados.ToList())

        Assert.IsNull(pagado1.FechaPagoReembolso)
        Assert.IsNull(pagado2.FechaPagoReembolso)
        Assert.AreEqual(3, viewModel.listaReembolsos.Count)
        Assert.AreEqual(2, viewModel.listaReembolsosSeleccionados.Count, "la selección se conserva para reintentar")
        ' ShowError pasa por ShowDialog sin callback.
        A.CallTo(Sub() dialogService.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).MustHaveHappenedOnceExactly()
    End Function

End Class
