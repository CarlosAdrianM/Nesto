Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports FakeItEasy
Imports Newtonsoft.Json
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' 25/11/26: la lista «Completo» de Inventario Tienda trae al final las filas pendientes de días anteriores
''' (incluirPendientesAnteriores=true) para poder borrarlas; se marcan con su día. Con la API antigua, todo como antes.
''' </summary>
<TestClass()>
Public Class InventarioPendientesDiasAnterioresTests

    Private Function CrearViewModel() As InventarioViewModel
        Return New InventarioViewModel(A.Fake(Of IServicioNavegacion)(), A.Fake(Of IConfiguracion)(), A.Fake(Of IServicioDialogos)(), A.Fake(Of IServicioInventario)())
    End Function

    <TestMethod()>
    Public Sub UrlLeerLineas_PideLasPendientesDeDiasAnteriores()
        Dim url = ServicioInventario.UrlLeerLineas("1", "ALC", New Date(2026, 11, 25))
        StringAssert.Contains(url, "Inventarios?empresa=1&almacen=ALC&fecha=")
        StringAssert.EndsWith(url, "&incluirPendientesAnteriores=true")
    End Sub

    <TestMethod()>
    Public Sub Json_LeePendienteDeDiaAnterior_YSinElCampoEsFalse()
        Dim filas = JsonConvert.DeserializeObject(Of List(Of InventarioDTO))(
            "[{""NºOrden"":1,""Fecha"":""2026-11-25T00:00:00""},{""NºOrden"":2,""Fecha"":""2026-11-24T00:00:00"",""PendienteDeDiaAnterior"":true,""SePuedeBorrar"":true}]")
        Assert.IsFalse(filas(0).PendienteDeDiaAnterior)
        Assert.AreEqual(String.Empty, filas(0).TextoDia)
        Assert.IsTrue(filas(1).PendienteDeDiaAnterior)
        Assert.AreEqual("pendiente del 24/11", filas(1).TextoDia)
        Assert.IsTrue(filas(1).SePuedeBorrar)
    End Sub

    <TestMethod()>
    Public Sub Json_NoSeMandaPendienteDeDiaAnterior()
        Dim json = JsonConvert.SerializeObject(New InventarioDTO With {.PendienteDeDiaAnterior = True})
        Assert.IsFalse(json.Contains("PendienteDeDiaAnterior"))
        Assert.IsFalse(json.Contains("TextoDia"))
    End Sub

    <TestMethod()>
    Public Sub ConPendientesAnteriores_SeAvisaDeCuantasSon()
        Dim vm = CrearViewModel()
        vm.movimientosTotal = New ObservableCollection(Of InventarioDTO) From {
            New InventarioDTO With {.NumOrden = 1},
            New InventarioDTO With {.NumOrden = 2, .PendienteDeDiaAnterior = True},
            New InventarioDTO With {.NumOrden = 3, .PendienteDeDiaAnterior = True}
        }
        Assert.IsTrue(vm.HayPendientesDiasAnteriores)
        StringAssert.Contains(vm.TextoPendientesDiasAnteriores, "Al final salen 2 filas pendientes de días anteriores")
    End Sub

    <TestMethod()>
    Public Sub UnaPendienteAnterior_EnSingular()
        StringAssert.Contains(InventarioViewModel.TextoPendientesAnteriores(New List(Of InventarioDTO) From {
            New InventarioDTO With {.PendienteDeDiaAnterior = True}}), "Al final sale 1 fila pendiente de un día anterior")
    End Sub

    <TestMethod()>
    Public Sub SinPendientesAnteriores_ComoAntes()
        Dim vm = CrearViewModel()
        vm.movimientosTotal = New ObservableCollection(Of InventarioDTO) From {New InventarioDTO With {.NumOrden = 1}}
        Assert.IsFalse(vm.HayPendientesDiasAnteriores)
        Assert.AreEqual(String.Empty, vm.TextoPendientesDiasAnteriores)
        Assert.AreEqual(String.Empty, InventarioViewModel.TextoPendientesAnteriores(Nothing))
    End Sub
End Class
