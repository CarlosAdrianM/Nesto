Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Linq
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models.PlanesVentajas
Imports Nesto.ViewModels

<TestClass()>
Public Class PlanesVentajasViewModelTests

    Private _configuracion As IConfiguracion
    Private _servicio As IPlanesVentajasService

    <TestInitialize()>
    Public Sub Initialize()
        _configuracion = A.Fake(Of IConfiguracion)()
        _servicio = A.Fake(Of IPlanesVentajasService)()

        ' Defaults para que CargarDatos no explote.
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, "EmpresaPorDefecto")).Returns(Task.FromResult("1"))
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, "RutaPlanVentajas")).Returns(Task.FromResult("C:\Planes\"))
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, "Vendedor")).Returns(Task.FromResult("AM"))
        A.CallTo(Function() _servicio.LeerEstados()).Returns(Task.FromResult(New List(Of EstadoPlanVentajasModel)))
        A.CallTo(Function() _servicio.LeerEmpresas()).Returns(Task.FromResult(New List(Of EmpresaResumenModel)))
        A.CallTo(Function() _servicio.ListarPlanes(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)) _
            .Returns(Task.FromResult(New List(Of PlanVentajasModel)))
        A.CallTo(Function() _servicio.ObtenerClientes(A(Of Integer).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(New List(Of ClientePlanVentajasModel)))
        A.CallTo(Function() _servicio.ObtenerLineasVenta(A(Of Integer).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(New List(Of LineaVentaPlanModel)))
    End Sub

    Private Function CrearViewModel() As PlanesVentajasViewModel
        Return New PlanesVentajasViewModel(_configuracion, _servicio)
    End Function

#Region "Comandos no nulos"

    <TestMethod()>
    Public Sub PlanesVentajasViewModel_AlCrear_CmdVerPlanNoEsNulo()
        Assert.IsNotNull(CrearViewModel().cmdVerPlan)
    End Sub

    <TestMethod()>
    Public Sub PlanesVentajasViewModel_AlCrear_CmdAsignarPlanNoEsNulo()
        Assert.IsNotNull(CrearViewModel().cmdAsignarPlan)
    End Sub

    <TestMethod()>
    Public Sub PlanesVentajasViewModel_AlCrear_CmdAñadirNoEsNulo()
        Assert.IsNotNull(CrearViewModel().cmdAñadir)
    End Sub

    <TestMethod()>
    Public Sub PlanesVentajasViewModel_AlCrear_CmdGuardarNoEsNulo()
        Assert.IsNotNull(CrearViewModel().cmdGuardar)
    End Sub

#End Region

#Region "CargarDatos"

    <TestMethod()>
    Public Async Function CargarDatos_LlamaAlServicioConParametrosEsperados() As Task
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, "Vendedor")).Returns(Task.FromResult("PEPE"))
        Dim vm = CrearViewModel()

        Await vm.CargarDatos()

        A.CallTo(Function() _servicio.LeerEstados()).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.LeerEmpresas()).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.ListarPlanes("PEPE", Nothing, False)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function CargarDatos_PoneVendedorYEmpresaActual() As Task
        Dim vm = CrearViewModel()

        Await vm.CargarDatos()

        Assert.AreEqual("AM", vm.vendedor)
        ' La empresa se pad-rellenan a 3 chars para coincidir con el CHAR(3) de la BD
        Assert.AreEqual("1  ", vm.empresaActual)
    End Function

    <TestMethod()>
    Public Async Function CargarDatos_SeleccionaUltimoPlanComoActual() As Task
        Dim planes As New List(Of PlanVentajasModel) From {
            New PlanVentajasModel With {.Numero = 1},
            New PlanVentajasModel With {.Numero = 2}
        }
        A.CallTo(Function() _servicio.ListarPlanes(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)) _
            .Returns(Task.FromResult(planes))
        Dim vm = CrearViewModel()

        Await vm.CargarDatos()

        Assert.IsNotNull(vm.planActual)
        Assert.AreEqual(2, vm.planActual.Numero)
    End Function

#End Region

