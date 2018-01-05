Imports Nesto.ViewModels
Imports Microsoft.Practices.Prism.Regions
Imports FakeItEasy
Imports Microsoft.Practices.Unity
Imports System.Windows.Controls
Imports Nesto.Models
Imports System.ComponentModel

<TestClass()>
Public Class AgenciaViewModelTests
    Private viewModel As AgenciasViewModel

    '''<summary>
    '''Obtiene o establece el contexto de las pruebas que proporciona
    '''información y funcionalidad para la serie de pruebas actual.
    '''</summary>
    Private container As IUnityContainer
    Private regionManager As IRegionManager
    Private servicio As IAgenciaService
    Private context As NestoEntities

#Region "Atributos de prueba adicionales"
    '
    ' Puede usar los siguientes atributos adicionales conforme escribe las pruebas:
    '
    ' Use ClassInitialize para ejecutar el código antes de ejecutar la primera prueba en la clase
    ' <ClassInitialize()> Public Shared Sub MyClassInitialize(ByVal testContext As TestContext)
    ' End Sub
    '
    ' Use ClassCleanup para ejecutar el código después de haberse ejecutado todas las pruebas en una clase
    ' <ClassCleanup()> Public Shared Sub MyClassCleanup()
    ' End Sub
    '
    ' Usar TestInitialize para ejecutar el código antes de ejecutar cada prueba
    ' <TestInitialize()> Public Sub MyTestInitialize()
    ' End Sub
    '
    ' Use TestCleanup para ejecutar el código una vez ejecutadas todas las pruebas
    ' <TestCleanup()> Public Sub MyTestCleanup()
    ' End Sub
    '
