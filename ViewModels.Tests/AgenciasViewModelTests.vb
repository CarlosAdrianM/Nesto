Imports Nesto.ViewModels
Imports Prism.Regions
Imports FakeItEasy
Imports System.Collections.ObjectModel
Imports Nesto.Models.Nesto.Models
Imports Prism.Ioc
Imports Prism.Services.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta

<TestClass()>
Public Class AgenciaViewModelTests
    Private viewModel As AgenciasViewModel

    '''<summary>
    '''Obtiene o establece el contexto de las pruebas que proporciona
    '''información y funcionalidad para la serie de pruebas actual.
    '''</summary>
    Private container As IContainerProvider
    Private regionManager As IRegionManager
    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IDialogService
    Private servicioPedidos As IPedidoVentaService
    Private servicioAutenticacion As IServicioAutenticacion

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
        container = A.Fake(Of IContainerProvider)
        regionManager = A.Fake(Of RegionManager)
        servicio = A.Fake(Of IAgenciaService)
        dialogService = A.Fake(Of IDialogService)
        servicioPedidos = A.Fake(Of IPedidoVentaService)
        servicioAutenticacion = A.Fake(Of IServicioAutenticacion)
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
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)


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
        agencia.Nombre = "ASM"
        A.CallTo(Function() servicio.CargarAgenciaPorRuta(A(Of String).Ignored, A(Of String).Ignored)).Returns(agencia)
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia})
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.PestannaNombre = Pestannas.PEDIDOS

        'act
        viewModel.cmdCargarDatos.Execute()

        'assert
        Assert.IsNotNull(viewModel.agenciaSeleccionada)
        Assert.AreEqual(2, viewModel.agenciaSeleccionada.Numero)
    End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_AlCargarDatos_ConfiguraLaAgencia()
    ' YA NO SE CARGA LA AGENCIA POR RUTA, SINO QUE SELECCIONAMOS LA DE MENOR COSTE POR BULTOS Y PESO

    '    A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
    '    A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("36")
    '    Dim pedido = New CabPedidoVta With {
    '        .Empresa = "1",
    '        .Número = 36,
    '        .Clientes = New Clientes(),
    '        .Ruta = "XXX" ' El Pedido sale por ruta XXX, eso es importante
    '    }
    '    A.CallTo(Function() servicio.CargarPedidoPorNumero(A(Of Integer).Ignored)).Returns(pedido)
    '    Dim empresa1 = A.Fake(Of Empresas)
    '    empresa1.Número = "1"
    '    Dim empresa2 = A.Fake(Of Empresas)
    '    empresa2.Número = "2"
    '    Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
    '        empresa1, empresa2
    '    }
    '    A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
    '    Dim agencia1 = A.Fake(Of AgenciasTransporte)
    '    agencia1.Empresa = "1"
    '    agencia1.Numero = 1
    '    agencia1.Nombre = "Otra agencia"
    '    agencia1.Ruta = "XXX" ' Esta agencia es de ruta XXX pero eso no prevalece
    '    Dim agencia2 = A.Fake(Of AgenciasTransporte)
    '    agencia2.Empresa = "1"
    '    agencia2.Numero = 2
    '    agencia2.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS
    '    agencia2.Ruta = "YYY"
    '    A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia2) ' Lo que prevalece es que la agencia de la ruta XXX es la 2
    '    A.CallTo(Function() servicio.CargarListaAgencias("1")).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia1, agencia2})
    '    A.CallTo(Function() servicio.CargarCliente(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns(New Clientes With {.CodPostal = "28110"})
    '    viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService)
    '    viewModel.PestannaNombre = Pestannas.PEDIDOS
    '    viewModel.cmdCargarDatos.Execute()


    '    Assert.IsNotNull(viewModel.empresaSeleccionada)
    '    Assert.AreEqual("1", viewModel.empresaSeleccionada.Número)
    '    Assert.IsNotNull(viewModel.agenciaSeleccionada)
    '    Assert.AreEqual(2, viewModel.agenciaSeleccionada.Numero) ' La agencia de la ruta XXX
    'End Sub

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
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)

        'act
        viewModel.cmdCargarDatos.Execute()

        'assert
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).MustHaveHappenedOnceExactly
        A.CallTo(Function() servicio.CargarListaEmpresas()).MustHaveHappenedOnceExactly
        A.CallTo(Function() servicio.CargarListaEnviosPedido(A(Of String).Ignored, A(Of Integer).Ignored)).MustHaveHappenedOnceExactly
        A.CallTo(Function() servicio.CargarListaPendientes()).MustNotHaveHappened
        A.CallTo(Function() servicio.CargarListaEnvios(A(Of Integer).Ignored)).MustNotHaveHappened
        A.CallTo(Function() servicio.CargarListaEnviosTramitados(A(Of String).Ignored, A(Of Integer).Ignored, A(Of Date).Ignored)).MustNotHaveHappened
        A.CallTo(Function() servicio.CargarListaReembolsos(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened
        A.CallTo(Function() servicio.CargarListaRetornos(A(Of String).Ignored, A(Of Integer).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlSeleccionarTabPendientes_ListaPendientesNoPuedeSerNulo()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.cmdCargarDatos.Execute()

        viewModel.PestannaNombre = Pestannas.PENDIENTES

        Assert.IsNotNull(viewModel.listaPendientes)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEtiquetasPendientesAlSeleccionarLaTabPendientes_ListaPendientesTieneAlgunRegistro()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        Assert.AreEqual(1, viewModel.listaPendientes.Count)
        A.CallTo(Function() servicio.CargarListaPendientes()).MustHaveHappenedOnceExactly
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEtiquetasPendientesAlSeleccionarLaTabPendientes_EnvioPendienteSeleccionadoNoEsNull()
        CrearViewModelConUnEnvioEnLaListaDePendientes()

        viewModel.PestannaNombre = Pestannas.PENDIENTES

        Assert.IsNotNull(viewModel.EnvioPendienteSeleccionado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEtiquetasPendientesAntesDeSeleccionarLaTabPendientes_ListaPendientesEstaVacia()
        CrearViewModelConUnEnvioEnLaListaDePendientes()

        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.cmdCargarDatos.Execute()

        Assert.IsNotNull(viewModel.listaPendientes)
        Assert.AreEqual(0, viewModel.listaPendientes.Count)
    End Sub

    '' seleccionar pestaña pendientes se desactiva la combo de agencias, porque tratamos todos juntos

    <TestMethod>
    Public Sub AgenciaViewModel_AlInsertarEnvio_SiEstaPendienteCogeLaAgenciaDelPendiente()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.ETIQUETAS

        viewModel.numeroPedido = 12345

        Assert.AreEqual(Constantes.Agencias.AGENCIA_REEMBOLSOS, viewModel.agenciaSeleccionada.Nombre)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlSeleccionarLaTabPendientesVariasVeces_LaListaPendientesMantieneElNumeroDeRegistros()
        CrearViewModelConUnEnvioEnLaListaDePendientes()

        viewModel.PestannaNombre = Pestannas.PENDIENTES
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        Assert.AreEqual(1, viewModel.listaPendientes.Count)
        A.CallTo(Function() servicio.CargarListaPendientes()).MustHaveHappenedTwiceExactly
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiNoHayEnvioPendienteSeleccionado_ElBotonBorrarEstaInactivo()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.cmdCargarDatos.Execute()
        viewModel.EnvioPendienteSeleccionado = Nothing

        Assert.IsFalse(viewModel.BorrarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayEnvioPendienteSeleccionado_ElBotonBorrarEstaActivo()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        A.CallTo(Function() configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(True)
        viewModel.cmdCargarDatos.Execute()
        viewModel.EnvioPendienteSeleccionado = A.Fake(Of EnvioAgenciaWrapper)

        Assert.IsTrue(viewModel.BorrarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiNoHayCambiosSinGuardar_ElBotonGuardarEstaInactivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        Assert.IsFalse(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayCambiosSinGuardar_ElBotonGuardarEstaActivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        viewModel.EnvioPendienteSeleccionado.Direccion = "nombre nuevo"

        Assert.IsTrue(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiElNuevoEstaSinGuardar_ElBotonGuardarEstaActivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.IsTrue(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiElNuevoEstaGuardado_ElBotonGuardarEstaInactivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        viewModel.InsertarEnvioPendienteCommand.Execute()
        viewModel.GuardarEnvioPendienteCommand.Execute()

        Assert.IsFalse(viewModel.GuardarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlSeleccionarLaTabPendientes_ElBotonInsertarEstaActivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()

        viewModel.PestannaNombre = Pestannas.PENDIENTES

        Assert.IsTrue(viewModel.InsertarEnvioPendienteCommand.CanExecute)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayCambiosSinGuardar_ElBotonInsertarEstaInactivo()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        viewModel.EnvioPendienteSeleccionado.Nombre = "Carlos"

        Assert.IsFalse(viewModel.InsertarEnvioPendienteCommand.CanExecute)
    End Sub

    ' si se modifica alguna propiedad de EnvioPendienteSeleccionado hay que raise el guardar e insertar 

    <TestMethod>
    Public Sub AgenciasViewModel_AlInsertar_TenemosUnEnvioMas()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.AreEqual(2, viewModel.listaPendientes.Count)
    End Sub

    <TestMethod()>
    Public Sub AgenciaViewModel_AlInsertar_ElNuevoEnvioEstaEnPendientes()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.AreEqual(CType(-1, Short), viewModel.EnvioPendienteSeleccionado.Estado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlInsertar_HayUnaLlamadaPropertyChangeDeInsertar()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        Dim ejecutado As Boolean = False
        Dim seHaEjecutado = Sub()
                                ejecutado = True
                            End Sub
        AddHandler viewModel.InsertarEnvioPendienteCommand.CanExecuteChanged, seHaEjecutado

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.IsTrue(ejecutado)
    End Sub

    <TestMethod()>
    Public Sub AgenciaViewModel_AlInsertar_SiNoHayAgenciaNiEmpresaLasCoge()
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1"
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim envio = New EnvioAgenciaWrapper With {
            .Numero = 123,
            .Empresa = "1",
            .Agencia = 2
        }

        Dim agencia = New AgenciasTransporte With {
            .Empresa = "1",
            .Numero = 2,
            .Ruta = "XXX",
            .Nombre = "ASM"
        }
        A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia)
        Dim listaAgencias = New ObservableCollection(Of AgenciasTransporte) From {agencia}
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(listaAgencias)
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.IsNotNull(viewModel.empresaSeleccionada)
        Assert.IsNotNull(viewModel.agenciaSeleccionada)
    End Sub

    ' Este test hay que actualizarlo si cambiamos la agencia por defecto.
    <TestMethod()>
    Public Sub AgenciaViewModel_AlInsertar_CogeLosValoresPorDefecto()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        viewModel.InsertarEnvioPendienteCommand.Execute()

        Assert.AreEqual(New AgenciaCorreosExpress().ServicioDefecto(), viewModel.servicioActual.ServicioId)
        Assert.AreEqual(New AgenciaCorreosExpress().HorarioDefecto, viewModel.horarioActual.id)
        Assert.AreEqual(New AgenciaCorreosExpress().paisDefecto, viewModel.paisActual.Id)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_CuandoNoHayEnvioPendienteSeleccionado_LosCamposDeLaTabPendientesEstanInactivos()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.cmdCargarDatos.Execute()
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        viewModel.EnvioPendienteSeleccionado = Nothing

        Assert.IsFalse(viewModel.HayUnEnvioPendienteSeleccionado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_CuandoCambiaEnvioPendienteSeleccionado_SeActualizaHayUnEnvioPendienteSeleccionado()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
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
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        Dim handler = A.Fake(Of EventHandler)
        AddHandler viewModel.BorrarEnvioPendienteCommand.CanExecuteChanged, handler

        viewModel.InsertarEnvioPendienteCommand.Execute()

        A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlModificarAlgunaPropiedadDelEnvioPendienteSeleccionado_HayUnaLlamadaPropertyChangeDeBorrar()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES
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
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        Dim handler = A.Fake(Of EventHandler)
        AddHandler viewModel.InsertarEnvioPendienteCommand.CanExecuteChanged, handler

        viewModel.InsertarEnvioPendienteCommand.Execute()

        A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlBorrarPendiente_HayUnaLlamadaPropertyChangeDeBorrar()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        Dim handler = A.Fake(Of EventHandler)
        AddHandler viewModel.BorrarEnvioPendienteCommand.CanExecuteChanged, handler

        viewModel.BorrarEnvioPendienteCommand.Execute()

        A.CallTo(Sub() handler.Invoke(A(Of Object).Ignored, A(Of EventArgs).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlBorrarPendiente_TenemosUnEnvioMenos()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        viewModel.BorrarEnvioPendienteCommand.Execute()

        Assert.AreEqual(0, viewModel.listaPendientes.Count)
    End Sub

    ' al imprimir etiqueta busca si hay etiqueta pendiente antes de crear una nueva

    <TestMethod>
    Public Sub AgenciaViewModel_SiAlBorrarEnvioPendienteNoQuedanMas_EnvioPendienteSeleccionadoTieneQueSerNulo()
        'arrange
        Dim agencia = New AgenciasTransporte With {
            .Empresa = "1  ",
            .Numero = 2,
            .Ruta = "XXX"
        }
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia})
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        'A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("36")
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
        'A.CallTo(Sub() dialogService.
        '             ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).
        '             Invokes(Of String, IDialogParameters, Action(Of IDialogResult))(Sub(n, p, c) c(New DialogResult(ButtonResult.OK)))
        A.CallTo(Sub() dialogService.
             ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).
             Invokes(Of String, IDialogParameters, Action(Of IDialogResult))(Sub(n, p, c)
                                                                                 If c IsNot Nothing Then
                                                                                     c(New DialogResult(ButtonResult.OK))
                                                                                 End If
                                                                             End Sub)
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.cmdCargarDatos.Execute()
        Dim horario As tipoIdDescripcion = New tipoIdDescripcion With {.id = 1, .descripcion = "horario estándar"}
        viewModel.listaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
            horario
        }
        viewModel.PestannaNombre = Pestannas.PENDIENTES

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
        viewModel.PestannaNombre = Pestannas.PENDIENTES

        Assert.IsTrue(viewModel.BorrarEnvioPendienteCommand.CanExecute)
        Assert.AreEqual(1, vecesEjecutado)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_SiHayUnEnvioPendienteSeleccionadoCreandose_AlCambiarDeAgenciaCambiaLaDelEnvio()
        CrearViewModelConUnEnvioEnLaListaDePendientes()
        Dim agencia2 = New AgenciasTransporte With {
            .Empresa = "1",
            .Numero = 8,
            .Ruta = "XXX",
            .Nombre = "Correos Express"
        }
        viewModel.listaAgencias.Add(agencia2)
        viewModel.PestannaNombre = Pestannas.PENDIENTES
        viewModel.InsertarEnvioPendienteCommand.Execute()

        viewModel.agenciaSeleccionada = agencia2

        Assert.AreEqual(8, viewModel.EnvioPendienteSeleccionado.Agencia)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlBorrarEnvio_SeEliminaDeListaEnviosPedido()
        CrearViewModelConUnEnvioEnLaListaDePedidos()

        viewModel.envioActual = viewModel.listaEnviosPedido.Single

        viewModel.cmdBorrar.Execute(Nothing)

        Assert.AreEqual(0, viewModel.listaEnviosPedido.Count)
    End Sub


    <TestMethod>
    Public Sub AgenciaViewModel_AlCambiarDeAgencia_ActualizaElPais()
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1")
        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1"
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim agencia1 = A.Fake(Of AgenciasTransporte)
        agencia1.Empresa = "1"
        agencia1.Numero = 1
        agencia1.Nombre = "ASM"
        agencia1.Ruta = "YYY"
        Dim agencia2 = A.Fake(Of AgenciasTransporte)
        agencia2.Empresa = "1"
        agencia2.Numero = 2
        agencia2.Nombre = "Correos Express"
        agencia2.Ruta = "XXX"
        A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia2)
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia1, agencia2})
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute()

        viewModel.agenciaSeleccionada = agencia1
        Assert.IsNotNull(viewModel.paisActual)
        Assert.AreEqual(34, viewModel.paisActual.Id)

        viewModel.agenciaSeleccionada = agencia2
        Assert.IsNotNull(viewModel.paisActual)
        Assert.AreEqual(724, viewModel.paisActual.Id)
    End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_AlCambiarDeAgenciaEnPestannaEnCurso_CambiaElEnvioActual()
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1")
        A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("12345     ")
        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1"
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
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
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim agencia1 = A.Fake(Of AgenciasTransporte)
        agencia1.Empresa = "1"
        agencia1.Numero = 1
        agencia1.Nombre = "ASM"
        agencia1.Ruta = "YYY"
        Dim agencia2 = A.Fake(Of AgenciasTransporte)
        agencia2.Empresa = "1"
        agencia2.Numero = 2
        agencia2.Nombre = "Correos Express"
        agencia2.Ruta = "XXX"
        A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia2)
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia1, agencia2})
        Dim envio1 = A.Fake(Of EnviosAgencia)
        envio1.Pais = 34 ' España
        Dim envio2 = A.Fake(Of EnviosAgencia)
        envio2.Pais = 724 ' España
        A.CallTo(Function() servicio.CargarListaEnvios(1)).Returns(New ObservableCollection(Of EnviosAgencia) From {envio1})
        A.CallTo(Function() servicio.CargarListaEnvios(2)).Returns(New ObservableCollection(Of EnviosAgencia) From {envio2})

        A.CallTo(Function() servicio.CargarListaEnviosPedido("1", 12345)).Returns(New ObservableCollection(Of EnviosAgencia) From {envio1})
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute()

        viewModel.agenciaSeleccionada = agencia1
        Assert.IsNotNull(viewModel.paisActual)
        Assert.AreEqual(viewModel.envioActual.Pais, viewModel.paisActual.Id)

        viewModel.PestannaNombre = Pestannas.EN_CURSO

        viewModel.agenciaSeleccionada = agencia2
        Assert.IsNotNull(viewModel.paisActual)
        Assert.AreEqual(viewModel.envioActual.Pais, viewModel.paisActual.Id)
    End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_ConfigurarAgencia_CogeLaAgenciaQueCoincidaLaRuta()
    '    A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
    '    A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("36")
    '    Dim empresa = A.Fake(Of Empresas)
    '    empresa.Número = "1"
    '    Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
    '        empresa
    '    }
    '    A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
    '    Dim agencia1 = A.Fake(Of AgenciasTransporte)
    '    agencia1.Empresa = "1"
    '    agencia1.Numero = 1
    '    agencia1.Nombre = "OnTime"
    '    agencia1.Ruta = "YYY"
    '    Dim agencia2 = A.Fake(Of AgenciasTransporte)
    '    agencia2.Empresa = "1"
    '    agencia2.Numero = 2
    '    agencia2.Nombre = "ASM"
    '    agencia2.Ruta = "XXX"
    '    A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia2)
    '    A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia1, agencia2})
    '    Dim pedido = A.Fake(Of CabPedidoVta)
    '    pedido.Empresa = "1"
    '    pedido.Ruta = "XXX"
    '    A.CallTo(Function() servicio.CargarPedidoPorNumero(123456)).Returns(pedido)
    '    viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService)
    '    viewModel.PestañaSeleccionada = New TabItem With {.Name = Pestannas.PEDIDOS}
    '    viewModel.cmdCargarDatos.Execute()

    '    viewModel.numeroPedido = 123456

    '    Assert.IsNotNull(viewModel.agenciaSeleccionada)
    '    Assert.AreEqual(2, viewModel.agenciaSeleccionada.Numero)
    'End Sub

    '' Comentamos el test, porqeu ahora sacamos todo por la misma agencia
    '<TestMethod>
    'Public Sub AgenciaViewModel_ConfigurarAgencia_SiTieneReembolsoCogeLaAgenciaDeReembolso()
    '    A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
    '    A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("36")
    '    Dim empresa = A.Fake(Of Empresas)
    '    empresa.Número = "1"
    '    Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
    '        empresa
    '    }
    '    A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
    '    Dim agencia1 = A.Fake(Of AgenciasTransporte)
    '    agencia1.Empresa = "1"
    '    agencia1.Numero = 1
    '    agencia1.Nombre = "OnTime"
    '    agencia1.Ruta = "YYY"
    '    Dim agencia2 = A.Fake(Of AgenciasTransporte)
    '    agencia2.Empresa = "1"
    '    agencia2.Numero = 2
    '    agencia2.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS
    '    agencia2.Ruta = "XXX"
    '    A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "YYY")).Returns(agencia1)
    '    A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia1, agencia2})
    '    Dim pedido = A.Fake(Of CabPedidoVta)
    '    pedido.Empresa = "1"
    '    pedido.Número = 123456
    '    pedido.Ruta = "YYY"
    '    pedido.IVA = "G21"
    '    A.CallTo(Function() servicio.CargarLineasPedidoSinPicking(123456)).Returns(New List(Of LinPedidoVta) From {New LinPedidoVta With {.Total = 1}})
    '    A.CallTo(Function() servicio.CargarPedidoPorNumero(123456)).Returns(pedido)
    '    A.CallTo(Function() servicio.CargarEnvio("1", 123456)).Returns(Nothing)
    '    viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService)
    '    viewModel.PestannaNombre = Pestannas.PEDIDOS
    '    viewModel.cmdCargarDatos.Execute()

    '    viewModel.numeroPedido = 123456

    '    Assert.IsNotNull(viewModel.agenciaSeleccionada)
    '    Assert.AreEqual(Constantes.Agencias.AGENCIA_REEMBOLSOS, viewModel.agenciaSeleccionada.Nombre)
    'End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_ConfigurarAgencia_SiNoTieneReembolsoCogeLaAgenciaPorDefecto()
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("36")
        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1"
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim agencia1 = A.Fake(Of AgenciasTransporte)
        agencia1.Empresa = "1"
        agencia1.Numero = 1
        agencia1.Nombre = "Sending"
        agencia1.Ruta = "YYY"
        Dim agencia2 = A.Fake(Of AgenciasTransporte)
        agencia2.Empresa = "1"
        agencia2.Numero = 2
        agencia2.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS
        agencia2.Ruta = "XXX"
        A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "YYY")).Returns(agencia1)
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia1, agencia2})
        A.CallTo(Function() servicio.CargarCliente(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns(New Clientes With {.CodPostal = "28110"})
        Dim pedido = A.Fake(Of CabPedidoVta)
        pedido.Empresa = "1"
        pedido.Número = 123456
        pedido.Ruta = "YYY"
        pedido.IVA = "G21"
        'A.CallTo(Function() servicio.CargarLineasPedidoSinPicking(123456)).Returns(New List(Of LinPedidoVta) From {New LinPedidoVta With {.Total = 1}})
        A.CallTo(Function() servicio.CargarPedidoPorNumero(123456)).Returns(pedido)
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.PestannaNombre = Pestannas.PEDIDOS
        viewModel.cmdCargarDatos.Execute()

        viewModel.numeroPedido = 123456
        viewModel.paisActual = New Pais(99, "NARNIA")

        Assert.IsNotNull(viewModel.agenciaSeleccionada)
        Assert.AreEqual(Constantes.Agencias.AGENCIA_DEFECTO, viewModel.agenciaSeleccionada.Nombre)
    End Sub

    '<TestMethod>
    'Public Sub AgenciaViewModel_ConfigurarAgencia_CogeLaAgenciaDeLaEmpresaCorrecta()
    ' YA NO SE SELECCIONA LA AGENCIA POR RUTA, SINO QUE LO HACEMOS POR MENOR COSTE DE BULTOS Y PESO
    '    A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
    '    A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("36")
    '    Dim empresa1 = A.Fake(Of Empresas)
    '    empresa1.Número = "1"
    '    Dim empresa2 = A.Fake(Of Empresas)
    '    empresa2.Número = "2"
    '    Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
    '        empresa1, empresa2
    '    }
    '    A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
    '    Dim agencia1 = A.Fake(Of AgenciasTransporte)
    '    agencia1.Empresa = "1"
    '    agencia1.Numero = 1
    '    agencia1.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS
    '    agencia1.Ruta = "XXX"
    '    Dim agencia2 = A.Fake(Of AgenciasTransporte)
    '    agencia2.Empresa = "2"
    '    agencia2.Numero = 2
    '    agencia2.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS
    '    agencia2.Ruta = "XXX"
    '    A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia1)
    '    A.CallTo(Function() servicio.CargarAgenciaPorRuta("2", "XXX")).Returns(agencia2)
    '    A.CallTo(Function() servicio.CargarListaAgencias("1")).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia1})
    '    A.CallTo(Function() servicio.CargarListaAgencias("2")).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia2})
    '    A.CallTo(Function() servicio.CargarCliente(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns(New Clientes With {.CodPostal = "28110"})
    '    Dim pedido = A.Fake(Of CabPedidoVta)
    '    pedido.Empresa = "2"
    '    pedido.Número = 123456
    '    pedido.IVA = "G21"
    '    pedido.Ruta = "XXX"
    '    A.CallTo(Function() servicio.CargarPedidoPorNumero(123456)).Returns(pedido)
    '    viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService)
    '    viewModel.PestannaNombre = Pestannas.PEDIDOS
    '    viewModel.cmdCargarDatos.Execute()

    '    viewModel.numeroPedido = 123456

    '    Assert.IsNotNull(viewModel.empresaSeleccionada)
    '    Assert.AreEqual("2", viewModel.empresaSeleccionada.Número)
    '    Assert.IsNotNull(viewModel.agenciaSeleccionada)
    '    Assert.AreEqual(2, viewModel.agenciaSeleccionada.Numero)
    'End Sub

    <TestMethod>
    Public Sub AgenciaViewModel_FiltrarTramitados_SiSeBuscaPorNombreLoEncuentra()
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1")
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        Dim empresa1 = A.Fake(Of Empresas)
        empresa1.Número = "1"
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa1
        }
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim agencia1 = A.Fake(Of AgenciasTransporte)
        agencia1.Empresa = "1"
        agencia1.Numero = 1
        agencia1.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS
        agencia1.Ruta = "XXX"
        Dim agencia2 = A.Fake(Of AgenciasTransporte)
        agencia2.Empresa = "1"
        agencia2.Numero = 2
        agencia2.Nombre = "ASM"
        agencia2.Ruta = "XXX"
        A.CallTo(Function() servicio.CargarListaAgencias("1")).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia1, agencia2})
        Dim envio = A.Fake(Of EnviosAgencia)
        envio.Agencia = 2
        A.CallTo(Function() servicio.CargarListaEnviosTramitadosPorNombre("1", "Carlos")).Returns(New ObservableCollection(Of EnviosAgencia) From {envio})
        viewModel.PestannaNombre = Pestannas.TRAMITADOS
        A.CallTo(Function() servicio.CargarAgencia(2)).Returns(agencia2)
        viewModel.cmdCargarDatos.Execute()

        viewModel.nombreFiltro = "Carlos"

        Assert.AreEqual(1, viewModel.listaEnviosTramitados.Count)
    End Sub



    Private Sub CrearViewModelConUnEnvioEnLaListaDePendientes()
        A.CallTo(Function() configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1  ")
        A.CallTo(Function() configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("12345     ")
        A.CallTo(Function() configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(True)

        Dim empresa = A.Fake(Of Empresas)
        empresa.Número = "1"
        Dim listaEmpresas = New ObservableCollection(Of Empresas) From {
            empresa
        }
        A.CallTo(Function() servicio.CargarListaEmpresas()).Returns(listaEmpresas)
        Dim envio = New EnvioAgenciaWrapper With {
            .Numero = 123,
            .Empresa = "1",
            .Agencia = 2
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
            .CodPostal = "07001",
            .Teléfono = "911234567"
        }
        pedido.Clientes = cliente
        A.CallTo(Function() servicio.CargarPedidoPorNumero(12345, False)).Returns(pedido)

        Dim agencia = New AgenciasTransporte With {
            .Empresa = "1",
            .Numero = 2,
            .Ruta = "XXX",
            .Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS
        }
        Dim agencia2 = New AgenciasTransporte With {
            .Empresa = "1",
            .Numero = 1,
            .Ruta = "NNN",
            .Nombre = Constantes.Agencias.AGENCIA_DEFECTO
        }
        A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia)
        Dim listaAgencias = New ObservableCollection(Of AgenciasTransporte) From {agencia, agencia2}
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(listaAgencias)
        Dim envioSinWrapper As EnviosAgencia = envio.ToEnvioAgencia
        envioSinWrapper.AgenciasTransporte = agencia
        A.CallTo(Function() servicio.CargarEnvio("1", 12345)).Returns(envioSinWrapper)
        A.CallTo(Function() servicio.CargarCliente(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns(cliente)
        Dim res = New DialogResult(ButtonResult.OK)
        A.CallTo(Sub() dialogService.
                     ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).
                     Invokes(Of String, IDialogParameters, Action(Of IDialogResult))(Sub(n, p, c) c(res))

        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.PestannaNombre = Pestannas.PEDIDOS
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
        A.CallTo(Function() servicio.CargarEnvio("1", 12345)).Returns(Nothing)
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
        agencia.Nombre = "ASM"
        A.CallTo(Function() servicio.CargarAgenciaPorRuta("1", "XXX")).Returns(agencia)
        A.CallTo(Function() servicio.CargarListaAgencias(A(Of String).Ignored)).Returns(New ObservableCollection(Of AgenciasTransporte) From {agencia})
        A.CallTo(Function() servicio.CargarListaEnviosTramitadosPorFecha(A(Of String).Ignored, A(Of Date).Ignored)).Returns(New ObservableCollection(Of EnviosAgencia))
        A.CallTo(Sub() dialogService.
                     ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).
                     Invokes(Of String, IDialogParameters, Action(Of IDialogResult))(Sub(n, p, c) c(New DialogResult(ButtonResult.OK)))
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.cmdCargarDatos.Execute()
    End Sub


    <TestMethod>
    Public Sub AgenciaCorreosExpress_CalcularDigitoControl_LoCalculaCon15Cifras()
        Dim correosExpress = New AgenciaCorreosExpress
        Dim posiciones = {6, 3, 2, 2, 1, 8, 0, 0, 0, 0, 0, 0, 6, 2, 6}
        Dim digitoControl = correosExpress.CalcularDigitoControl(posiciones)

        Assert.AreEqual(2, digitoControl)
    End Sub

    <TestMethod>
    Public Sub AgenciaCorreosExpress_CalcularDigitoControl_LoCalculaCon22Cifras()
        Dim correosExpress = New AgenciaCorreosExpress
        Dim posiciones = {6, 1, 0, 1, 8, 0, 1, 3, 0, 9, 2, 6, 1, 6, 3, 0, 1, 1, 5, 0, 0, 1}

        Dim digitoControl = correosExpress.CalcularDigitoControl(posiciones)

        Assert.AreEqual(9, digitoControl)
    End Sub

    '<TestMethod>
    'Public Sub AgenciaCorreosExpress_CalcularCodigoBarras_CalculaBien()
    '    viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService)
    '    viewModel.envioActual = New EnviosAgencia
    '    viewModel.envioActual.Servicio = 63
    '    viewModel.envioActual.AgenciasTransporte = New AgenciasTransporte
    '    viewModel.envioActual.AgenciasTransporte.PrefijoCodigoBarras = "2218"
    '    viewModel.envioActual.Numero = 626
    '    Dim correosExpress = New AgenciaCorreosExpress

    '    Dim codigoBarras = correosExpress.calcularCodigoBarras(viewModel)

    '    Assert.AreEqual("6322180000006262", codigoBarras)
    'End Sub

    <TestMethod>
    Public Sub AgenciaCorreosExpress_CalcularCodigoBarrasBulto_CalculaBien()
        Dim correosExpress = New AgenciaCorreosExpress
        Dim codigoBarrasEnvio = "6101801309261631"

        Dim codigoBarrasBulto = correosExpress.CalcularCodigoBarrasBulto(codigoBarrasEnvio, 1, "15001")

        Assert.AreEqual("61018013092616301150019", codigoBarrasBulto)
    End Sub

    <TestMethod>
    Public Sub AgenciaCorreosExpress_CalcularCodigoBarrasRetorno_CalculaBien()
        Dim correosExpress = New AgenciaCorreosExpress
        Dim codigoBarrasEnvio = "5408522919307535"

        Dim codigoBarrasRetorno = correosExpress.CalcularCodigoBarrasRetorno(codigoBarrasEnvio)

        Assert.AreEqual("5208522919307544", codigoBarrasRetorno)
    End Sub

    ' Nesto#359: tests mínimos de AgenciaCanteras. La factory de AgenciasViewModel la invoca
    ' al seleccionar la agencia, así que como mínimo debe construirse sin crashear, devolver
    ' "OK" en la llamada al "web service" (sin integración real) y no admitir retorno/cobro
    ' (Canteras solo entrega; reembolso lo bloquea NestoAPI).

    <TestMethod>
    Public Sub AgenciaCanteras_Constructor_NoLanzaExcepcion()
        Dim canteras = New AgenciaCanteras()

        Assert.IsNotNull(canteras.ListaPaises)
        Assert.IsNotNull(canteras.ListaTiposRetorno)
        Assert.IsNotNull(canteras.ListaServicios)
        Assert.IsNotNull(canteras.ListaHorarios)
    End Sub

    <TestMethod>
    Public Async Function AgenciaCanteras_LlamadaWebService_DevuelveOkSinIntegracion() As Task
        Dim canteras = New AgenciaCanteras()
        Dim respuesta = Await canteras.LlamadaWebService(New EnviosAgencia(), Nothing)

        Assert.IsTrue(respuesta.Exito, "Canteras no integra; debe responder OK para que la máquina de estados avance.")
        Assert.AreEqual("OK", respuesta.TextoRespuestaError)
    End Function

    <TestMethod>
    Public Sub AgenciaCanteras_NoAdmiteRetorno()
        Dim canteras = New AgenciaCanteras()

        Assert.AreEqual(CByte(0), canteras.retornoObligatorio,
            "Canteras nunca pide retorno (operativa manual).")
        Assert.AreEqual(CByte(0), canteras.retornoSinRetorno)
    End Sub

    <TestMethod>
    Public Sub AgenciaCanteras_PaisDefecto_EsEspanna()
        Dim canteras = New AgenciaCanteras()

        Assert.AreEqual(34, canteras.paisDefecto, "Canarias es territorio español; país por defecto = 34.")
    End Sub

    ' Nesto#359: flag PermiteEditarCodigoBarras. Canteras lo declara True para que el
    ' usuario pueda pegar el nº de envío que llega por correo. El resto de agencias
    ' debe heredar el default False (calculan el código automáticamente y pisarlo
    ' rompería el seguimiento). Comprobamos al menos una para detectar despistes.

    <TestMethod>
    Public Sub AgenciaCanteras_PermiteEditarCodigoBarras_DevuelveTrue()
        Dim canteras = New AgenciaCanteras()

        Assert.IsTrue(canteras.PermiteEditarCodigoBarras,
            "Canteras debe permitir edición manual del nº de envío (llega por correo después de tramitar).")
    End Sub

    <TestMethod>
    Public Sub AgenciaCorreosExpress_PermiteEditarCodigoBarras_DevuelveFalse()
        Dim cex = New AgenciaCorreosExpress()

        Assert.IsFalse(cex.PermiteEditarCodigoBarras,
            "CEX calcula el CodigoBarras automáticamente al tramitar; pisarlo a mano rompería el tracking.")
    End Sub

    ' Nesto#359: cmdPegarCodigoBarras. Solo habilitado si la agencia lo permite Y el
    ' CodigoBarras está vacío (para no sobreescribir tramitados/asignados). Ejecutarlo
    ' debe asignar el texto introducido y llamar a _servicio.Modificar.

    <TestMethod>
    Public Sub cmdPegarCodigoBarras_SinEnvioActual_CanExecuteFalse()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.agenciaSeleccionada = New AgenciasTransporte With {.Nombre = "Canteras"}
        viewModel.envioActual = Nothing

        Assert.IsFalse(viewModel.cmdPegarCodigoBarras.CanExecute())
    End Sub

    <TestMethod>
    Public Sub cmdPegarCodigoBarras_AgenciaNoPermite_CanExecuteFalse()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.agenciaSeleccionada = New AgenciasTransporte With {.Nombre = "Correos Express"}
        viewModel.envioActual = New EnviosAgencia With {.CodigoBarras = Nothing}

        Assert.IsFalse(viewModel.cmdPegarCodigoBarras.CanExecute())
    End Sub

    <TestMethod>
    Public Sub cmdPegarCodigoBarras_CodigoBarrasYaAsignado_CanExecuteFalse()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.agenciaSeleccionada = New AgenciasTransporte With {.Nombre = "Canteras"}
        viewModel.envioActual = New EnviosAgencia With {.CodigoBarras = "ENV-ABC-001"}

        Assert.IsFalse(viewModel.cmdPegarCodigoBarras.CanExecute(),
            "Si CodigoBarras ya está asignado no se debe permitir editar para no perder el original.")
    End Sub

    <TestMethod>
    Public Sub cmdPegarCodigoBarras_CanterasYCodigoVacio_CanExecuteTrue()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.agenciaSeleccionada = New AgenciasTransporte With {.Nombre = "Canteras"}
        viewModel.envioActual = New EnviosAgencia With {.CodigoBarras = Nothing}

        Assert.IsTrue(viewModel.cmdPegarCodigoBarras.CanExecute())
    End Sub

    <TestMethod>
    Public Sub cmdPegarCodigoBarras_DialogCancelado_NoLlamaAModificar()
        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.agenciaSeleccionada = New AgenciasTransporte With {.Nombre = "Canteras"}
        viewModel.envioActual = New EnviosAgencia With {.CodigoBarras = Nothing}
        ' Sin mock de ShowDialog → callback no se invoca → GetText devuelve Nothing → Return temprano.

        viewModel.cmdPegarCodigoBarras.Execute()

        Assert.IsTrue(String.IsNullOrEmpty(viewModel.envioActual.CodigoBarras))
        A.CallTo(Sub() servicio.Modificar(A(Of EnviosAgencia).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod>
    Public Sub cmdPegarCodigoBarras_DialogConTexto_AsignaCodigoYLlamaAModificar()
        Dim fakeResult = A.Fake(Of IDialogResult)
        A.CallTo(Function() fakeResult.Result).Returns(ButtonResult.OK)
        Dim params = New DialogParameters From {{"text", "ENVIO-12345"}}
        A.CallTo(Function() fakeResult.Parameters).Returns(params)
        A.CallTo(Sub() dialogService.
                     ShowDialog("InputTextDialog", A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).
                     Invokes(Of String, IDialogParameters, Action(Of IDialogResult))(Sub(n, p, c) c(fakeResult))

        viewModel = New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
        viewModel.agenciaSeleccionada = New AgenciasTransporte With {.Nombre = "Canteras"}
        Dim envio = New EnviosAgencia With {.CodigoBarras = Nothing, .Pedido = "12345"}
        viewModel.envioActual = envio

        viewModel.cmdPegarCodigoBarras.Execute()

        Assert.AreEqual("ENVIO-12345", envio.CodigoBarras)
        A.CallTo(Sub() servicio.Modificar(envio)).MustHaveHappenedOnceExactly()
    End Sub

End Class

