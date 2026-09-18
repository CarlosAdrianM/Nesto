Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels
Imports Newtonsoft.Json

''' <summary>
''' Nesto#415 / Nesto#340 (Agencias, slice A4.3): el pago de reembolsos lo contabiliza el servidor
''' (POST api/EnviosAgencias/PagarReembolsos). Aquí se fija el contrato con el endpoint: la ruta y
''' los nombres del JSON en los dos sentidos (espejo de PagoReembolsosServiceTests en NestoAPI).
''' Si un nombre deja de casar, Web API/Newtonsoft dejan la propiedad por defecto sin dar error:
''' NumerosEnvio = null en el servidor es un 400, y un Asiento 0 aquí pasaría por bueno.
''' </summary>
<TestClass()>
Public Class AgenciaServicePagoReembolsosTests

    <TestMethod()>
    Public Sub RutaPagarReembolsos_EsLaDelEndpoint()
        Assert.AreEqual("EnviosAgencias/PagarReembolsos", AgenciaService.RUTA_PAGAR_REEMBOLSOS)
    End Sub

    <TestMethod()>
    Public Sub PagoReembolsosDto_ContratoDelJson_MismosNombresQueElServidor()
        Dim peticion = GetType(PagoReembolsosDto).GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n).ToArray()
        CollectionAssert.AreEqual({"Agencia", "Cliente", "Empresa", "NumerosEnvio"}, peticion)

        Dim respuesta = GetType(ResultadoPagoReembolsosDto).GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n).ToArray()
        CollectionAssert.AreEqual({"Asiento", "Envios", "Importe", "Mensaje"}, respuesta)
    End Sub

    <TestMethod()>
    Public Sub PagoReembolsosDto_SerializaComoEsperaElServidor()
        Dim json As String = JsonConvert.SerializeObject(New PagoReembolsosDto With {
            .Empresa = "1", .Cliente = "12345", .Agencia = 12, .NumerosEnvio = New List(Of Integer) From {248001, 248002}})

        Assert.AreEqual("{""Empresa"":""1"",""Cliente"":""12345"",""Agencia"":12,""NumerosEnvio"":[248001,248002]}", json)
    End Sub

    <TestMethod()>
    Public Sub ResultadoPagoReembolsosDto_DeserializaLaRespuestaDelServidor()
        Dim dto = JsonConvert.DeserializeObject(Of ResultadoPagoReembolsosDto)("{""Asiento"":88131,""Envios"":2,""Importe"":151.50,""Mensaje"":""ok""}")

        Assert.AreEqual(88131, dto.Asiento)
        Assert.AreEqual(2, dto.Envios)
        Assert.AreEqual(151.5D, dto.Importe)
        Assert.AreEqual("ok", dto.Mensaje)
    End Sub

End Class