#Region "CanGuardar / isDirty"

    <TestMethod()>
    Public Sub CanGuardar_SinPlanActual_DevuelveFalse()
        Dim vm = CrearViewModel()
        Assert.IsFalse(vm.cmdGuardar.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub CanGuardar_TrasAñadir_DevuelveTrue()
        Dim vm = CrearViewModel()
        vm.cmdAñadir.Execute(Nothing)
        Assert.IsTrue(vm.cmdGuardar.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub CanGuardar_TrasAsignarPlanExistente_DevuelveFalse()
        Dim vm = CrearViewModel()
        vm.planActual = New PlanVentajasModel With {.Numero = 5}
        Assert.IsFalse(vm.cmdGuardar.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub CanGuardar_AlCambiarPropiedadDelPlanActual_DevuelveTrue()
        Dim vm = CrearViewModel()
        vm.planActual = New PlanVentajasModel With {.Numero = 5, .Importe = 100}

        vm.planActual.Importe = 200

        Assert.IsTrue(vm.cmdGuardar.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub CanGuardar_AlAñadirClienteEditable_DevuelveTrue()
        Dim vm = CrearViewModel()
        vm.planActual = New PlanVentajasModel With {.Numero = 5}
        vm.listaClientesEditar = New ObservableCollection(Of ClienteAsignacionModel)()

        vm.listaClientesEditar.Add(New ClienteAsignacionModel With {.Cliente = "00001"})

        Assert.IsTrue(vm.cmdGuardar.CanExecute(Nothing))
    End Sub

#End Region

#Region "Añadir"

    <TestMethod()>
    Public Sub Añadir_CreaPlanConEmpresaActualYNumeroCero()
        Dim vm = CrearViewModel()
        vm.empresaActual = "1  "
        vm.listaPlanes = New ObservableCollection(Of PlanVentajasModel)()

        vm.cmdAñadir.Execute(Nothing)

        Assert.IsNotNull(vm.planActual)
        Assert.AreEqual(0, vm.planActual.Numero)
        Assert.AreEqual("1  ", vm.planActual.Empresa)
        Assert.AreEqual(Today, vm.planActual.FechaInicio)
        Assert.AreEqual(Today, vm.planActual.FechaFin)
        Assert.AreEqual(1, vm.listaPlanes.Count)
    End Sub

#End Region

#Region "Carga del plan actual"

    <TestMethod()>
    Public Async Function CargarDatosPlanActual_LlamaServicioParaClientesYLineas() As Task
        Dim vm = CrearViewModel()
        vm.empresaActual = "1  "
        vm.planActual = New PlanVentajasModel With {.Numero = 42}

        Await vm.CargarDatosPlanActualCoreAsync()

        A.CallTo(Function() _servicio.ObtenerClientes(42, "1  ")).MustHaveHappenedOnceOrMore()
        A.CallTo(Function() _servicio.ObtenerLineasVenta(42, "1  ")).MustHaveHappenedOnceOrMore()
    End Function

    <TestMethod()>
    Public Async Function CargarDatosPlanActual_ImporteVentasEsSumaDeLineas() As Task
        Dim lineas As New List(Of LineaVentaPlanModel) From {
            New LineaVentaPlanModel With {.BaseImponible = 100D},
            New LineaVentaPlanModel With {.BaseImponible = 250.5D}
        }
        A.CallTo(Function() _servicio.ObtenerLineasVenta(A(Of Integer).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(lineas))
        Dim vm = CrearViewModel()
        vm.planActual = New PlanVentajasModel With {.Numero = 42}

        Await vm.CargarDatosPlanActualCoreAsync()

        Assert.AreEqual(350.5D, vm.importeVentas)
    End Function

    <TestMethod()>
    Public Async Function CargarDatosPlanActual_RellenaListaClientesEditarConClientesDelPlan() As Task
        Dim vm = CrearViewModel()
        vm.planActual = New PlanVentajasModel With {
            .Numero = 42,
            .Clientes = New List(Of String) From {"00001", "00002"}
        }

        Await vm.CargarDatosPlanActualCoreAsync()

        Assert.AreEqual(2, vm.listaClientesEditar.Count)
        Assert.AreEqual("00001", vm.listaClientesEditar(0).Cliente)
        Assert.AreEqual("00002", vm.listaClientesEditar(1).Cliente)
    End Function

    <TestMethod()>
    Public Async Function CargarDatosPlanActual_NoMarcaDirtyAlCargar() As Task
        Dim vm = CrearViewModel()
        vm.planActual = New PlanVentajasModel With {
            .Numero = 42,
            .Clientes = New List(Of String) From {"00001"}
        }

        Await vm.CargarDatosPlanActualCoreAsync()

        Assert.IsFalse(vm.cmdGuardar.CanExecute(Nothing), "La recarga no debe marcar el plan como sucio.")
    End Function

#End Region

#Region "Guardar (POST vs PUT)"

    <TestMethod()>
    Public Async Function Guardar_PlanNumeroCero_LlamaACrearPlan() As Task
        Dim nuevoEnServidor As New PlanVentajasModel With {.Numero = 99}
        A.CallTo(Function() _servicio.CrearPlan(A(Of PlanVentajasModel).Ignored)).Returns(Task.FromResult(nuevoEnServidor))
        Dim vm = CrearViewModel()
        vm.planActual = New PlanVentajasModel With {.Numero = 0, .Empresa = "1  ", .Importe = 500D}

        Await vm.GuardarAsync()

        A.CallTo(Function() _servicio.CrearPlan(A(Of PlanVentajasModel).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.ActualizarPlan(A(Of PlanVentajasModel).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Guardar_PlanExistente_LlamaAActualizarPlan() As Task
        Dim planExistente As New PlanVentajasModel With {.Numero = 7, .Importe = 1000D}
        A.CallTo(Function() _servicio.ActualizarPlan(A(Of PlanVentajasModel).Ignored)).Returns(Task.FromResult(planExistente))
        Dim vm = CrearViewModel()
        vm.planActual = planExistente

        Await vm.GuardarAsync()

        A.CallTo(Function() _servicio.ActualizarPlan(planExistente)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.CrearPlan(A(Of PlanVentajasModel).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Guardar_SincronizaListaClientesEditarAlModelo() As Task
        Dim capturado As PlanVentajasModel = Nothing
        A.CallTo(Function() _servicio.ActualizarPlan(A(Of PlanVentajasModel).Ignored)) _
            .Invokes(Sub(llamada) capturado = CType(llamada.Arguments(0), PlanVentajasModel)) _
            .Returns(Task.FromResult(New PlanVentajasModel With {.Numero = 7}))
        Dim vm = CrearViewModel()
        vm.planActual = New PlanVentajasModel With {.Numero = 7}
        vm.listaClientesEditar = New ObservableCollection(Of ClienteAsignacionModel) From {
            New ClienteAsignacionModel With {.Cliente = "00001"},
            New ClienteAsignacionModel With {.Cliente = "00002"},
            New ClienteAsignacionModel With {.Cliente = "  "}, ' se descarta
            New ClienteAsignacionModel With {.Cliente = "00001"}  ' duplicado, se descarta
        }

        Await vm.GuardarAsync()

        Assert.IsNotNull(capturado)
        CollectionAssert.AreEqual(New String() {"00001", "00002"}, capturado.Clientes.ToArray())
    End Function

    <TestMethod()>
    Public Async Function Guardar_RecargaListaTrasGuardar() As Task
        Dim guardado As New PlanVentajasModel With {.Numero = 33}
        A.CallTo(Function() _servicio.ActualizarPlan(A(Of PlanVentajasModel).Ignored)).Returns(Task.FromResult(guardado))
        A.CallTo(Function() _servicio.ListarPlanes(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)) _
            .Returns(Task.FromResult(New List(Of PlanVentajasModel) From {guardado}))
        Dim vm = CrearViewModel()
        vm.planActual = New PlanVentajasModel With {.Numero = 33}

        Await vm.GuardarAsync()

        A.CallTo(Function() _servicio.ListarPlanes(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)) _
            .MustHaveHappened()
    End Function

#End Region

#Region "Filtros del listado"

    <TestMethod()>
    Public Async Function RecargarLista_PasaIncluirCanceladosSegunVerPlanesNulos() As Task
        Dim vm = CrearViewModel()
        vm.vendedor = "AM"
        vm.verPlanesNulos = True

        Await vm.RecargarListaPlanesAsync()

        A.CallTo(Function() _servicio.ListarPlanes("AM", Nothing, True)).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function RecargarLista_PasaFiltroClienteCuandoFiltroNoVacio() As Task
        Dim vm = CrearViewModel()
        vm.vendedor = "AM"
        vm.filtro = "  00123  "

        Await vm.RecargarListaPlanesAsync()

        A.CallTo(Function() _servicio.ListarPlanes("AM", "00123", False)).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function RecargarLista_FiltroEnBlanco_NoPasaFiltroCliente() As Task
        Dim vm = CrearViewModel()
        vm.vendedor = "AM"
        vm.filtro = "   "

        Await vm.RecargarListaPlanesAsync()

        A.CallTo(Function() _servicio.ListarPlanes("AM", Nothing, False)).MustHaveHappened()
    End Function

#End Region

End Class
