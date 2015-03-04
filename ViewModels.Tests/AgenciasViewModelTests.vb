Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels
Imports Nesto.Models


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
    Dim agenciaVM As New AgenciasViewModel

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
Public Class cuandoConstruimosElXMLdeSalida
    Dim agenciaVM As New AgenciasViewModel
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
    Public Sub losDatosDelEnvioDebenSerLosCorrectos()
        'arrange
        Dim identificador As String
        Dim retorno As Integer
        Dim refC As String
        Dim reembolso As String
        Dim bultos As Integer
        Dim servicio As Integer
        Dim horario As Integer
        agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM"}
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        'agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 558929).FirstOrDefault
        'agenciaVM.retornoActual = New AgenciasViewModel.tipoIdDescripcion With {.id = 2}
        'agenciaVM.bultos = 3
        'agenciaVM.servicioActual = New AgenciasViewModel.tipoIdDescripcion With {.id = 54}
        'agenciaVM.horarioActual = New AgenciasViewModel.tipoIdDescripcion With {.id = 18}

        agenciaVM.envioActual = New EnviosAgencia With {
            .Agencia = "1",
            .AgenciasTransporte = agenciaVM.agenciaSeleccionada,
            .Atencion = "Carlos",
            .Bultos = 2,
            .Cliente = "992",
            .CodigoBarras = "12345678901234",
            .CodPostal = "08080",
            .Contacto = "0",
            .Direccion = "Paseo de la Habana, 82",
            .Email = "micorreo@midominio.com",
            .EmailPlaza = "sucorreo@asm.com",
            .Empresa = "1",
            .Empresas = agenciaVM.empresaSeleccionada,
            .Estado = 0,
            .Fecha = "25/09/14",
            .Horario = 18,
            .Movil = "612345678",
            .Nemonico = "A13",
            .Nombre = "PEPITO GRILLO DISNEY",
            .NombrePlaza = "SUPER AGENCIA",
            .Pedido = 123456,
            .Poblacion = "EL PRAT DE LLOBREGAT",
            .Reembolso = 100,
            .Retorno = 2,
            .Servicio = 54,
            .Telefono = "912345678",
            .TelefonoPlaza = "987654321",
        .Vendedor = "NV"
        }

        Dim ns As XNamespace = "http://www.asmred.com/"
        'act
        agenciaVM.XMLdeSalida = agenciaEspecifica.construirXMLdeSalida()
        identificador = agenciaVM.XMLdeSalida.Root.Attribute("uidcliente").Value
        retorno = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Retorno").Value
        refC = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Referencias").Elements(ns + "Referencia").Where(Function(p)
                                                                                                                                                       Return p.Attribute("tipo").Value = "C"
                                                                                                                                                   End Function).Value
        reembolso = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Importes").Element(ns + "Reembolso").Value
        bultos = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Bultos").Value
        servicio = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Servicio").Value
        horario = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Horario").Value
        'Debug.Print(agenciaVM.XMLdeSalida.ToString)
        'assert
        Assert.IsTrue(identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11") 'el de pruebas de ASM
        Assert.IsTrue(retorno = 2)
        Assert.IsTrue(refC = "992/123456")
        Assert.IsTrue(reembolso = "100")
        Assert.IsTrue(bultos = 2)
        Assert.IsTrue(servicio = 54)
        Assert.IsTrue(horario = 18)
    End Sub

    <TestMethod()>
    Public Sub losDatosDeLaEmpresaDebenSerCorrectos()
        'arrange
        Dim nombre As String
        Dim direccion As String
        Dim poblacion As String
        Dim provincia As String
        Dim pais As String
        Dim codPostal As String
        Dim telefono As String
        Dim EMail As String
        agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM"}
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        'agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 558935).FirstOrDefault
        agenciaVM.envioActual = New EnviosAgencia With {
            .Agencia = "1",
            .AgenciasTransporte = agenciaVM.agenciaSeleccionada,
            .Atencion = "Carlos",
            .Bultos = 2,
            .Cliente = "992",
            .CodigoBarras = "12345678901234",
            .CodPostal = "08080",
            .Contacto = "0",
            .Direccion = "Paseo de la Habana, 82",
            .Email = "micorreo@midominio.com",
            .EmailPlaza = "sucorreo@asm.com",
            .Empresa = "1",
            .Empresas = agenciaVM.empresaSeleccionada,
            .Estado = 0,
            .Fecha = "25/09/14",
            .Horario = 18,
            .Movil = "612345678",
            .Nemonico = "A13",
            .Nombre = "PEPITO GRILLO DISNEY",
            .NombrePlaza = "SUPER AGENCIA",
            .Pedido = 123456,
            .Poblacion = "EL PRAT DE LLOBREGAT",
            .Reembolso = 100,
            .Retorno = 2,
            .Servicio = 54,
            .Telefono = "912345678",
            .TelefonoPlaza = "987654321",
        .Vendedor = "NV"
        }
        Dim ns As XNamespace = "http://www.asmred.com/"
        'act
        agenciaVM.XMLdeSalida = agenciaEspecifica.construirXMLdeSalida()
        nombre = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Remite").Element(ns + "Nombre").Value
        direccion = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Remite").Element(ns + "Direccion").Value
        poblacion = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Remite").Element(ns + "Poblacion").Value
        provincia = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Remite").Element(ns + "Provincia").Value
        pais = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Remite").Element(ns + "Pais").Value
        codPostal = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Remite").Element(ns + "CP").Value
        telefono = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Remite").Element(ns + "Telefono").Value
        EMail = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Remite").Element(ns + "Email").Value
        'assert
        Assert.IsTrue(nombre = "Empresa de Pruebas")
        Assert.IsTrue(direccion = "c/ Mi Calle, 1")
        Assert.IsTrue(poblacion = "Ripollet")
        Assert.IsTrue(provincia = "Barcelona")
        Assert.IsTrue(pais = "34") 'este es constante
        Assert.IsTrue(codPostal = "08001")
        Assert.IsTrue(telefono = "916233343")
        Assert.IsTrue(EMail = "carlos@midominio.com")
    End Sub

    <TestMethod()>
    Public Sub losDatosDelClienteDebenSerCorrectos()
        'arrange
        Dim nombre As String
        Dim direccion As String
        Dim poblacion As String
        Dim provincia As String
        Dim pais As String
        Dim codPostal As String
        Dim telefonoFijo As String
        Dim telefonoMovil As String
        Dim Email As String
        Dim comentarios As String
        Dim att As String
        agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM"}
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "95", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        'agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 538601).FirstOrDefault
        agenciaVM.envioActual = New EnviosAgencia With {
            .Agencia = "1",
            .AgenciasTransporte = agenciaVM.agenciaSeleccionada,
            .Atencion = "Carlos",
            .Bultos = 2,
            .Cliente = "992",
            .CodigoBarras = "12345678901234",
            .CodPostal = "08080",
            .Contacto = "0",
            .Direccion = "Paseo de la Habana, 82",
            .Email = "micorreo@midominio.com",
            .EmailPlaza = "sucorreo@asm.com",
            .Empresa = "1",
            .Empresas = agenciaVM.empresaSeleccionada,
            .Estado = 0,
            .Fecha = "25/09/14",
            .Horario = 18,
            .Movil = "612345678",
            .Nemonico = "A13",
            .Nombre = "PEPITO GRILLO DISNEY",
            .NombrePlaza = "SUPER AGENCIA",
            .Observaciones = "Comentarios del pedido",
            .Pedido = 123456,
            .Poblacion = "EL PRAT DE LLOBREGAT",
            .Provincia = "MADRID",
            .Reembolso = 100,
            .Retorno = 2,
            .Servicio = 54,
            .Telefono = "912345678",
            .TelefonoPlaza = "987654321",
        .Vendedor = "NV"
        }
        Dim ns As XNamespace = "http://www.asmred.com/"
        'act
        agenciaVM.XMLdeSalida = agenciaEspecifica.construirXMLdeSalida()
        nombre = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "Nombre").Value
        direccion = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "Direccion").Value
        poblacion = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "Poblacion").Value
        provincia = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "Provincia").Value
        pais = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "Pais").Value
        codPostal = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "CP").Value
        telefonoFijo = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "Telefono").Value
        telefonoMovil = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "Movil").Value
        Email = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "Email").Value
        comentarios = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "Observaciones").Value
        att = agenciaVM.XMLdeSalida.Element(ns + "Servicios").Element(ns + "Envio").Element(ns + "Destinatario").Element(ns + "ATT").Value
        'assert
        Assert.IsTrue(nombre = "PEPITO GRILLO DISNEY")
        Assert.IsTrue(direccion = "Paseo de la Habana, 82")
        Assert.IsTrue(poblacion = "EL PRAT DE LLOBREGAT")
        Assert.IsTrue(provincia = "MADRID")
        Assert.IsTrue(pais = "34")
        Assert.IsTrue(codPostal = "08080")
        Assert.IsTrue(telefonoFijo = "912345678")
        Assert.IsTrue(telefonoMovil = "612345678")
        Assert.IsTrue(Email = "micorreo@midominio.com")
        Assert.IsTrue(comentarios = "Comentarios del pedido")
        Assert.IsTrue(att = "Carlos")
    End Sub

    <TestMethod()>
    Public Sub elImporteDelReembolsoDebeSerElTotal()
        'arrange
        Dim reembolso As Decimal
        agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM"}
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "95", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 558932).FirstOrDefault

        Dim ns As XNamespace = "http://www.asmred.com/"
        'act
        reembolso = agenciaVM.importeReembolso
        'assert
        Assert.IsTrue(reembolso = 112.88)
    End Sub

    <TestMethod()>
    Public Sub elImporteDebeSerCeroSiTieneCCC()
        'arrange
        Dim reembolso As String
        agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM"}
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "95", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 492244).FirstOrDefault
        Dim ns As XNamespace = "http://www.asmred.com/"
        'act
        'agenciaVM.XMLdeSalida = agenciaVM.construirXMLdeSalida()
        reembolso = agenciaVM.importeReembolso
        'assert
        Assert.IsTrue(reembolso = 0)
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
    Dim agenciaVM As New AgenciasViewModel
    Private Shared DbContext As New NestoEntities

    <TestMethod()>
    Public Sub debeGuardarElRegistroEnLaBBDD()
        'arrange
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 561372).FirstOrDefault
        agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM", .PrefijoCodigoBarras = "6112979"}
        agenciaVM.bultos = 2
        'act
        'agenciaVM.cmdImprimirEtiquetaPedido.Execute(Nothing)
        agenciaVM.insertarRegistro()
        'assert
        Assert.IsTrue(agenciaVM.envioActual.CodigoBarras = "61129790561372")
        Assert.IsTrue(agenciaVM.envioActual.Nemonico = "A63")
        Assert.IsTrue(agenciaVM.envioActual.NombrePlaza = "RETIRO 797")
        Assert.IsTrue(agenciaVM.envioActual.TelefonoPlaza = "915744174")
        Assert.IsTrue(agenciaVM.envioActual.EmailPlaza = "asm.retiro@asmred.com")
    End Sub

    <TestMethod()>
    Public Sub debeGuardarElRegistroEnLaBBDD2()
        'arrange
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com"}
        agenciaVM.pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = 563148).FirstOrDefault
        agenciaVM.agenciaSeleccionada = New AgenciasTransporte With {.Empresa = "1", .Numero = "1", .Identificador = "6BAB7A53-3B6D-4D5A-9450-702D2FAC0B11", .Nombre = "ASM", .PrefijoCodigoBarras = "6112979"}
        agenciaVM.bultos = 2
        'act
        'agenciaVM.cmdImprimirEtiquetaPedido.Execute(Nothing)
        agenciaVM.insertarRegistro()
        'assert
        Assert.IsTrue(agenciaVM.envioActual.CodigoBarras = "61129790563148")
        'Assert.IsTrue(agenciaVM.envioActual.Nemonico = "A42")
        'Assert.IsTrue(agenciaVM.envioActual.NombrePlaza = "ASM NAVALCARNERO")
        'Assert.IsTrue(agenciaVM.envioActual.TelefonoPlaza = "918134517")
        'Assert.IsTrue(agenciaVM.envioActual.EmailPlaza = "asm.703@asmred.es")
    End Sub

    <TestMethod()>
    Public Sub debeBuscarElMultiusuario()
        'arrange
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        agenciaVM.numeroMultiusuario = 25
        'act

        'assert
        Assert.IsTrue(agenciaVM.multiusuario.Nombre.Trim = "Pedro Jurado")
    End Sub
