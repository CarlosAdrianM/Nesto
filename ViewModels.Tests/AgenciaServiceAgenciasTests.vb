Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.ViewModels

''' <summary>
''' Nesto#340 (Agencias, slice A1): las agencias de transporte ya no se leen de Entity Framework,
''' sino de api/Agencias. El riesgo del cambio no es la lectura en sí, es la COMPARACIÓN: SQL Server
''' ignoraba el relleno de los char y las mayúsculas, y la API devuelve los campos ya recortados,
''' así que un "=" pelado en memoria dejaría de encontrar la agencia sin dar ningún error.
''' </summary>
<TestClass()>
Public Class AgenciaServiceAgenciasTests

    Private Shared Function CrearServicio(ParamArray agencias As AgenciaMantenimiento()) As AgenciaService
        Dim clienteApi = A.Fake(Of IServicioAgenciasMantenimiento)()
        A.CallTo(Function() clienteApi.LeerAgencias()).
            Returns(Task.FromResult(New List(Of AgenciaMantenimiento)(agencias)))
        Return New AgenciaService(clienteApi)
    End Function

    Private Shared Function Agencia(numero As Integer, empresa As String, nombre As String,
                                    Optional ruta As String = "", Optional cuenta As String = "") As AgenciaMantenimiento
        ' La API los devuelve SIEMPRE recortados (AgenciasTarifasController.ADto hace Trim).
        Return New AgenciaMantenimiento With {
            .Numero = numero,
            .Empresa = empresa,
            .Nombre = nombre,
            .Ruta = ruta,
            .CuentaReembolsos = cuenta,
            .Identificador = "ID",
            .PrefijoCodigoBarras = "PRE"
        }
    End Function

    <TestMethod()>
    Public Sub CargarListaAgencias_ConLaEmpresaRellenaDeEspacios_LaEncuentraIgualmente()
        ' Empresa es char(3) en la base de datos, así que los llamantes traen "1  ", no "1".
        Dim servicio = CrearServicio(Agencia(1, "1", "GLS"), Agencia(8, "3", "Correos Express"))

        Dim resultado = servicio.CargarListaAgencias("1  ")

        Assert.AreEqual(1, resultado.Count)
        Assert.AreEqual("GLS", resultado(0).Nombre)
    End Sub

    <TestMethod()>
    Public Sub CargarListaAgencias_FiltraPorEmpresa()
        Dim servicio = CrearServicio(Agencia(1, "1", "GLS"), Agencia(8, "3", "Correos Express"))

        Assert.AreEqual(1, servicio.CargarListaAgencias("3").Count)
        Assert.AreEqual("Correos Express", servicio.CargarListaAgencias("3")(0).Nombre)
    End Sub

    <TestMethod()>
    Public Sub CargarAgencia_DevuelveLaDelNumeroPedido()
        Dim servicio = CrearServicio(Agencia(1, "1", "GLS"), Agencia(8, "1", "Correos Express"))

        Assert.AreEqual("Correos Express", servicio.CargarAgencia(8).Nombre)
    End Sub

    <TestMethod()>
    Public Sub CargarAgencia_SiNoExiste_DevuelveNothing()
        Dim servicio = CrearServicio(Agencia(1, "1", "GLS"))

        Assert.IsNull(servicio.CargarAgencia(99))
    End Sub

    <TestMethod()>
    Public Sub CargarAgenciaPorRuta_ConRutaYEmpresaRellenas_LaEncuentra()
        Dim servicio = CrearServicio(Agencia(1, "1", "GLS", ruta:="16"))

        Dim resultado = servicio.CargarAgenciaPorRuta("1  ", "16 ")

        Assert.IsNotNull(resultado)
        Assert.AreEqual(1, resultado.Numero)
    End Sub

    <TestMethod()>
    Public Sub CargarAgenciaPorNombreYCuentaReembolsos_ConValoresRellenos_LaEncuentra()
        Dim servicio = CrearServicio(Agencia(1, "1", "GLS", cuenta:="5700000001"))

        Dim resultado = servicio.CargarAgenciaPorNombreYCuentaReembolsos("1  ", "5700000001 ", "GLS ")

        Assert.IsNotNull(resultado)
        Assert.AreEqual(1, resultado.Numero)
    End Sub

    <TestMethod()>
    Public Sub LasAgencias_SoloSePidenUnaVez_AunqueSeConsultenVariasVeces()
        ' Antes cada listado y cada envío insertado releía las agencias de la base de datos. Contra
        ' la API eso sería una llamada HTTP por envío, así que se cachean.
        Dim clienteApi = A.Fake(Of IServicioAgenciasMantenimiento)()
        A.CallTo(Function() clienteApi.LeerAgencias()).
            Returns(Task.FromResult(New List(Of AgenciaMantenimiento) From {Agencia(1, "1", "GLS", ruta:="16")}))
        Dim servicio As New AgenciaService(clienteApi)

        Dim unused1 = servicio.CargarListaAgencias("1")
        Dim unused2 = servicio.CargarAgencia(1)
        Dim unused3 = servicio.CargarAgenciaPorRuta("1", "16")

        A.CallTo(Function() clienteApi.LeerAgencias()).MustHaveHappenedOnceExactly()
    End Sub
End Class
