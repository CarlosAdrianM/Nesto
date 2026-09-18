Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels
Imports Newtonsoft.Json

''' <summary>
''' Nesto#340 (Agencias, slice A4.2): la recepción del retorno la estampa el servidor
''' (POST api/EnviosAgencias/{id}/RecibirRetorno). Aquí se fija el contrato con el endpoint: la ruta
''' y los nombres del JSON de respuesta (espejo de RecibirRetornoTests en NestoAPI). Si los nombres
''' dejan de casar, Newtonsoft deja la fecha en 01/01/0001 sin dar ningún error.
''' </summary>
<TestClass()>
Public Class AgenciaServiceRetornosTests

    <TestMethod()>
    Public Sub RutaRecibirRetorno_EsLaDelEndpoint()
        Assert.AreEqual("EnviosAgencias/248001/RecibirRetorno", AgenciaService.RutaRecibirRetorno(248001))
    End Sub

    <TestMethod()>
    Public Sub RetornoRecibidoDto_ContratoDelJson_MismosNombresQueElServidor()
        Dim propiedades = GetType(RetornoRecibidoDto).GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n).ToArray()
        CollectionAssert.AreEqual({"FechaRetornoRecibido", "Numero", "Pedido"}, propiedades)
    End Sub

    <TestMethod()>
    Public Sub RetornoRecibidoDto_DeserializaLaRespuestaDelServidor()
        Dim dto = JsonConvert.DeserializeObject(Of RetornoRecibidoDto)("{""Numero"":248001,""Pedido"":925100,""FechaRetornoRecibido"":""2026-09-18T00:00:00""}")

        Assert.AreEqual(248001, dto.Numero)
        Assert.AreEqual(925100, dto.Pedido)
        Assert.AreEqual(New Date(2026, 9, 18), dto.FechaRetornoRecibido)
    End Sub

End Class
