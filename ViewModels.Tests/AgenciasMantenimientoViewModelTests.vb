Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.ViewModels
Imports Prism.Services.Dialogs

''' <summary>
''' Nesto#340: ViewModel de mantenimiento de agencias (alta/edición, fuel y cuarentena).
''' </summary>
<TestClass()>
Public Class AgenciasMantenimientoViewModelTests

    Private servicio As IServicioAgenciasMantenimiento
    Private configuracion As IConfiguracion
    Private dialogService As IDialogService

    <TestInitialize()>
    Public Sub Init()
        servicio = A.Fake(Of IServicioAgenciasMantenimiento)
        configuracion = A.Fake(Of IConfiguracion)
        dialogService = A.Fake(Of IDialogService)
        A.CallTo(Function() servicio.LeerAgencias()).Returns(Task.FromResult(New List(Of AgenciaMantenimiento)()))
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, A(Of String).Ignored)).Returns(Task.FromResult(""))
    End Sub

    Private Function CrearVm() As AgenciasMantenimientoViewModel
        Return New AgenciasMantenimientoViewModel(servicio, configuracion, dialogService)
    End Function

    <TestMethod()>
    Public Async Function CargarAsync_LlenaAgenciasYMarcaCuarentenaPorNombre() As Task
        Dim lista = New List(Of AgenciaMantenimiento) From {
            New AgenciaMantenimiento With {.Numero = 1, .Nombre = "GLS", .RecargoCombustible = 0.1055D},
            New AgenciaMantenimiento With {.Numero = 10, .Nombre = "Sending"}
        }
        A.CallTo(Function() servicio.LeerAgencias()).Returns(Task.FromResult(lista))
        A.CallTo(Function() configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.AgenciasEnCuarentena)).Returns(Task.FromResult("Sending"))
        Dim vm = CrearVm()

        Await vm.CargarAsync()

        Assert.AreEqual(2, vm.Agencias.Count)
        Assert.AreEqual(10.55D, vm.Agencias.Single(Function(a) a.Nombre = "GLS").PorcentajeFuel)
        Assert.IsFalse(vm.Agencias.Single(Function(a) a.Nombre = "GLS").EnCuarentena)
        Assert.IsTrue(vm.Agencias.Single(Function(a) a.Nombre = "Sending").EnCuarentena)
    End Function

    <TestMethod()>
    Public Async Function GuardarAsync_CreaLasNuevasYActualizaLasExistentes() As Task
        Dim existente = New AgenciaMantenimiento With {.Numero = 1, .Nombre = "GLS", .EsNueva = False}
        A.CallTo(Function() servicio.LeerAgencias()).Returns(Task.FromResult(New List(Of AgenciaMantenimiento) From {existente}))
        Dim vm = CrearVm()
        Await vm.CargarAsync()
        Dim nueva = New AgenciaMantenimiento With {.Numero = 12, .Nombre = "Innovatrans", .EsNueva = True}
        vm.Agencias.Add(nueva)

        Await vm.GuardarAsync()

        A.CallTo(Function() servicio.CrearAgencia(nueva)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.GuardarAgencia(existente)).MustHaveHappenedOnceExactly()
        Assert.IsFalse(nueva.EsNueva, "Tras crearla deja de ser nueva")
    End Function

    <TestMethod()>
    Public Async Function GuardarAsync_GuardaElParametroDeCuarentenaConLosNombresMarcados() As Task
        Dim lista = New List(Of AgenciaMantenimiento) From {
            New AgenciaMantenimiento With {.Numero = 8, .Nombre = "Correos Express"},
            New AgenciaMantenimiento With {.Numero = 1, .Nombre = "GLS"}
        }
        A.CallTo(Function() servicio.LeerAgencias()).Returns(Task.FromResult(lista))
        Dim vm = CrearVm()
        Await vm.CargarAsync()
        ' El usuario marca la casilla de cuarentena de Correos Express.
        vm.Agencias.Single(Function(a) a.Nombre = "Correos Express").EnCuarentena = True

        Await vm.GuardarAsync()

        A.CallTo(Function() configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AgenciasEnCuarentena, "Correos Express")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function GuardarAsync_ExpandeLaCuentaDeReembolsosEnFormatoPunto() As Task
        Dim agencia = New AgenciaMantenimiento With {.Numero = 12, .Nombre = "Innovatrans", .CuentaReembolsos = "555.20", .EsNueva = True}
        A.CallTo(Function() servicio.LeerAgencias()).Returns(Task.FromResult(New List(Of AgenciaMantenimiento) From {agencia}))
        Dim vm = CrearVm()
        Await vm.CargarAsync()

        Await vm.GuardarAsync()

        Assert.AreEqual("55500020", agencia.CuentaReembolsos)
        A.CallTo(Function() servicio.CrearAgencia(A(Of AgenciaMantenimiento).That.Matches(Function(a) a.CuentaReembolsos = "55500020"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function NuevaAgencia_AnadeFilaConElSiguienteNumeroYMarcadaComoNueva() As Task
        A.CallTo(Function() servicio.LeerAgencias()).Returns(Task.FromResult(New List(Of AgenciaMantenimiento) From {
            New AgenciaMantenimiento With {.Numero = 11, .Nombre = "Canteras"}
        }))
        Dim vm = CrearVm()
        Await vm.CargarAsync()

        vm.NuevaAgenciaCommand.Execute()

        Dim nueva = vm.Agencias.Last()
        Assert.AreEqual(12, nueva.Numero)
        Assert.IsTrue(nueva.EsNueva)
    End Function

End Class
