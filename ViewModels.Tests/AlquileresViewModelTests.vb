Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models.Alquileres
Imports Nesto.ViewModels
Imports Prism.Services.Dialogs

<TestClass()>
Public Class AlquileresViewModelTests

    Private _dialogService As IDialogService
    Private _configuracion As IConfiguracion
    Private _servicio As IProductosAlquilerService

    <TestInitialize()>
    Public Sub Initialize()
        _dialogService = A.Fake(Of IDialogService)()
        _configuracion = A.Fake(Of IConfiguracion)()
        _servicio = A.Fake(Of IProductosAlquilerService)()

        ' Por defecto el servicio devuelve lista vacía para que el constructor no explote.
        A.CallTo(Function() _servicio.LeerProductosAlquiler()) _
            .Returns(Task.FromResult(New List(Of ProductoAlquilerModel)))
    End Sub

    Private Function CrearViewModel() As AlquileresViewModel
        Return New AlquileresViewModel(_dialogService, _configuracion, _servicio)
    End Function

    <TestMethod()>
    Public Async Function CargarProductosAlquiler_PoblaLaColeccionDesdeElServicio() As Task
        ' Nesto#340 Fase 1C.1: la lista se lee del API, no de EF.
        Dim productos = New List(Of ProductoAlquilerModel) From {
            New ProductoAlquilerModel With {.Empresa = "1", .Numero = "26780", .Nombre = "Aparato X", .Stock = 10, .StockAlquileres = 3, .Diferencia = 7},
            New ProductoAlquilerModel With {.Empresa = "1", .Numero = "26781", .Nombre = "Aparato Y", .Stock = 5, .StockAlquileres = 5, .Diferencia = 0}
        }
        A.CallTo(Function() _servicio.LeerProductosAlquiler()).Returns(Task.FromResult(productos))

        Dim vm = CrearViewModel()
        ' seleccionarUltimo:=False evita disparar la query EF de CabAlquileres del setter ProductoSeleccionado.
        Await vm.CargarProductosAlquilerAsync(seleccionarUltimo:=False)

        Assert.AreEqual(2, vm.colProductosAlquilerLista.Count)
        Assert.AreEqual("26780", vm.colProductosAlquilerLista.First().Numero)
        Assert.AreEqual(7, vm.colProductosAlquilerLista.First().Diferencia)
        A.CallTo(Function() _servicio.LeerProductosAlquiler()).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CargarMovimientos_PoblaLaColeccionDesdeElServicio() As Task
        ' Nesto#340 Fase 1C.2: los movimientos se leen del API, no de EF.
        Dim movimientos = New List(Of MovimientoAlquilerModel) From {
            New MovimientoAlquilerModel With {.NumeroOrden = 1, .Producto = "26780", .Texto = "Alquiler enero", .Cantidad = 1, .Precio = 50D, .Total = 50D, .Estado = 4},
            New MovimientoAlquilerModel With {.NumeroOrden = 2, .Producto = "26780", .Texto = "Alquiler febrero", .Cantidad = 1, .Precio = 50D, .Total = 50D, .Estado = 1}
        }
        A.CallTo(Function() _servicio.LeerMovimientosAlquiler("1", 12345)).Returns(Task.FromResult(movimientos))

        Dim vm = CrearViewModel()
        Await vm.CargarMovimientosAsync("1", 12345)

        Assert.AreEqual(2, vm.colMovimientos.Count)
        Assert.AreEqual(2, vm.colMovimientos.Last().NumeroOrden)
        A.CallTo(Function() _servicio.LeerMovimientosAlquiler("1", 12345)).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CargarCompra_PoblaLaColeccionDesdeElServicio() As Task
        ' Nesto#340 Fase 1C.2: las compras se leen del API, no de EF.
        Dim compras = New List(Of CompraAlquilerModel) From {
            New CompraAlquilerModel With {.NumeroOrden = 1, .NumeroPedido = 555, .Proveedor = "PROV1", .Producto = "26780", .NumSerie = "ABC123", .Texto = "Compra aparato", .Cantidad = 1, .Precio = 300D, .Total = 363D, .Estado = 4},
            New CompraAlquilerModel With {.NumeroOrden = 2, .NumeroPedido = 556, .Proveedor = "PROV1", .Producto = "26780", .NumSerie = "ABC123", .Texto = "Reparación", .Cantidad = 1, .Precio = 50D, .Total = 60.5D, .Estado = 1}
        }
        A.CallTo(Function() _servicio.LeerComprasAlquiler("26780", "ABC123")).Returns(Task.FromResult(compras))

        Dim vm = CrearViewModel()
        Await vm.CargarCompraAsync("26780", "ABC123")

        Assert.AreEqual(2, vm.colCompra.Count)
        Assert.AreEqual(556, vm.colCompra.Last().NumeroPedido)
        A.CallTo(Function() _servicio.LeerComprasAlquiler("26780", "ABC123")).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CargarInmovilizados_PoblaLaColeccionDesdeElServicio() As Task
        ' Nesto#340 Fase 1C.2: el extracto del inmovilizado se lee del API, no de EF.
        Dim inmovilizados = New List(Of ExtractoInmovilizadoModel) From {
            New ExtractoInmovilizadoModel With {.NumeroOrden = 1, .Concepto = "Compra aparato", .NumeroDocumento = "555", .Importe = 300D, .ImportePendiente = 0D, .Estado = 4},
            New ExtractoInmovilizadoModel With {.NumeroOrden = 2, .Concepto = "Amortización", .NumeroDocumento = "A001", .Importe = -25D, .ImportePendiente = 0D, .Estado = 1}
        }
        A.CallTo(Function() _servicio.LeerInmovilizadosAlquiler("1", "ALQ000123")).Returns(Task.FromResult(inmovilizados))

        Dim vm = CrearViewModel()
        Await vm.CargarInmovilizadosAsync("1", "ALQ000123")

        Assert.AreEqual(2, vm.colExtractoInmovilizado.Count)
        Assert.AreEqual(2, vm.colExtractoInmovilizado.Last().NumeroOrden)
        A.CallTo(Function() _servicio.LeerInmovilizadosAlquiler("1", "ALQ000123")).MustHaveHappened()
    End Function

End Class