End Class

<TestClass()>
Public Class cuandoCambiamosElNumeroDePedido
    Dim agenciaVM As New AgenciasViewModel
    Private Shared DbContext As New NestoEntities
    <TestMethod()>
    Public Sub debeBuscarElPedido()
        'arrange
        agenciaVM.empresaSeleccionada = New Empresas With {.Número = "1", .Nombre = "Empresa de Pruebas", .Dirección = "c/ Mi Calle, 1", .Población = "Ripollet", .Provincia = "Barcelona", .CodPostal = "08001", .Teléfono = "916233343", .Email = "carlos@midominio.com", .FechaPicking = "21/08/2014"}
        agenciaVM.numeroPedido = 523418
        'act

        'assert
        Assert.IsTrue(agenciaVM.direccionEnvio = "C/ TURQUÍA, 9 - LOCAL")
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

    <TestMethod()>
    Public Sub debeContabilizarElReembolso()
        'arrange
        Dim asiento As Integer
        agenciaVM.envioActual = (From c In dbContext.EnviosAgencia Where c.Empresa = "1" And c.Numero = 195).FirstOrDefault
        'act Lo comento porque podría contabilizar
        asiento = agenciaVM.contabilizarReembolso(agenciaVM.envioActual)
        'assert
        Assert.IsFalse(asiento <= 0)
    End Sub
End Class

<TestClass()>
Public Class cuandoCargamosElEstado

    Dim agenciaVM As New AgenciasViewModel
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
        Debug.Print(agenciaVM.XMLdeEstado.ToString)
        Assert.IsFalse(IsNothing(agenciaVM.XMLdeEntrada))
    End Sub
End Class
