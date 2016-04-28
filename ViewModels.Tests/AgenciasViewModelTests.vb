Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels
Imports Nesto.Models
Imports Microsoft.Practices.Unity
Imports Microsoft.Practices.Prism.Regions

<TestClass()>
Public Class cuandoInsertamosUnEnvio

    Dim agenciaVM As New AgenciasViewModel
    Private Shared DbContext As New NestoEntities

    Private testContextInstance As TestContext

    '''<summary>
    '''Obtiene o establece el contexto de las pruebas que proporciona
    '''información y funcionalidad para la serie de pruebas actual.
    '''</summary>
    Public Property TestContext() As TestContext
        Get
            Return testContextInstance
        End Get
        Set(ByVal value As TestContext)
            testContextInstance = value
        End Set
    End Property

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

    '<TestMethod()>
    'Public Sub debeDevolverUnValorAlXMLdeEntrada()
    '    'arrange
    '    agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM"}
    '    agenciaVM.empresaSeleccionada = New Empresas With {.Número = "95", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
    '    agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 545861).FirstOrDefault
    '    'error con el 561369 y 545860
    '    'act
    '    agenciaVM.llamadaWebService()
    '    'assert
    '    Debug.Print(agenciaVM.XMLdeSalida.ToString)
    '    Debug.Print(agenciaVM.XMLdeEntrada.ToString)
    '    Assert.IsFalse(IsNothing(agenciaVM.XMLdeEntrada))
    'End Sub

    <TestMethod()>
    Public Sub elDigitoControlEsCorrecto()
        'arrange

        'act

        'assert
        Assert.IsTrue(agenciaVM.calcularDigitoControl("21005190520534001") = 2)
        Assert.IsTrue(agenciaVM.calcularDigitoControl("61771001461814001") = 0)
        Assert.IsTrue(agenciaVM.calcularDigitoControl("61771001461814002") = 7)
    End Sub

    '<TestMethod()>
    'Public Sub debemosPoderImprimirEtiquetas()
    '    'arrange
    '    agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
    '    agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 561371).FirstOrDefault
    '    agenciaVM.bultos = 2
    '    'act
    '    agenciaVM.cmdImprimirEtiquetaPedido.Execute(Nothing)
    '    'assert
    '    Assert.IsTrue(True)
    'End Sub

    '<TestMethod()>
    'Public Sub debemosPoderInsertarEnvios()
    '    agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
    '    agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 562142).FirstOrDefault
    '    agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM"}
    '    agenciaVM.bultos = 2
    '    'act
    '    agenciaVM.cmdInsertar.Execute(Nothing)
    '    'agenciaVM.cmdImprimirEtiquetaPedido.Execute(Nothing)
    '    'assert
    '    Assert.IsTrue(True)
    'End Sub

End Class

<TestClass()>
Public Class cuandoCargamosElModelo
    Dim container As IUnityContainer
    Dim regionManager As IRegionManager
    Dim agenciaVM As New AgenciasViewModel(container, regionManager)


    Private testContextInstance As TestContext

    '''<summary>
    '''Obtiene o establece el contexto de las pruebas que proporciona
    '''información y funcionalidad para la serie de pruebas actual.
    '''</summary>
    Public Property TestContext() As TestContext
        Get
            Return testContextInstance
        End Get
        Set(ByVal value As TestContext)
            testContextInstance = value
        End Set
    End Property

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
    <TestMethod()>
    Public Sub laListaDeAgenciasNoPuedeEstarVacia()
        'arrange

        'act

        'assert
        Assert.IsFalse(agenciaVM.listaAgencias.Count = 0)
    End Sub



End Class

