Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.ViewModels
Imports Prism.Regions
Imports System.Windows
Imports Unity

<TestClass()>
Public Class MenuBarViewModelTests

    Private _container As IUnityContainer
    Private _regionManager As IRegionManager
    Private _configuracion As IConfiguracion
    Private _servicioAutenticacion As IServicioAutenticacion

    <TestInitialize()>
    Public Sub Initialize()
        _container = A.Fake(Of IUnityContainer)
        _regionManager = A.Fake(Of IRegionManager)
        _configuracion = A.Fake(Of IConfiguracion)
        _servicioAutenticacion = A.Fake(Of IServicioAutenticacion)
    End Sub

    Private Function CrearViewModel() As MenuBarViewModel
        Return New MenuBarViewModel(_container, _regionManager, _configuracion, _servicioAutenticacion)
    End Function

#Region "Inicializacion de Commands"

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_VentasEmpresasCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.VentasEmpresasCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_RapportCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.RapportCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_ClientesFichaCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.ClientesFichaCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_ControlPedidosCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.ControlPedidosCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_InventarioCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.InventarioCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_PickingCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.PickingCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_PackingCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.PackingCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_ClientesAlquileresCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.ClientesAlquileresCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_ClientesRemesasCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.ClientesRemesasCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_ClientesAgenciasCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.ClientesAgenciasCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_RatioDeudaCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.RatioDeudaCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_PrestashopCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.PrestashopCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_VideosCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.VideosCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_VendedoresComisionesCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.VendedoresComisionesCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_VendedoresClientesCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.VendedoresClientesCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_VendedoresPlanVentajasCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.VendedoresPlanVentajasCommand)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_ParametrosCommandNoEsNulo()
        Dim vm = CrearViewModel()
        Assert.IsNotNull(vm.ParametrosCommand)
    End Sub

#End Region

#Region "Valores por defecto"

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_OpcionesFechasEsActual()
        Dim vm = CrearViewModel()
        Assert.AreEqual("Actual", vm.OpcionesFechas)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_FechaInformeInicialEsHoy()
        Dim vm = CrearViewModel()
        Assert.AreEqual(Today, vm.FechaInformeInicial)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_AlCrear_FechaInformeFinalEsHoy()
        Dim vm = CrearViewModel()
        Assert.AreEqual(Today, vm.FechaInformeFinal)
    End Sub

#End Region

#Region "Visibilidad por defecto"

    <TestMethod()>
    Public Sub MenuBarViewModel_SinGrupos_VentasEmpresasVisibleEsHidden()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(False)
        Dim vm = CrearViewModel()
        Assert.AreEqual(Visibility.Hidden, vm.VentasEmpresasVisible)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_SinGrupos_RapportVisibleEsHidden()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(False)
        Dim vm = CrearViewModel()
        Assert.AreEqual(Visibility.Hidden, vm.RapportVisible)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_SinGrupos_AlmacenVisibleEsHidden()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(False)
        Dim vm = CrearViewModel()
        Assert.AreEqual(Visibility.Hidden, vm.AlmacenVisible)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_SinGrupos_VideosVisibleEsCollapsed()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(False)
        Dim vm = CrearViewModel()
        Assert.AreEqual(Visibility.Collapsed, vm.VideosVisible)
    End Sub

#End Region

#Region "Visibilidad con grupos"

    <TestMethod()>
    Public Sub MenuBarViewModel_EnGrupoDireccion_VentasEmpresasVisibleEsVisible()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)).Returns(True)
        Dim vm = CrearViewModel()
        Assert.AreEqual(Visibility.Visible, vm.VentasEmpresasVisible)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_EnGrupoDireccion_RapportVisibleEsVisible()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)).Returns(True)
        Dim vm = CrearViewModel()
        Assert.AreEqual(Visibility.Visible, vm.RapportVisible)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_EnGrupoAlmacen_AlmacenVisibleEsVisible()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(True)
        Dim vm = CrearViewModel()
        Assert.AreEqual(Visibility.Visible, vm.AlmacenVisible)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_EnGrupoTiendaOnLine_VideosVisibleEsVisible()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)).Returns(True)
        Dim vm = CrearViewModel()
        Assert.AreEqual(Visibility.Visible, vm.VideosVisible)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_NoEnGrupoAlmacen_AlmacenVisibleEsHidden()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)).Returns(True)
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(False)
        Dim vm = CrearViewModel()
        Assert.AreEqual(Visibility.Hidden, vm.AlmacenVisible)
    End Sub

