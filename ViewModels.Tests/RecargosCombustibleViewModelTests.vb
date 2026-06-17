Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.ViewModels
Imports Prism.Services.Dialogs

''' <summary>
''' Nesto#340: ViewModel de mantenimiento del recargo de combustible por agencia.
''' </summary>
<TestClass()>
Public Class RecargosCombustibleViewModelTests

    Private servicio As IServicioRecargosCombustible
    Private dialogService As IDialogService

    <TestInitialize()>
    Public Sub Init()
        servicio = A.Fake(Of IServicioRecargosCombustible)
        dialogService = A.Fake(Of IDialogService)
        A.CallTo(Function() servicio.LeerRecargos()).Returns(Task.FromResult(New List(Of RecargoCombustibleAgencia)()))
    End Sub

    <TestMethod()>
    Public Async Function CargarAsync_LlenaAgenciasYExponeElPorcentaje() As Task
        Dim lista = New List(Of RecargoCombustibleAgencia) From {
            New RecargoCombustibleAgencia With {.Numero = 1, .Nombre = "GLS", .RecargoCombustible = 0.1055D}
        }
        A.CallTo(Function() servicio.LeerRecargos()).Returns(Task.FromResult(lista))
        Dim vm = New RecargosCombustibleViewModel(servicio, dialogService)

        Await vm.CargarAsync()

        Assert.AreEqual(1, vm.Agencias.Count)
        Assert.AreEqual(10.55D, vm.Agencias(0).PorcentajeFuel, "El usuario edita el % (10,55) sobre la fracción 0,1055")
    End Function

    <TestMethod()>
    Public Async Function GuardarAsync_GuardaElRecargoDeCadaAgencia() As Task
        Dim lista = New List(Of RecargoCombustibleAgencia) From {
            New RecargoCombustibleAgencia With {.Numero = 1, .RecargoCombustible = 0.1D},
            New RecargoCombustibleAgencia With {.Numero = 8, .RecargoCombustible = 0.05D}
        }
        A.CallTo(Function() servicio.LeerRecargos()).Returns(Task.FromResult(lista))
        Dim vm = New RecargosCombustibleViewModel(servicio, dialogService)
        Await vm.CargarAsync()

        Await vm.GuardarAsync()

        A.CallTo(Function() servicio.GuardarRecargo(1, 0.1D)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.GuardarRecargo(8, 0.05D)).MustHaveHappenedOnceExactly()
    End Function

End Class