<TestClass()>
Public Class cuandoCogemosLosTelefonos
    Dim agenciaVM As New AgenciasViewModel

    <TestMethod()>
    Public Sub tieneQueDevolverUnTelefono()
        Dim listaTelefonos As String
        'act
        listaTelefonos = "123456789/911234567/666123456"
        'assert
        Assert.IsTrue(agenciaVM.telefonoUnico(listaTelefonos) <> "")
    End Sub

    <TestMethod()>
    Public Sub siPasamosVariosTieneQueDevolverUnoSolo()
        Dim listaTelefonos As String
        'act
        listaTelefonos = "123456789/911234567/666123456"
        'assert
        Assert.IsTrue(agenciaVM.telefonoUnico(listaTelefonos).Length = 9)
    End Sub

    <TestMethod()>
    Public Sub elFijoDebeEmpezarPorNueve()
        'arrange
        Dim listaTelefonos As String
        'act
        listaTelefonos = "123456789/911234567/666123456"
        'assert
        Assert.IsTrue(agenciaVM.telefonoUnico(listaTelefonos, "F").Substring(0, 1) = "9")
    End Sub

    <TestMethod()>
    Public Sub elMovilDebeEmpezarPorSeisSieteUOcho()
        Dim listaTelefonos As String
        'act
        listaTelefonos = "123456789/911234567/666123456"
        'assert
        Assert.IsTrue(agenciaVM.telefonoUnico(listaTelefonos, "M").Substring(0, 1) = "6" _
            Or agenciaVM.telefonoUnico(listaTelefonos, "M").Substring(0, 1) = "7" _
            Or agenciaVM.telefonoUnico(listaTelefonos, "M").Substring(0, 1) = "8")
    End Sub

    <TestMethod()>
    Public Sub siNoHayNingunoDelTipoSolicitadoDevolveraCadenaVacia()
        'arrange
        Dim listaTelefonos As String
        'act
        listaTelefonos = "666123456"
        'assert
        Assert.IsTrue(agenciaVM.telefonoUnico(listaTelefonos, "F") = "")
    End Sub

    <TestMethod()>
    Public Sub siUnTelefonoNoTieneLaLongitudValidaNoLoDevuelve()
        'arrange
        Dim listaTelefonos As String
        'act
        listaTelefonos = "66612345/911234567/611234567"
        'assert
        Assert.IsTrue(agenciaVM.telefonoUnico(listaTelefonos, "M") = "611234567")
    End Sub


End Class

<TestClass()>
Public Class cuandoCogemosLosCorreos
    Dim agenciaVM As New AgenciasViewModel
    Private Shared DbContext As New NestoEntities


    <TestMethod()>
    Public Sub debeDevolverUnoDeLosCorreos()
        'arrange
        Dim listaPersonas As New List(Of PersonasContactoCliente)
        'act
        listaPersonas.Add(New PersonasContactoCliente With {.CorreoElectrónico = "pepito@pepito.com", .Cargo = 5})
        'assert
        Assert.IsTrue(agenciaVM.correoUnico(listaPersonas) = "pepito@pepito.com")
    End Sub

    <TestMethod()>
    Public Sub siHayUnoDeAgenciasDebeDevolverEse()
        'arrange
        Dim listaPersonas As New List(Of PersonasContactoCliente)
        'act
        listaPersonas.Add(New PersonasContactoCliente With {.CorreoElectrónico = "pepito@pepito.com", .Cargo = 1})
        listaPersonas.Add(New PersonasContactoCliente With {.CorreoElectrónico = "fulanito@fulanito.com", .Cargo = 26})
        'assert
        Assert.IsTrue(agenciaVM.correoUnico(listaPersonas) = "fulanito@fulanito.com")
    End Sub

    <TestMethod()>
    Public Sub siNoHayPersonasDebeDevolverCadenaVacia()
        'arrange
        Dim listaPersonas As New List(Of PersonasContactoCliente)
        'act
        'assert
        Assert.IsTrue(agenciaVM.correoUnico(listaPersonas) = "")
    End Sub

    <TestMethod()>
    Public Sub debeDevolverElCorreoCorrecto()
        'arrange
        Dim cliente As Clientes
        cliente = (From c In DbContext.Clientes Where c.Empresa = "1" And c.Nº_Cliente = "17036" And c.Contacto = "0").FirstOrDefault
        'act

        'assert
        Assert.IsTrue(agenciaVM.correoUnico(cliente.PersonasContactoCliente.ToList) = "analoma@telefonica.net")
    End Sub


End Class

