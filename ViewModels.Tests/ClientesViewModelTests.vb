Imports Nesto.ViewModels
Imports Nesto.Models
'Imports System.Text

<TestClass()>
Public Class CuandoCargamosUnCliente

    Dim clienteVM As New ClientesViewModel


    Private testContextInstance As TestContext

    '''<summary>
    '''Obtiene o establece el contexto de las pruebas que proporciona
    '''información y funcionalidad para la ejecución de pruebas actual.
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
    'Public Sub ElNombreDebeCoincidir()
    '    'arrange
    '    clienteVM.empresaActual = "1"
    '    clienteVM.clienteActual = "15191"
    '    clienteVM.contactoActual = "0"
    '    'act

    '    'assert
    '    Assert.IsTrue(clienteVM.nombre.Trim = "CENTRO DE ESTÉTICA EL EDÉN, S.L.U.")
    'End Sub

    '<TestMethod()>
    'Public Sub LaCuentaDebeCoincidir()
    '    'arrange
    '    clienteVM.empresaActual = "1"
    '    clienteVM.clienteActual = "15191"
    '    clienteVM.contactoActual = "0"
    '    'act
    '    'assert
    '    Assert.IsTrue(clienteVM.cuentaActiva.Nº_Cuenta = "0200080296")
    'End Sub

    '<TestMethod()>
    'Public Sub DebeTenerTresCuentasBanco()
    '    'arrange
    '    clienteVM.empresaActual = "1"
    '    clienteVM.clienteActual = "15191"
    '    clienteVM.contactoActual = "0"
    '    'act
    '    'assert
    '    Assert.IsTrue(clienteVM.cuentasBanco.Count = 3)
    'End Sub

    '<TestMethod()>
    'Public Sub CuentaActivaEsTres()
    '    'arrange
    '    clienteVM.empresaActual = "1"
    '    clienteVM.clienteActual = "15191"
    '    clienteVM.contactoActual = "0"
    '    'act
    '    'assert
    '    Assert.IsTrue(clienteVM.cuentaActiva.Número = "3  ")
    'End Sub


End Class

'<TestClass()>
'Public Class CuandoCargamosLasEmpresas
'    Dim clienteVM As New ClientesViewModel

'    <TestMethod()>
'    Public Sub DebeHaberSeis()
'        'arrange
'        'act
'        'assert
'        Assert.IsTrue(clienteVM.listaEmpresas.Count = 6)
'    End Sub

'End Class