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

    <TestMethod()>
    Public Async Function CargarRemesas_PoblaElGridYSeleccionaLaPrimera() As Task
        ' Nesto#340 Fase 1C.14 slice 2: las remesas se leen del API, no de EF.
        Dim remesas = New List(Of RemesaModel) From {
            New RemesaModel With {.Numero = 10897, .Importe = 10546.66D, .Banco = "5"},
            New RemesaModel With {.Numero = 10896, .Importe = 9420.99D, .Banco = "5"}
        }
        A.CallTo(Function() _servicio.LeerRemesas(A(Of String).Ignored, 100)).Returns(Task.FromResult(remesas))

        Dim vm = CrearViewModel()
        Await vm.CargarRemesasAsync(100)

        Assert.AreEqual(2, vm.listaRemesas.Count)
        Assert.AreEqual(10897, vm.remesaActual.Numero)
    End Function

    <TestMethod()>
    Public Async Function CargarRemesas_SinTop_PideTodasAlServicio() As Task
        ' El botón "Ver Todas" carga sin límite: el servicio debe recibir top = Nothing.
        A.CallTo(Function() _servicio.LeerRemesas(A(Of String).Ignored, Nothing)) _
            .Returns(Task.FromResult(New List(Of RemesaModel)))

        Dim vm = CrearViewModel()
        Await vm.CargarRemesasAsync(Nothing)

        A.CallTo(Function() _servicio.LeerRemesas(A(Of String).Ignored, Nothing)).MustHaveHappenedOnceExactly()
        Assert.IsNull(vm.remesaActual)
    End Function

    <TestMethod()>
    Public Async Function CargarMovimientos_PoblaElGridDeEfectos() As Task
        ' Nesto#340 Fase 1C.14 slice 3: los efectos de la remesa se leen del API, no de EF.
        Dim movimientos = New List(Of MovimientoRemesaModel) From {
            New MovimientoRemesaModel With {.Id = 1, .Número = "15191", .Contacto = "0", .Importe = 250.5D},
            New MovimientoRemesaModel With {.Id = 2, .Número = "26985", .Contacto = "0", .Importe = 100D}
        }
        A.CallTo(Function() _servicio.LeerMovimientos(A(Of String).Ignored, 10897)).Returns(Task.FromResult(movimientos))

        Dim vm = CrearViewModel()
        Await vm.CargarMovimientosAsync(10897)

        Assert.AreEqual(2, vm.listaMovimientos.Count)
        Assert.AreEqual("15191", vm.listaMovimientos.First().Número)
        Assert.AreEqual(250.5D, vm.listaMovimientos.First().Importe)
    End Function

    <TestMethod()>
    Public Async Function CargarMovimientos_SiElServicioFalla_DejaElGridVacioYAvisa() As Task
        A.CallTo(Function() _servicio.LeerMovimientos(A(Of String).Ignored, A(Of Integer).Ignored)) _
            .Throws(New Exception("API caída"))

        Dim vm = CrearViewModel()
        Await vm.CargarMovimientosAsync(10897)

        Assert.AreEqual(0, vm.listaMovimientos.Count, "El grid no puede quedarse con los datos de la remesa anterior")
        StringAssert.Contains(vm.mensajeError, "API caída")
    End Function

    <TestMethod()>
    Public Async Function CargarRemesas_SiElServicioFalla_NoLanzaYDejaMensajeError() As Task
        A.CallTo(Function() _servicio.LeerRemesas(A(Of String).Ignored, A(Of Integer?).Ignored)) _
            .Throws(New Exception("API caída"))

        Dim vm = CrearViewModel()
        Await vm.CargarRemesasAsync(100)

        StringAssert.Contains(vm.mensajeError, "API caída")
    End Function
End Class