<TestClass()>
Public Class cuandoImprimimosLaEtiqueta
    Dim container As IUnityContainer
    Dim regionManager As IRegionManager
    Dim agenciaVM As New AgenciasViewModel(container, regionManager)
    Private Shared DbContext As New NestoEntities



    '<TestMethod()>
    'Public Sub debeGuardarElRegistroEnLaBBDD()
    '    'arrange
    '    agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
    '    agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 561372).FirstOrDefault
    '    agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM", .PrefijoCodigoBarras = "6112979"}
    '    agenciaVM.bultos = 2
    '    'act
    '    'agenciaVM.cmdImprimirEtiquetaPedido.Execute(Nothing)
    '    agenciaVM.insertarRegistro()
    '    'assert
    '    Assert.IsTrue(agenciaVM.envioActual.CodigoBarras = "61129790561372")
    '    Assert.IsTrue(agenciaVM.envioActual.Nemonico = "A63")
    '    Assert.IsTrue(agenciaVM.envioActual.NombrePlaza = "RETIRO 797")
    '    Assert.IsTrue(agenciaVM.envioActual.TelefonoPlaza = "915744174")
    '    Assert.IsTrue(agenciaVM.envioActual.EmailPlaza = "asm.retiro@asmred.com")
    'End Sub

    '<TestMethod()>
    'Public Sub debeGuardarElRegistroEnLaBBDD2()
    '    'arrange
    '    agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com"}
    '    agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 563148).FirstOrDefault
    '    agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM", .PrefijoCodigoBarras = "6112979"}
    '    agenciaVM.bultos = 2
    '    'act
    '    'agenciaVM.cmdImprimirEtiquetaPedido.Execute(Nothing)
    '    agenciaVM.insertarRegistro()
    '    'assert
    '    Assert.IsTrue(agenciaVM.envioActual.CodigoBarras = "61129790563148")
    '    'Assert.IsTrue(agenciaVM.envioActual.Nemonico = "A42")
    '    'Assert.IsTrue(agenciaVM.envioActual.NombrePlaza = "ASM NAVALCARNERO")
    '    'Assert.IsTrue(agenciaVM.envioActual.TelefonoPlaza = "918134517")
    '    'Assert.IsTrue(agenciaVM.envioActual.EmailPlaza = "asm.703@asmred.es")
    'End Sub

    <TestMethod()>
    Public Sub debeBuscarElMultiusuario()
        'arrange
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        'agenciaVM.empresaSeleccionada = DbContext.Empresas.Where(Function(e) e.Número = "1  ").FirstOrDefault
        'Debug.Print(agenciaVM.ToString)
        'agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM", .PrefijoCodigoBarras = "6112979"}

        agenciaVM.numeroMultiusuario = 25
        'act

        'assert
        Assert.IsTrue(True) 'agenciaVM.multiusuario.Nombre.Trim = "Pedro Jurado")
    End Sub
End Class

<TestClass()>
Public Class cuandoCambiamosElNumeroDePedido
    Dim container As IUnityContainer
    Dim regionManager As IRegionManager
    Dim agenciaVM As New AgenciasViewModel(container, regionManager)
    Private Shared DbContext As New NestoEntities
    <TestMethod()>
    Public Sub debeBuscarElPedido()
        'arrange
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        agenciaVM.numeroPedido = 523418
        'act

        'assert
        Assert.IsTrue(agenciaVM.direccionEnvio = "Pª DEL PIREO Nº6")
    End Sub


End Class

<TestClass()>
Public Class cuandoContabilizamosElReembolso
    Dim agenciaVM As New AgenciasViewModel
    Dim dbContext As New NestoEntities
    Private testContextInstance As TestContext

    '''<summary>
    '''Obtiene o establece el contexto de las pruebas que proporciona
    '''información y funcionalidad para la serie de pruebas actual.
    '''</summary>
    Public Property TestContext() As TestContext
        Get
            Return testContextInstance
        End Get
        Set(ByVal value As TestContext)
            testContextInstance = value
        End Set
    End Property

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

    '<TestMethod()>
    'Public Sub debeContabilizarElReembolso()
    '    'arrange
    '    Dim asiento As Integer
    '    agenciaVM.envioActual = (From c In dbContext.EnviosAgencia Where c.Empresa = "1" And c.Numero = 195).FirstOrDefault
    '    'act Lo comento porque podría contabilizar
    '    asiento = agenciaVM.contabilizarReembolso(agenciaVM.envioActual)
    '    'assert
    '    Assert.IsFalse(asiento <= 0)
    'End Sub
End Class

<TestClass()>
Public Class cuandoCargamosElEstado
    Dim container As IUnityContainer
    Dim regionManager As IRegionManager
    Dim agenciaVM As New AgenciasViewModel(container, regionManager)
    Dim agenciaEspecifica As AgenciaASM = New AgenciaASM(agenciaVM)
    Private Shared DbContext As New NestoEntities

    Private testContextInstance As TestContext

    '''<summary>
    '''Obtiene o establece el contexto de las pruebas que proporciona
    '''información y funcionalidad para la serie de pruebas actual.
    '''</summary>
    Public Property TestContext() As TestContext
        Get
            Return testContextInstance
        End Get
        Set(ByVal value As TestContext)
            testContextInstance = value
        End Set
    End Property

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

    <TestMethod()>
    Public Sub debeDevolverUnValorAlXMLdeEstado()
        'arrange
        agenciaVM.envioActual = (From c In DbContext.EnviosAgencia Where c.Numero = 173).FirstOrDefault
        'agenciaVM.empresaSeleccionada = New Empresas With {.Número = "95", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        'act
        agenciaVM.XMLdeEstado = agenciaEspecifica.cargarEstado(agenciaVM.envioActual)
        'assert
        'Debug.Print(agenciaVM.XMLdeEstado.ToString)
        Assert.IsFalse(IsNothing(agenciaVM.XMLdeEstado))
    End Sub
End Class