#End Region
    <TestInitialize()>
    Public Sub Initialize()
        container = A.Fake(Of IUnityContainer)
        regionManager = A.Fake(Of RegionManager)
        servicio = A.Fake(Of IAgenciaService)
        context = A.Fake(Of NestoEntities)
        viewModel = Nothing
    End Sub

    <TestMethod()>
    Public Sub AgenciaViewModel_AlCalcularElDigitoDeControl_DevuelveElDigitoControlCorrecto()
        'arrange
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()
        'act

        'assert
        Assert.IsTrue(viewModel.calcularDigitoControl("21005190520534001") = 2)
        Assert.IsTrue(viewModel.calcularDigitoControl("61771001461814001") = 0)
        Assert.IsTrue(viewModel.calcularDigitoControl("61771001461814002") = 7)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlSeleccionarTabPendientes_ListaPendientesNoPuedeSerNulo()
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()
        Assert.IsNotNull(viewModel.listaPendientes)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlSeleccionarTabPendientes_ElContextoPendientesNoPuedeSerNulo()
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()

        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.IsNotNull(viewModel.contextPendientes)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEtiquetasPendientesAlSeleccionarLaTabPendientes_ListaPendientesTieneAlgunRegistro()
        CrearViewModelConUnEnvioEnLaLista()

        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.AreEqual(1, viewModel.listaPendientes.Count)
        A.CallTo(Function() servicio.CargarListaPendientes(A(Of NestoEntities).Ignored)).MustHaveHappened(Repeated.Exactly.Once)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEtiquetasPendientesAlSeleccionarLaTabPendientes_EnvioPendienteSeleccionadoNoEsNull()
        CrearViewModelConUnEnvioEnLaLista()

        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.IsNotNull(viewModel.EnvioPendienteSeleccionado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEtiquetasPendientesAntesDeSeleccionarLaTabPendientes_ListaPendientesEstaVacia()
        Dim envio = A.Fake(Of EnviosAgencia)
        Dim lista = New List(Of EnviosAgencia) From {
            envio
        }
        A.CallTo(Function() servicio.CargarListaPendientes(context)).Returns(lista)
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()

        Assert.AreEqual(0, viewModel.listaPendientes.Count)
    End Sub

    ' seleccionar pestaña pendientes se desactiva la combo de agencias, porque tratamos todos juntos

    <TestMethod>
    Public Sub AgenciaViewModel_SiNoHayEnvioSeleccionado_ElBotonGuardarEstaInactivo()
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()
        viewModel.EnvioPendienteSeleccionado = Nothing

        Assert.IsFalse(viewModel.BorrarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEnvioSeleccionado_ElBotonGuardarEstaActivo()
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()
        viewModel.EnvioPendienteSeleccionado = A.Fake(Of EnviosAgencia)

        Assert.IsTrue(viewModel.BorrarEnvioPendienteCommand.CanExecute)
    End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_SiNoHayCambiosSinGuardar_ElBotonGuardarEstaInactivo()
    '    CrearViewModelConUnEnvioEnLaLista()
    '    viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

    '    Assert.IsFalse(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    'End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_SiHayCambiosSinGuardar_ElBotonGuardarEstaActivo()
    '    CrearViewModelConUnEnvioEnLaLista()
    '    viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

    '    Assert.IsTrue(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    'End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_AlSeleccionarLaTabPendientes_ElBotonInsertarEstaActivo()
    '    CrearViewModelConUnEnvioEnLaLista()

    '    viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

    '    Assert.IsTrue(viewModel.InsertarEnvioPendienteCommand.CanExecute)
    'End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_SiHayCambiosSinGuardar_ElBotonInsertarEstaInactivo()
    '    CrearViewModelConUnEnvioEnLaLista()
    '    viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

    '    viewModel.EnvioPendienteSeleccionado.Nombre = "Carlos"

    '    Assert.IsFalse(viewModel.InsertarEnvioPendienteCommand.CanExecute)
    'End Sub

    ' si se modifica alguna propiedad de EnvioPendienteSeleccionado hay que raise el guardar e insertar 

    <TestMethod>
    Public Sub AgenciasViewModel_AlInsertar_TenemosUnEnvioMas()
        CrearViewModelConUnEnvioEnLaLista()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.AreEqual(2, viewModel.listaPendientes.Count)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlInsertar_ElNuevoEnvioEstaEnPendientes()
        CrearViewModelConUnEnvioEnLaLista()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.AreEqual(CType(-1, Short), viewModel.EnvioPendienteSeleccionado.Estado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlInsertar_HayUnaLlamadaPropertyChangeDeInsertar()
        CrearViewModelConUnEnvioEnLaLista()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        Dim ejecutado As Boolean = False
        Dim seHaEjecutado = Sub()
                                ejecutado = True
                            End Sub
        AddHandler viewModel.InsertarEnvioPendienteCommand.CanExecuteChanged, seHaEjecutado

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.IsTrue(ejecutado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_CuandoNoHayEnvioPendienteSeleccionado_LosCamposDeLaTabPendientesEstanInactivos()
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.EnvioPendienteSeleccionado = Nothing

        Assert.IsFalse(viewModel.HayUnEnvioPendienteSeleccionado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_CuandoCambiaEnvioPendienteSeleccionado_SeActualizaHayUnEnvioPendienteSeleccionado()
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()
        Dim ejecutado As Boolean = False
        Dim seHaEjecutado = Sub(s, e)
                                If e.PropertyName = "HayUnEnvioPendienteSeleccionado" Then
                                    ejecutado = True
                                End If
                            End Sub
        AddHandler viewModel.PropertyChanged, seHaEjecutado
        viewModel.EnvioPendienteSeleccionado = Nothing

        Assert.IsTrue(ejecutado)
    End Sub

    ' Otra forma más elegante de hacer lo mismo que en AgenciaViewModel_AlInsertar_HayUnaLlamadaPropertyChangeDeInsertar
    <TestMethod>
    Public Sub AgenciaViewModel_AlInsertar_HayUnaLlamadaPropertyChangeDeBorrar()
        CrearViewModelConUnEnvioEnLaLista()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        Dim handler = A.Fake(Of EventHandler)
        AddHandler viewModel.BorrarEnvioPendienteCommand.CanExecuteChanged, handler

        viewModel.InsertarEnvioPendienteCommand.Execute()

        A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappened(Repeated.Exactly.Once)
    End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_AlModificarAlgunaPropiedadDelEnvioPendienteSeleccionado_HayUnaLlamadaPropertyChangeDeBorrar()
    '    CrearViewModelConUnEnvioEnLaLista()
    '    viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
    '    Dim handler = A.Fake(Of PropertyChangedEventHandler)
    '    AddHandler viewModel.EnvioPendienteSeleccionado.PropertyChanged, handler

    '    viewModel.EnvioPendienteSeleccionado.Nombre = "Carlos"
    '    viewModel.EnvioPendienteSeleccionado.Fecha = New DateTime(2017, 10, 26)
    '    viewModel.EnvioPendienteSeleccionado.Horario = 2

    '    A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappened(Repeated.Exactly.Times(3))
    'End Sub


    '<TestMethod>
    'Public Sub AgenciaViewModel_AlBorrar_HayUnaLlamadaPropertyChangeDeInsertar()
    '    CrearViewModelConUnEnvioEnLaLista()
    '    viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
    '    Dim handler = A.Fake(Of EventHandler)
    '    AddHandler viewModel.InsertarEnvioPendienteCommand.CanExecuteChanged, handler

    '    viewModel.BorrarEnvioPendienteCommand.Execute()

    '    A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappened(Repeated.Exactly.Once)
    'End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_AlBorrar_HayUnaLlamadaPropertyChangeDeBorrar()
    '    CrearViewModelConUnEnvioEnLaLista()
    '    viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
    '    Dim handler = A.Fake(Of EventHandler)
    '    AddHandler viewModel.BorrarEnvioPendienteCommand.CanExecuteChanged, handler

    '    viewModel.BorrarEnvioPendienteCommand.Execute()

    '    A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappened(Repeated.Exactly.Once)
    'End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_AlBorrar_TenemosUnEnvioMenos()
    '    CrearViewModelConUnEnvioEnLaLista()
    '    viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

    '    viewModel.BorrarEnvioPendienteCommand.Execute()

    '    Assert.AreEqual(0, viewModel.listaPendientes.Count)
    'End Sub


    ' al imprimir etiqueta busca si hay etiqueta pendiente antes de crear una nueva

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayBorrarEnvioPendienteNoQuedanMas_EnvioPendienteSeleccionadoTieneQueSerNulo()
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.InsertarEnvioPendienteCommand.Execute()
        viewModel.BorrarEnvioPendienteCommand.Execute()

        Assert.IsNull(viewModel.EnvioPendienteSeleccionado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayUnEnvioPendienteSeleccionado_BorrarCommandEstaActivo()
        CrearViewModelConUnEnvioEnLaLista()
        Dim ejecutado As Boolean = False
        Dim seHaEjecutado = Sub()
                                ejecutado = True
                            End Sub
        AddHandler viewModel.BorrarEnvioPendienteCommand.CanExecuteChanged, seHaEjecutado
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.IsTrue(viewModel.BorrarEnvioPendienteCommand.CanExecute)
        Assert.IsTrue(ejecutado)
    End Sub



    Private Sub CrearViewModelConUnEnvioEnLaLista()
        Dim envio = A.Fake(Of EnviosAgencia)
        Dim lista = New List(Of EnviosAgencia) From {
            envio
        }
        A.CallTo(Function() servicio.CargarListaPendientes(A(Of NestoEntities).Ignored)).Returns(lista)
        viewModel = New AgenciasViewModel(container, regionManager, servicio)
        viewModel.cmdCargarDatos.Execute()
    End Sub


End Class