#End Region

#Region "MostrarFechas"

    <TestMethod()>
    Public Sub MenuBarViewModel_OpcionesFechasActual_MostrarFechasEsHidden()
        Dim vm = CrearViewModel()
        vm.OpcionesFechas = "Actual"
        Assert.AreEqual(Visibility.Hidden, vm.MostrarFechas)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_OpcionesFechasAnterior_MostrarFechasEsHidden()
        Dim vm = CrearViewModel()
        vm.OpcionesFechas = "Anterior"
        Assert.AreEqual(Visibility.Hidden, vm.MostrarFechas)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_OpcionesFechasPersonalizar_MostrarFechasEsVisible()
        Dim vm = CrearViewModel()
        vm.OpcionesFechas = "Personalizar"
        Assert.AreEqual(Visibility.Visible, vm.MostrarFechas)
    End Sub

#End Region

#Region "ObtenerNombreVistaUnico"

    <TestMethod()>
    Public Sub MenuBarViewModel_NavegarAVistaNoRegistrada_NoLanzaExcepcion()
        ' Si la vista no esta registrada en _viewTypes, NavegarAVista simplemente retorna
        Dim vm = CrearViewModel()
        ' Ejecutar el command de Clientes sin registrar la vista no deberia lanzar excepcion
        vm.ClientesFichaCommand.Execute(Nothing)
    End Sub

#End Region

#Region "Navegacion"

    <TestMethod()>
    Public Sub MenuBarViewModel_VideosCommand_NavegarAVideosView()
        Dim vm = CrearViewModel()
        vm.VideosCommand.Execute(Nothing)
        A.CallTo(Sub() _regionManager.RequestNavigate("MainRegion", "VideosView")).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_NavegarAVistaRegistrada_ActivaVistaEnRegion()
        ' Arrange
        Dim region = A.Fake(Of IRegion)
        Dim regions = A.Fake(Of IRegionCollection)
        A.CallTo(Function() _regionManager.Regions).Returns(regions)
        A.CallTo(Function() regions("MainRegion")).Returns(region)

        Dim vm = CrearViewModel()
        vm.RegistrarTipoVista("Clientes", GetType(Object))

        ' Act
        vm.ClientesFichaCommand.Execute(Nothing)

        ' Assert - verifica que se activa una vista en la region
        A.CallTo(Sub() region.Activate(A(Of Object).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

#End Region

#Region "RegistrarTipoVista"

    <TestMethod()>
    Public Sub MenuBarViewModel_RegistrarTipoVista_PermiteNavegar()
        ' Si registramos un tipo de vista, NavegarAVista deberia intentar resolver y añadir
        Dim region = A.Fake(Of IRegion)
        Dim regions = A.Fake(Of IRegionCollection)
        A.CallTo(Function() _regionManager.Regions).Returns(regions)
        A.CallTo(Function() regions("MainRegion")).Returns(region)

        Dim vm = CrearViewModel()
        vm.RegistrarTipoVista("Comisiones", GetType(Object))

        vm.VendedoresComisionesCommand.Execute(Nothing)

        A.CallTo(Sub() region.Activate(A(Of Object).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

#End Region

#Region "Propiedades setter"

    <TestMethod()>
    Public Sub MenuBarViewModel_CambiarFechaInformeInicial_ActualizaValor()
        Dim vm = CrearViewModel()
        Dim nuevaFecha = New Date(2025, 1, 15)
        vm.FechaInformeInicial = nuevaFecha
        Assert.AreEqual(nuevaFecha, vm.FechaInformeInicial)
    End Sub

    <TestMethod()>
    Public Sub MenuBarViewModel_CambiarFechaInformeFinal_ActualizaValor()
        Dim vm = CrearViewModel()
        Dim nuevaFecha = New Date(2025, 6, 30)
        vm.FechaInformeFinal = nuevaFecha
        Assert.AreEqual(nuevaFecha, vm.FechaInformeFinal)
    End Sub

#End Region

End Class
