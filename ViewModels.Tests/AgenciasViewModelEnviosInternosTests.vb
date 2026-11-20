Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels

''' <summary>Sugerencia 1277 (Carlos 20/11/26): el botón «Envíos internos» de Agencias, solo para Almacén, Dirección e Informática.</summary>
<TestClass()>
Public Class AgenciasViewModelEnviosInternosTests

    Private Shared Function Crear(grupo As String) As AgenciasViewModel
        Dim configuracion = A.Fake(Of IConfiguracion)
        If grupo IsNot Nothing Then
            A.CallTo(Function() configuracion.UsuarioEnGrupo(grupo)).Returns(True)
        End If
        Return New AgenciasViewModel(A.Fake(Of IAgenciaService), configuracion, A.Fake(Of IServicioDialogos),
                                     A.Fake(Of IPedidoVentaService), A.Fake(Of IServicioAutenticacion))
    End Function

    <TestMethod()>
    Public Sub EnviosInternos_AlmacenDireccionEInformatica()
        For Each grupo In {Constantes.GruposSeguridad.ALMACEN, Constantes.GruposSeguridad.DIRECCION, Constantes.GruposSeguridad.INFORMATICA}
            Dim vm = Crear(grupo)
            Assert.IsTrue(vm.PuedeUsarEnviosInternos, grupo)
            Assert.IsTrue(vm.AbrirEnviosInternosCommand.CanExecute(Nothing), grupo)
        Next
    End Sub

    <TestMethod()>
    Public Sub EnviosInternos_LosDemasNoLoVen()
        For Each grupo In {Constantes.GruposSeguridad.ADMINISTRACION, Constantes.GruposSeguridad.TIENDAS, Nothing}
            Dim vm = Crear(grupo)
            Assert.IsFalse(vm.PuedeUsarEnviosInternos, If(grupo, "sin grupo"))
            Assert.IsFalse(vm.AbrirEnviosInternosCommand.CanExecute(Nothing), If(grupo, "sin grupo"))
        Next
    End Sub
End Class
