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

    <TestMethod()>
    Public Sub LasAgencias_LlevanLaEmpresaConElRellenoDelCharDeLaBD()
        ' Regresión del 28/08/2026. La API devuelve Empresa recortada ("1"), pero el resto de Nesto
        ' la compara con "=" pelado contra otros char(3) que siguen viniendo de Entity Framework con
        ' su relleno: CabPedidoVta.Empresa, Empresas.Número... Al día siguiente de empezar a leer las
        ' agencias de la API, imprimir CUALQUIER etiqueta reventaba en ConfigurarAgenciaPedido:
        '
        '     listaAgencias.Single(Function(a) a.Empresa = pedidoSeleccionado.Empresa AndAlso ...)
        '     -> "Sequence contains no matching element"   ("1" <> "1  ")
        '
        ' Y dos FirstOrDefault del mismo estilo fallaban CALLADOS, sin dar ningún error.
        Dim servicio = CrearServicio(Agencia(1, "1", "GLS"))

        Dim agenciaLeida = servicio.CargarAgencia(1)

        Assert.AreEqual("1  ", agenciaLeida.Empresa,
            "La entidad tiene que llevar el mismo relleno que cuando la leía Entity Framework")
    End Sub

    <TestMethod()>
    Public Sub LasAgencias_ConLaEmpresaRellena_CasanConUnaComparacionPelada()
        ' El caso real: el "=" que hay en AgenciasViewModel comparando contra CabPedidoVta.Empresa.
        Dim servicio = CrearServicio(Agencia(1, "1", "GLS"), Agencia(8, "3", "Correos Express"))
        Dim empresaDelPedido As String = "1  "        ' char(3), tal cual llega de EF

        Dim agencias = servicio.CargarListaAgencias(empresaDelPedido)
        Dim encontrada = agencias.SingleOrDefault(Function(a) a.Empresa = empresaDelPedido AndAlso a.Numero = 1)

        Assert.IsNotNull(encontrada, "Con la empresa recortada este Single no encontraba nada y reventaba")
        Assert.AreEqual("GLS", encontrada.Nombre)
    End Sub

    <TestMethod()>
    Public Sub LasAgencias_SinEmpresa_NoSeInventanEspacios()
        Dim servicio = CrearServicio(Agencia(1, Nothing, "GLS"))

        Assert.IsNull(servicio.CargarAgencia(1).Empresa)
    End Sub

End Class
