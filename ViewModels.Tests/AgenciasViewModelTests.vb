Imports Nesto.ViewModels
Imports Microsoft.Practices.Prism.Regions
Imports FakeItEasy
Imports Microsoft.Practices.Unity
Imports System.Windows.Controls
Imports Nesto.Models
Imports System.ComponentModel
Imports Nesto.Contratos
Imports System.Collections.ObjectModel

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
    Private configuracion As IConfiguracion

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
        configuracion = A.Fake(Of IConfiguracion)
        container = A.Fake(Of IUnityContainer)
        regionManager = A.Fake(Of RegionManager)
        servicio = A.Fake(Of IAgenciaService)
        viewModel = Nothing
    End Sub

    'TODO:
    ' Test: si en la ventana pedidos hay un envío ya de ese pedido y lo tenemos seleccionado, al cambiar de agencia o empresa, tiene que coger el país del envío.
    ' Test: si en pendientes cambiamos la empresa, se quedan las combos sin actualizar. Probar si retornoActual, servicioActual y horarioActual son nulos.


    <TestMethod()>
    Public Sub AgenciaViewModel_AlCargarDatos_HayUnaEmpresaSeleccionada()
        'arrange
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim pedido = New CabPedidoVta With {
            .Empresa = "1  ",
            .Número = 1,
            .Clientes = New Clientes()
        }
        A.CallTo(Function() servicio.CargarPedidoPorFactura(A(Of String).Ignored)).Returns(pedido)
        Dim agencia = A.Fake(Of AgenciasTransporte)
        agencia.Empresa = "1  "
        agencia.Numero = 2
        agencia.Ruta = "XXX"
        A.CallTo(Function() servicio.CargarAgenciaPorRuta(A(Of String).Ignored, A(Of String).Ignored)).Returns(agencia)
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)

        'act
        viewModel.cmdCargarDatos.Execute()

        'assert
        Assert.IsNotNull(viewModel.empresaSeleccionada)
        Assert.AreEqual("1  ", viewModel.empresaSeleccionada.Número)
    End Sub

    <TestMethod()>
    Public Sub AgenciaViewModel_AlCargarDatos_HayUnaAgenciaSeleccionada()
        'arrange
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim pedido = New CabPedidoVta With {
            .Empresa = "1  ",
            .Número = 1,
            .Clientes = New Clientes()
        }
        A.CallTo(Function() servicio.CargarPedidoPorFactura(A(Of String).Ignored)).Returns(pedido)
        Dim agencia = A.Fake(Of AgenciasTransporte)
        agencia.Empresa = "1  "
        agencia.Numero = 2
        agencia.Ruta = "XXX"
        A.CallTo(Function() servicio.CargarAgenciaPorRuta(A(Of String).Ignored, A(Of String).Ignored)).Returns(agencia)
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia})
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)

        'act
        viewModel.cmdCargarDatos.Execute()

        'assert
        Assert.IsNotNull(viewModel.agenciaSeleccionada)
        Assert.AreEqual(2, viewModel.agenciaSeleccionada.Numero)
    End Sub

    <TestMethod()>
    Public Sub AgenciaViewModel_AlCargarDatos_SoloCargaLosDatosUnaVez()
        'arrange
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("12345     ")
        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1  "
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
        Dim agencia = New AgenciasTransporte With {
            .Empresa = "1  ",
            .Numero = 2,
            .Ruta = "XXX"
        }
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia})
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim pedido = New CabPedidoVta With {
            .Empresa = "1  ",
            .Número = 12345,
            .Clientes = New Clientes()
        }
        A.CallTo(Function() servicio.CargarPedidoPorNumero(A(Of Integer).Ignored)).Returns(pedido)
        A.CallTo(Function() servicio.CargarAgenciaPorRuta(A(Of String).Ignored, A(Of String).Ignored)).Returns(agencia)
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)

        'act
        viewModel.cmdCargarDatos.Execute()

        'assert
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).MustHaveHappenedOnceExactly
        A.CallTo(Function() servicio.CargarListaEmpresas()).MustHaveHappenedOnceExactly
        A.CallTo(Function() servicio.CargarListaEnvios(A(Of Integer).Ignored)).MustHaveHappenedOnceExactly
    End Sub

    <TestMethod()>
    Public Sub AgenciaViewModel_AlCalcularElDigitoDeControl_DevuelveElDigitoControlCorrecto()
        'arrange
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()
        'act

        'assert
        Assert.IsTrue(viewModel.calcularDigitoControl("21005190520534001") = 2)
        Assert.IsTrue(viewModel.calcularDigitoControl("61771001461814001") = 0)
        Assert.IsTrue(viewModel.calcularDigitoControl("61771001461814002") = 7)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlSeleccionarTabPendientes_ListaPendientesNoPuedeSerNulo()
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()

        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.IsNotNull(viewModel.listaPendientes)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEtiquetasPendientesAlSeleccionarLaTabPendientes_ListaPendientesTieneAlgunRegistro()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.AreEqual(1, viewModel.listaPendientes.Count)
        A.CallTo(Function() servicio.CargarListaPendientes()).MustHaveHappenedOnceExactly
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEtiquetasPendientesAlSeleccionarLaTabPendientes_EnvioPendienteSeleccionadoNoEsNull()
        CrearViewModelConUnEnvioEnLaListaDePendientes()

        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.IsNotNull(viewModel.EnvioPendienteSeleccionado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEtiquetasPendientesAntesDeSeleccionarLaTabPendientes_ListaPendientesEstaVacia()
        CrearViewModelConUnEnvioEnLaListaDePendientes()

        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()

        Assert.IsNotNull(viewModel.listaPendientes)
        Assert.AreEqual(0, viewModel.listaPendientes.Count)
    End Sub

    '' seleccionar pestaña pendientes se desactiva la combo de agencias, porque tratamos todos juntos

    <TestMethod>
    Public Sub AgenciaViewModel_AlSeleccionarLaTabPendientesVariasVeces_LaListaPendientesMantieneElNumeroDeRegistros()
        CrearViewModelConUnEnvioEnLaListaDePendientes()

        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPedidos"}
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.AreEqual(1, viewModel.listaPendientes.Count)
        A.CallTo(Function() servicio.CargarListaPendientes()).MustHaveHappenedTwiceExactly
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiNoHayEnvioPendienteSeleccionado_ElBotonBorrarEstaInactivo()
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()
        viewModel.EnvioPendienteSeleccionado = Nothing

        Assert.IsFalse(viewModel.BorrarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEnvioPendienteSeleccionado_ElBotonBorrarrEstaActivo()
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()
        viewModel.EnvioPendienteSeleccionado = A.Fake(Of EnvioAgenciaWrapper)

        Assert.IsTrue(viewModel.BorrarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiNoHayCambiosSinGuardar_ElBotonGuardarEstaInactivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.IsFalse(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayCambiosSinGuardar_ElBotonGuardarEstaActivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        viewModel.EnvioPendienteSeleccionado.Direccion = "nombre nuevo"

        Assert.IsTrue(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiElNuevoEstaSinGuardar_ElBotonGuardarEstaActivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.IsTrue(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiElNuevoEstaGuardado_ElBotonGuardarEstaInactivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        viewModel.InsertarEnvioPendienteCommand.Execute()
        viewModel.GuardarEnvioPendienteCommand.Execute()

        Assert.IsFalse(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlSeleccionarLaTabPendientes_ElBotonInsertarEstaActivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()

        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.IsTrue(viewModel.InsertarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayCambiosSinGuardar_ElBotonInsertarEstaInactivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.EnvioPendienteSeleccionado.Nombre = "Carlos"

        Assert.IsFalse(viewModel.InsertarEnvioPendienteCommand.CanExecute)
    End Sub

    ' si se modifica alguna propiedad de EnvioPendienteSeleccionado hay que raise el guardar e insertar 

    <TestMethod>
    Public Sub AgenciasViewModel_AlInsertar_TenemosUnEnvioMas()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.AreEqual(2, viewModel.listaPendientes.Count)
    End Sub

    <TestMethod()>
    Public Sub AgenciaViewModel_AlInsertar_ElNuevoEnvioEstaEnPendientes()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.AreEqual(CType(-1, Short), viewModel.EnvioPendienteSeleccionado.Estado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlInsertar_HayUnaLlamadaPropertyChangeDeInsertar()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
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
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.EnvioPendienteSeleccionado = Nothing

        Assert.IsFalse(viewModel.HayUnEnvioPendienteSeleccionado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_CuandoCambiaEnvioPendienteSeleccionado_SeActualizaHayUnEnvioPendienteSeleccionado()
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()
        Dim vecesEjecutado As Integer = 0
        Dim seHaEjecutado = Sub(s, e)
                                If e.PropertyName = "HayUnEnvioPendienteSeleccionado" Then
                                    vecesEjecutado += 1
                                End If
                            End Sub
        AddHandler viewModel.PropertyChanged, seHaEjecutado
        viewModel.EnvioPendienteSeleccionado = Nothing

        Assert.AreEqual(1, vecesEjecutado)
    End Sub

    ' Otra forma más elegante de hacer lo mismo que en AgenciaViewModel_AlInsertar_HayUnaLlamadaPropertyChangeDeInsertar
    <TestMethod>
    Public Sub AgenciaViewModel_AlInsertar_HayUnaLlamadaPropertyChangeDeBorrar()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        Dim handler = A.Fake(Of EventHandler)
        AddHandler viewModel.BorrarEnvioPendienteCommand.CanExecuteChanged, handler

        viewModel.InsertarEnvioPendienteCommand.Execute()

        A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlModificarAlgunaPropiedadDelEnvioPendienteSeleccionado_HayUnaLlamadaPropertyChangeDeBorrar()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        Dim handler = A.Fake(Of EventHandler)
        AddHandler viewModel.BorrarEnvioPendienteCommand.CanExecuteChanged, handler

        viewModel.EnvioPendienteSeleccionado.Nombre = "Carlos"
        viewModel.EnvioPendienteSeleccionado.Fecha = New DateTime(2017, 10, 26)
        viewModel.EnvioPendienteSeleccionado.Horario = 2

        A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappened(3, Times.Exactly)
    End Sub


    <TestMethod>
    Public Sub AgenciaViewModel_AlInsertarPendiente_HayUnaLlamadaPropertyChangeDeInsertar()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        Dim handler = A.Fake(Of EventHandler)
        AddHandler viewModel.InsertarEnvioPendienteCommand.CanExecuteChanged, handler

        viewModel.BorrarEnvioPendienteCommand.Execute()

        A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlBorrarPendiente_HayUnaLlamadaPropertyChangeDeBorrar()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}
        Dim handler = A.Fake(Of EventHandler)
        AddHandler viewModel.BorrarEnvioPendienteCommand.CanExecuteChanged, handler

        viewModel.BorrarEnvioPendienteCommand.Execute()

        A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlBorrarPendiente_TenemosUnEnvioMenos()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.BorrarEnvioPendienteCommand.Execute()

        Assert.AreEqual(0, viewModel.listaPendientes.Count)
    End Sub

    ' al imprimir etiqueta busca si hay etiqueta pendiente antes de crear una nueva

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayBorrarEnvioPendienteNoQuedanMas_EnvioPendienteSeleccionadoTieneQueSerNulo()
        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        viewModel.InsertarEnvioPendienteCommand.Execute()
        viewModel.BorrarEnvioPendienteCommand.Execute()

        Assert.IsNull(viewModel.EnvioPendienteSeleccionado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayUnEnvioPendienteSeleccionado_BorrarCommandEstaActivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        Dim vecesEjecutado As Integer = 0
        Dim seHaEjecutado = Sub()
                                vecesEjecutado += 1
                            End Sub
        AddHandler viewModel.BorrarEnvioPendienteCommand.CanExecuteChanged, seHaEjecutado
        viewModel.PestañaSeleccionada = New TabItem With {.Name = "tabPendientes"}

        Assert.IsTrue(viewModel.BorrarEnvioPendienteCommand.CanExecute)
        Assert.AreEqual(1, vecesEjecutado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlBorrarEnvio_SeEliminaDeListaEnviosPedido()
        CrearViewModelConUnEnvioEnLaListaDePedidos()
        viewModel.envioActual = viewModel.listaEnviosPedido.Single

        viewModel.cmdBorrar.Execute(Nothing)

        Assert.AreEqual(0, viewModel.listaEnviosPedido.Count)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlTramitarEnvio_SeAgregaAListaDeTramitados()
        CrearViewModelConUnEnvioEnLaListaDePedidos()
        viewModel.envioActual = viewModel.listaEnviosPedido.Single

        viewModel.cmdTramitar.Execute(Nothing)

        Assert.AreEqual(1, viewModel.listaEnviosTramitados.Count)
    End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_AlCambiarDeAgencia_ActualizaElPais()
    '    Dim empresa = A.Fake(Of Empresas)
    '    empresa.Número = "1"
    '    Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
    '        empresa
    '    }
    '    A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
    '    Dim agencia1 = A.Fake(Of AgenciasTransporte)
    '    agencia1.Empresa = "1"
    '    agencia1.Numero = 1
    '    agencia1.Nombre = "ASM"
    '    agencia1.Ruta = "YYY"
    '    Dim agencia2 = A.Fake(Of AgenciasTransporte)
    '    agencia2.Empresa = "1"
    '    agencia2.Numero = 2
    '    agencia2.Nombre = "OnTime"
    '    agencia2.Ruta = "XXX"
    '    A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia2)
    '    A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia1, agencia2})
    '    viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
    '    viewModel.cmdCargarDatos.Execute()

    '    viewModel.agenciaSeleccionada = agencia2
    '    'viewModel.agenciaSeleccionada = agencia1

    '    Assert.IsNotNull(viewModel.paisActual)
    '    Assert.AreEqual(999, viewModel.paisActual.id)
    'End Sub

    Private Sub CrearViewModelConUnEnvioEnLaListaDePendientes()
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("12345     ")

        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1"
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim envio = New EnvioAgenciaWrapper With {
            .Numero = 123
        }
        Dim listaEnvios = New List(Of EnvioAgenciaWrapper) From {
            envio
        }
        A.CallTo(Function() servicio.CargarListaPendientes()).Returns(listaEnvios)

        Dim pedido = New CabPedidoVta With {
            .Empresa = "1",
            .Número = 12345,
            .Ruta = "XXX",
            .Nº_Cliente = "1",
            .Contacto = "0"
        }

        Dim cliente = New Clientes() With {
            .Empresa = "1",
            .Nº_Cliente = "1",
            .Contacto = "0",
            .Nombre = "Nombre cliente",
            .Dirección = "Dirección cliente",
            .Población = "Población cliente",
            .Provincia = "Provincia cliente",
            .CodPostal = "28001",
            .Teléfono = "911234567"
        }
        pedido.Clientes = cliente
        A.CallTo(Function() servicio.CargarPedidoPorNumero(12345, False)).Returns(pedido)

        Dim agencia = A.Fake(Of AgenciasTransporte)
        agencia.Empresa = "1"
        agencia.Numero = 2
        agencia.Ruta = "XXX"
        A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia)
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia})

        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()
    End Sub

    Private Sub CrearViewModelConUnEnvioEnLaListaDePedidos()
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("12345     ")

        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1"
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim envio = New EnviosAgencia With {
            .Numero = 123
        }
        Dim listaEnvios = New ObservableCollection(Of EnviosAgencia) From {
            envio
        }
        A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, A(Of Integer).Ignored)).Returns(listaEnvios)

        Dim pedido = New CabPedidoVta With {
            .Empresa = "1",
            .Número = 12345,
            .Ruta = "XXX",
            .Nº_Cliente = "1",
            .Contacto = "0"
        }

        Dim cliente = New Clientes() With {
            .Empresa = "1",
            .Nº_Cliente = "1",
            .Contacto = "0",
            .Nombre = "Nombre cliente",
            .Dirección = "Dirección cliente",
            .Población = "Población cliente",
            .Provincia = "Provincia cliente",
            .CodPostal = "28001",
            .Teléfono = "911234567"
        }
        pedido.Clientes = cliente
        A.CallTo(Function() servicio.CargarPedidoPorNumero(12345, False)).Returns(pedido)

        Dim agencia = A.Fake(Of AgenciasTransporte)
        agencia.Empresa = "1"
        agencia.Numero = 2
        agencia.Ruta = "XXX"
        agencia.Nombre = "OnTime"
        A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia)
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia})
        A.CallTo(Function() servicio.CargarListaEnviosTramitadosPorFecha(A(Of String).Ignored, A(Of Date).Ignored)).Returns(New ObservableCollection(Of EnviosAgencia))

        viewModel = New AgenciasViewModel(container, regionManager, servicio, configuracion)
        viewModel.cmdCargarDatos.Execute()
    End Sub

End Class

