Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.ViewModels
Imports Prism.Services.Dialogs

''' <summary>
''' NestoAPI#406: la pantalla que permite marcar qué familias se venden al público al mismo
''' precio que al profesional. Antes de esto, la única forma de tocarlo era un UPDATE en SSMS.
''' </summary>
<TestClass()>
Public Class FamiliasMantenimientoViewModelTests

    Private _servicio As IServicioFamiliasMantenimiento
    Private _configuracion As IConfiguracion
    Private _dialogService As IDialogService

    <TestInitialize()>
    Public Sub Inicializar()
        _servicio = A.Fake(Of IServicioFamiliasMantenimiento)()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogService = A.Fake(Of IDialogService)()
    End Sub

    Private Shared Function Familia(numero As String, descripcion As String, marcada As Boolean) As FamiliaMantenimiento
        Return New FamiliaMantenimiento With {
            .Empresa = "1",
            .Numero = numero,
            .Descripcion = descripcion,
            .Estado = 0,
            .PublicoIgualQueProfesional = marcada
        }
    End Function

    Private Function CrearViewModel(ParamArray familias As FamiliaMantenimiento()) As FamiliasMantenimientoViewModel
        A.CallTo(Function() _servicio.LeerFamilias(A(Of String).Ignored)).
            Returns(Task.FromResult(New List(Of FamiliaMantenimiento)(familias)))
        Return New FamiliasMantenimientoViewModel(_servicio, _configuracion, _dialogService)
    End Function

    <TestMethod()>
    Public Async Function Familias_AlCargar_TraeLasFamiliasDelServidor() As Task
        Dim vm = CrearViewModel(Familia("Staleks", "Staleks", True), Familia("Lisap", "Lisap", False))
        Await vm.CargarAsync()

        Assert.AreEqual(2, vm.Familias.Count)
        Assert.AreEqual(2, vm.FamiliasFiltradas.Count)
    End Function

    <TestMethod()>
    Public Async Function Familias_SoloSeGuardanLasQueHanCambiado() As Task
        ' Son casi 300 familias. Mandarlas todas haría que el servidor republicara el catálogo
        ' entero cada vez que alguien abre la pantalla y pulsa Guardar.
        Dim staleks = Familia("Staleks", "Staleks", False)
        Dim lisap = Familia("Lisap", "Lisap", False)
        Dim vm = CrearViewModel(staleks, lisap)
        Await vm.CargarAsync()

        vm.Familias.Single(Function(f) f.Numero = "Staleks").PublicoIgualQueProfesional = True
        Await vm.GuardarAsync()

        A.CallTo(Function() _servicio.GuardarFamilia(A(Of FamiliaMantenimiento).That.Matches(Function(f) f.Numero = "Staleks"))).
            MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.GuardarFamilia(A(Of FamiliaMantenimiento).That.Matches(Function(f) f.Numero = "Lisap"))).
            MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Familias_TrasGuardar_DejanDeEstarPendientes() As Task
        ' Si no se limpiara la marca, un segundo Guardar volvería a mandarlas y a republicar.
        Dim vm = CrearViewModel(Familia("Staleks", "Staleks", False))
        Await vm.CargarAsync()
        vm.Familias.Single().PublicoIgualQueProfesional = True

        Await vm.GuardarAsync()
        Await vm.GuardarAsync()

        A.CallTo(Function() _servicio.GuardarFamilia(A(Of FamiliaMantenimiento).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Familias_DesmarcarTambienCuentaComoCambio() As Task
        Dim vm = CrearViewModel(Familia("Staleks", "Staleks", True))
        Await vm.CargarAsync()

        vm.Familias.Single().PublicoIgualQueProfesional = False
        Await vm.GuardarAsync()

        A.CallTo(Function() _servicio.GuardarFamilia(A(Of FamiliaMantenimiento).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Familias_ElFiltroBuscaPorCodigoYPorDescripcion() As Task
        Dim vm = CrearViewModel(
            Familia("Silverfox", "Weelko", False),
            Familia("Staleks", "Staleks", False),
            Familia("Lisap", "Lisap", False))
        Await vm.CargarAsync()

        vm.Filtro = "weelko"   ' por descripción, sin distinguir mayúsculas
        Assert.AreEqual(1, vm.FamiliasFiltradas.Count)
        Assert.AreEqual("Silverfox", vm.FamiliasFiltradas.Single().Numero)

        vm.Filtro = "stal"     ' por código
        Assert.AreEqual("Staleks", vm.FamiliasFiltradas.Single().Numero)
    End Function

    <TestMethod()>
    Public Async Function Familias_SoloMarcadas_DejaFueraElResto() As Task
        Dim vm = CrearViewModel(Familia("Staleks", "Staleks", True), Familia("Lisap", "Lisap", False))
        Await vm.CargarAsync()

        vm.SoloMarcadas = True

        Assert.AreEqual(1, vm.FamiliasFiltradas.Count)
        Assert.AreEqual("Staleks", vm.FamiliasFiltradas.Single().Numero)
    End Function
End Class
