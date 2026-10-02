Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Modulos.CarteraPagos
Imports Nesto.Modulos.Inventario
Imports Nesto.ViewModels

''' <summary>
''' Nesto#490 (4C.4): adónde navegan Cartera de pagos, Inventario y Remesas. Escritas contra el
''' IRegionManager de Prism ANTES de migrar la navegación (d7b3b306); al pasar a IServicioNavegacion
''' solo ha cambiado el tipo del fake: mismas regiones, vistas y claves.
''' </summary>
<TestClass()>
Public Class NavegacionViewModelsTests

    Private _navegacion As IServicioNavegacion

    <TestInitialize()>
    Public Sub Inicializar()
        _navegacion = A.Fake(Of IServicioNavegacion)()
    End Sub

    <TestMethod()>
    Public Sub CarteraPagos_Abrir_AbreLaVistaDeCarteraDePagos()
        Dim vm As New CarteraPagosViewModel(_navegacion, A.Fake(Of IConfiguracion)(), A.Fake(Of ICarteraPagosService)(), A.Fake(Of IServicioDialogos)())

        vm.cmdAbrirCarteraPagos.Execute(Nothing)

        A.CallTo(Sub() _navegacion.RequestNavigate("MainRegion", "CarteraPagosView")).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod()>
    Public Sub Inventario_Abrir_AbreLaVistaDeInventario()
        Dim vm As New InventarioViewModel(_navegacion, A.Fake(Of IConfiguracion)(), A.Fake(Of IServicioDialogos)(), A.Fake(Of IClienteApiFactory)())

        vm.cmdAbrirInventario.Execute(Nothing)

        A.CallTo(Sub() _navegacion.RequestNavigate("MainRegion", "InventarioView")).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod()>
    Public Sub Remesas_AbrirExtractoCliente_AbreElExtractoDelClienteSinEspacios()
        Dim recibidos As ParametrosNavegacion = Nothing
        A.CallTo(Sub() _navegacion.RequestNavigate("MainRegion", "ExtractoClienteView", A(Of ParametrosNavegacion).Ignored)) _
            .Invokes(Sub(region As String, vista As String, p As ParametrosNavegacion) recibidos = p)
        Dim vm As New RemesasViewModel(A.Fake(Of IConfiguracion)(), A.Fake(Of IServicioDialogos)(), ServicioRemesas(), _navegacion)

        vm.AbrirExtractoCliente(New EfectoCandidatoModel With {.Cliente = " 15191 "})

        Assert.IsNotNull(recibidos)
        Assert.AreEqual("15191", recibidos.GetValue(Of String)("cliente"))
    End Sub

    <TestMethod()>
    Public Sub Remesas_AbrirExtractoClienteSinCliente_NoNavega()
        Dim vm As New RemesasViewModel(A.Fake(Of IConfiguracion)(), A.Fake(Of IServicioDialogos)(), ServicioRemesas(), _navegacion)

        vm.AbrirExtractoCliente(New EfectoCandidatoModel With {.Cliente = " "})

        A.CallTo(_navegacion).Where(Function(c) c.Method.Name = "RequestNavigate").MustNotHaveHappened()
    End Sub

    Private Shared Function ServicioRemesas() As IRemesasService
        Dim servicio = A.Fake(Of IRemesasService)()
        A.CallTo(Function() servicio.LeerFechaCargoPropuesta()).Returns(Task.FromResult(Date.Today))
        A.CallTo(Function() servicio.LeerEmpresas()).Returns(Task.FromResult(New List(Of EmpresaModel)))
        Return servicio
    End Function

End Class
