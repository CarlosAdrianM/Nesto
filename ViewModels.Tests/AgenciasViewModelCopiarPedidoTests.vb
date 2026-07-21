Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.ViewModels
Imports Prism.Regions
Imports Prism.Services.Dialogs

''' <summary>
''' Nesto#418: en Agencias → Incidentados el usuario necesita el nº de pedido para buscarlo en otro
''' sitio, pero Ctrl+C sobre el grid copia la fila entera (SelectionUnit por defecto = FullRow). Se
''' añade "Copiar nº de pedido" al menú contextual, que deja en el portapapeles SOLO el número.
'''
''' Aquí se prueba la lógica del ViewModel (qué texto se copiaría y cuándo está habilitado el
''' comando); la llamada a Clipboard.SetText no se prueba porque exige hilo STA, mismo criterio que
''' en DetallePedidoViewModelCopiarPortapapelesTests.
''' </summary>
<TestClass()>
Public Class AgenciasViewModelCopiarPedidoTests

    Private regionManager As IRegionManager
    Private servicio As IAgenciaService
    Private configuracion As IConfiguracion
    Private dialogService As IDialogService
    Private servicioPedidos As IPedidoVentaService
    Private servicioAutenticacion As IServicioAutenticacion

    <TestInitialize()>
    Public Sub Initialize()
        regionManager = A.Fake(Of IRegionManager)
        servicio = A.Fake(Of IAgenciaService)
        configuracion = A.Fake(Of IConfiguracion)
        dialogService = A.Fake(Of IDialogService)
        servicioPedidos = A.Fake(Of IPedidoVentaService)
        servicioAutenticacion = A.Fake(Of IServicioAutenticacion)
    End Sub

    Private Function CrearViewModel() As AgenciasViewModel
        Return New AgenciasViewModel(regionManager, servicio, configuracion, dialogService, servicioPedidos, servicioAutenticacion)
    End Function

    <TestMethod()>
    Public Sub TextoNumeroPedidoParaCopiar_SinEnvioSeleccionado_DevuelveCadenaVacia()
        Dim vm = CrearViewModel()

        Assert.AreEqual(String.Empty, vm.TextoNumeroPedidoParaCopiar,
                        "Sin envío seleccionado no hay nada que copiar y no debe lanzar")
    End Sub

    <TestMethod()>
    Public Sub TextoNumeroPedidoParaCopiar_ConEnvio_DevuelveSoloElNumeroDePedido()
        Dim vm = CrearViewModel()
        vm.envioActual = New EnviosAgencia With {
            .Numero = 12345,
            .Pedido = 922175,
            .Nombre = "CLIENTE DE PRUEBA",
            .Direccion = "CALLE FALSA 123"
        }

        Assert.AreEqual("922175", vm.TextoNumeroPedidoParaCopiar,
                        "Debe copiarse solo el nº de pedido, sin el resto de campos de la fila")
    End Sub

    <TestMethod()>
    Public Sub TextoNumeroPedidoParaCopiar_EnvioSinPedido_DevuelveCadenaVacia()
        Dim vm = CrearViewModel()
        vm.envioActual = New EnviosAgencia With {
            .Numero = 12345,
            .Pedido = Nothing
        }

        Assert.AreEqual(String.Empty, vm.TextoNumeroPedidoParaCopiar,
                        "Los envíos manuales pueden no tener pedido asociado: no debe lanzar ni copiar 'nada'")
    End Sub

    <TestMethod()>
    Public Sub CopiarNumeroPedidoCommand_SinEnvioSeleccionado_NoSePuedeEjecutar()
        Dim vm = CrearViewModel()

        Assert.IsFalse(vm.CopiarNumeroPedidoCommand.CanExecute(),
                       "El menú de copiar debe estar deshabilitado si no hay envío seleccionado")
    End Sub

    <TestMethod()>
    Public Sub CopiarNumeroPedidoCommand_EnvioSinPedido_NoSePuedeEjecutar()
        Dim vm = CrearViewModel()
        vm.envioActual = New EnviosAgencia With {.Numero = 12345, .Pedido = Nothing}

        Assert.IsFalse(vm.CopiarNumeroPedidoCommand.CanExecute(),
                       "Sin nº de pedido no hay nada que copiar")
    End Sub

    <TestMethod()>
    Public Sub CopiarNumeroPedidoCommand_ConEnvioConPedido_SePuedeEjecutar()
        Dim vm = CrearViewModel()
        vm.envioActual = New EnviosAgencia With {.Numero = 12345, .Pedido = 922175}

        Assert.IsTrue(vm.CopiarNumeroPedidoCommand.CanExecute(),
                      "Con un envío con pedido seleccionado, el menú debe estar habilitado")
    End Sub

    ' Nesto#422 (ampliación de #418): copiar nº de envío, campo bajo el cursor y envío completo

    <TestMethod()>
    Public Sub TextoNumeroEnvioParaCopiar_ConEnvio_DevuelveElCodigoDeBarras()
        Dim vm = CrearViewModel()
        vm.envioActual = New EnviosAgencia With {.Numero = 12345, .CodigoBarras = "61197140234493 "}

        Assert.AreEqual("61197140234493", vm.TextoNumeroEnvioParaCopiar, "Sin el padding de la BD")
        Assert.IsTrue(vm.CopiarNumeroEnvioCommand.CanExecute())
    End Sub

    <TestMethod()>
    Public Sub TextoNumeroEnvioParaCopiar_SinEnvioOSinCodigo_VacioYDeshabilitado()
        Dim vm = CrearViewModel()

        Assert.AreEqual(String.Empty, vm.TextoNumeroEnvioParaCopiar)
        Assert.IsFalse(vm.CopiarNumeroEnvioCommand.CanExecute())
    End Sub

    <TestMethod()>
    Public Sub CampoBajoCursor_LaVistaLoEstablece_ElMenuMuestraElNombreYSeHabilita()
        Dim vm = CrearViewModel()

        Assert.AreEqual("Copiar campo", vm.TextoCopiarCampoBajoCursor, "Sin celda: texto genérico")
        Assert.IsFalse(vm.CopiarCampoCommand.CanExecute())

        vm.EstablecerCampoBajoCursor("Población", "ALGETE")

        Assert.AreEqual("Copiar Población", vm.TextoCopiarCampoBajoCursor)
        Assert.IsTrue(vm.CopiarCampoCommand.CanExecute())

        vm.EstablecerCampoBajoCursor(Nothing, Nothing)
        Assert.IsFalse(vm.CopiarCampoCommand.CanExecute(), "Al salir de una celda se deshabilita")
    End Sub

    <TestMethod()>
    Public Sub TextoEnvioCompletoParaCopiar_ContieneTodosLosCamposVisiblesDelGrid()
        Dim vm = CrearViewModel()
        vm.envioActual = New EnviosAgencia With {
            .Numero = 12345,
            .Pedido = 922175,
            .Cliente = "15191 ",
            .Contacto = "0",
            .Nombre = "CLIENTE DE PRUEBA",
            .Direccion = "CALLE FALSA 123",
            .Poblacion = "ALGETE",
            .CodPostal = "28110",
            .Telefono = "916280000",
            .CodigoBarras = "61197140234493"
        }

        Dim texto = vm.TextoEnvioCompletoParaCopiar

        For Each esperado In {"Pedido: 922175", "Nº Cliente: 15191", "CLIENTE DE PRUEBA",
                              "CALLE FALSA 123", "ALGETE", "C.P.: 28110", "916280000",
                              "Nº Envío: 61197140234493"}
            StringAssert.Contains(texto, esperado)
        Next
        Assert.IsTrue(vm.CopiarEnvioCompletoCommand.CanExecute())
    End Sub

    <TestMethod()>
    Public Sub TextoEnvioCompletoParaCopiar_SinEnvio_VacioYDeshabilitado()
        Dim vm = CrearViewModel()

        Assert.AreEqual(String.Empty, vm.TextoEnvioCompletoParaCopiar)
        Assert.IsFalse(vm.CopiarEnvioCompletoCommand.CanExecute())
    End Sub

End Class
