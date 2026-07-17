Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.ViewModels
Imports Prism.Services.Dialogs

<TestClass()>
Public Class RemesasViewModelTests

    Private _dialogService As IDialogService
    Private _configuracion As IConfiguracion
    Private _servicio As IRemesasService

    <TestInitialize()>
    Public Sub Initialize()
        _dialogService = A.Fake(Of IDialogService)()
        _configuracion = A.Fake(Of IConfiguracion)()
        _servicio = A.Fake(Of IRemesasService)()

        A.CallTo(Function() _servicio.LeerEmpresas()) _
            .Returns(Task.FromResult(New List(Of EmpresaModel)))
    End Sub

    Private Function CrearViewModel() As RemesasViewModel
        Return New RemesasViewModel(_configuracion, _dialogService, _servicio)
    End Function

    <TestMethod()>
    Public Async Function CargarEmpresas_PoblaLaColeccionDesdeElServicio() As Task
        ' Nesto#340 Fase 1C.14 slice 1: las empresas se leen del API, no de EF.
        Dim empresas = New List(Of EmpresaModel) From {
            New EmpresaModel With {.Numero = "1  ", .Nombre = "Nueva Visión, S.A."},
            New EmpresaModel With {.Numero = "3  ", .Nombre = "Global"}
        }
        A.CallTo(Function() _servicio.LeerEmpresas()).Returns(Task.FromResult(empresas))

        Dim vm = CrearViewModel()
        Await vm.CargarEmpresasAsync()

        Assert.AreEqual(2, vm.listaEmpresas.Count)
        Assert.AreEqual("1  ", vm.listaEmpresas.First().Numero)
        Assert.AreEqual("Global", vm.listaEmpresas.Last().Nombre)
        A.CallTo(Function() _servicio.LeerEmpresas()).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CargarEmpresas_SiElServicioFalla_NoLanzaYDejaMensajeError() As Task
        A.CallTo(Function() _servicio.LeerEmpresas()).Throws(New Exception("API caída"))

        Dim vm = CrearViewModel()
        Await vm.CargarEmpresasAsync()

        Assert.AreEqual(0, vm.listaEmpresas.Count)
        StringAssert.Contains(vm.mensajeError, "API caída")
    End Function
End Class
