Imports System.Threading.Tasks
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Models.Nesto.Models
Imports Nesto.ViewModels

' NestoAPI#493: CTT Express hereda TODO el flujo "registrar al imprimir" de AgenciaGestionadaPorApi
' (la misma base que Innovatrans). Aquí se verifica lo que la distingue y que la base firma las
' respuestas con el nombre de la agencia concreta (es lo que va a la auditoría).
<TestClass()>
Public Class AgenciaCTTTests

    Private Shared Function CrearAgencia() As AgenciaCTT
        Return New AgenciaCTT()
    End Function

    <TestMethod()>
    Public Sub CTT_FlujoEsRegistrarAlImprimir()
        Assert.AreEqual(TipoFlujoTramitacion.RegistrarAlImprimir, CrearAgencia().FlujoTramitacion)
    End Sub

    <TestMethod()>
    Public Sub CTT_ImplementaGestionRemota()
        Assert.IsInstanceOfType(CrearAgencia(), GetType(IAgenciaConGestionRemota))
    End Sub

    <TestMethod()>
    Public Sub CTT_IdentidadYPlaza()
        Dim agencia = CrearAgencia()
        Assert.AreEqual("CTT", agencia.NombreAgencia)
        Assert.AreEqual(13, agencia.AgenciaId)
        Assert.AreEqual(13, agencia.ListaServicios.Single().AgenciaId, "La tarifa placeholder lleva el id de la agencia")

        Dim nemonico As String = Nothing, nombrePlaza As String = Nothing, telefono As String = Nothing, email As String = Nothing
        agencia.calcularPlaza("28001", 34, nemonico, nombrePlaza, telefono, email)
        Assert.AreEqual("CT", nemonico)
        Assert.AreEqual("CTT", nombrePlaza)
    End Sub

    <TestMethod()>
    Public Sub CTT_NoPermiteEditarCodigoBarrasNiExigeDimensiones()
        Assert.IsFalse(CrearAgencia().PermiteEditarCodigoBarras)
        Assert.IsFalse(CrearAgencia().DimensionesBultosObligatorias)
        Assert.AreEqual(34, CrearAgencia().paisDefecto)
    End Sub

    <TestMethod()>
    Public Sub CTT_SinPortalDeSeguimientoTodavia_EnlaceVacio()
        Dim envio As New EnviosAgencia With {.CodigoBarras = "CTT0001"}
        Assert.AreEqual(String.Empty, DirectCast(CrearAgencia(), IAgencia).EnlaceSeguimiento(envio))
    End Sub

    <TestMethod()>
    Public Async Function AnularEnAgencia_LaRespuestaVaFirmadaPorCTT() As Task
        ' La base usa NombreAgencia para RespuestaAgencia.Agencia: es lo que distingue en la auditoría
        ' un envío de CTT de uno de Innovatrans.
        Dim servicio = A.Fake(Of IAgenciaService)()
        A.CallTo(Function() servicio.AnularEnvioRemoto(1)).Returns(Task.CompletedTask)
        Dim envio As New EnviosAgencia With {.Numero = 1, .CodigoBarras = "CTT0001", .Estado = 0}

        Dim respuesta = Await CrearAgencia().AnularEnAgencia(envio, servicio)

        Assert.IsTrue(respuesta.Exito)
        Assert.AreEqual("CTT", respuesta.Agencia)
        Assert.AreEqual(String.Empty, envio.CodigoBarras)
        Assert.AreEqual(CShort(-1), envio.Estado)
    End Function

    <TestMethod()>
    Public Async Function InsertarYEtiquetar_SinEtiqueta_ElErrorNombraACTT() As Task
        Dim servicio = A.Fake(Of IAgenciaService)()
        A.CallTo(Function() servicio.TramitarEnvioRemoto(1)) _
            .Returns(Task.FromResult(New TramitarEnvioResultadoDto With {.Albaran = "CTT0001", .Bultos = 1}))
        Dim envio As New EnviosAgencia With {.Numero = 1, .Estado = 0}

        Dim respuesta = Await CrearAgencia().InsertarYEtiquetar(envio, servicio)

        Assert.IsFalse(respuesta.Exito)
        StringAssert.Contains(respuesta.TextoRespuestaError, "CTT no devolvió la etiqueta")
        Assert.AreEqual("CTT0001", envio.CodigoBarras, "El albarán asignado por la plataforma se conserva aunque falle la etiqueta")
    End Function

End Class
