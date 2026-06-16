Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta
Imports Prism.Events
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports Unity

''' <summary>
''' Nesto#385: "Copiar al portapapeles" en DetallePedido lanzaba NullReferenceException cuando
''' no había pedido cargado (ToString accedía a pedido.numero con pedido = Nothing, y el comando
''' no tenía CanExecute). El setter de pedido ignora los null, así que pedido sigue Nothing hasta
''' que se carga uno real.
''' </summary>
<TestClass()>
Public Class DetallePedidoViewModelCopiarPortapapelesTests

    Private regionManager As IRegionManager
    Private configuracion As IConfiguracion
    Private servicio As IPedidoVentaService
    Private eventAggregator As IEventAggregator
    Private dialogService As IDialogService
    Private container As IUnityContainer
    Private servicioAutenticacion As IServicioAutenticacion

    <TestInitialize()>
    Public Sub Initialize()
        regionManager = A.Fake(Of IRegionManager)
        configuracion = A.Fake(Of IConfiguracion)
        servicio = A.Fake(Of IPedidoVentaService)
        eventAggregator = A.Fake(Of IEventAggregator)
        dialogService = A.Fake(Of IDialogService)
        container = A.Fake(Of IUnityContainer)
        servicioAutenticacion = A.Fake(Of IServicioAutenticacion)
    End Sub

    Private Function CrearViewModel() As DetallePedidoViewModel
        Return New DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container, servicioAutenticacion)
    End Function

    <TestMethod()>
    Public Sub ToString_SinPedidoCargado_NoLanzaYDevuelveCadenaVacia()
        ' Arrange - recién construido, sin cargar ningún pedido (pedido = Nothing)
        Dim vm = CrearViewModel()

        ' Act
        Dim resultado = vm.ToString()

        ' Assert
        Assert.AreEqual(String.Empty, resultado,
                        "Sin pedido cargado, ToString no debe acceder a pedido.numero ni lanzar NRE")
    End Sub

    <TestMethod()>
    Public Sub CopiarAlPortapapelesCommand_SinPedido_NoSePuedeEjecutar()
        ' Arrange
        Dim vm = CrearViewModel()

        ' Act & Assert - el botón no debe poder ejecutarse si no hay pedido
        Assert.IsFalse(vm.CopiarAlPortapapelesCommand.CanExecute(),
                       "Sin pedido cargado, el comando Copiar al portapapeles no debe poder ejecutarse")
    End Sub

End Class
