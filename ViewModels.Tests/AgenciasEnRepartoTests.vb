Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Models.Nesto.Models
Imports Nesto.ViewModels
Imports Newtonsoft.Json

''' <summary>
''' NestoAPI#516: en la pestaña Tramitados se ve qué envíos ha sacado ya la agencia a reparto. El dato lo
''' calcula la API (EnvioAgenciaListadoDTO.EnReparto, derivado de DetalleEstado); Nesto solo lo muestra.
''' </summary>
<TestClass()>
Public Class AgenciasEnRepartoTests

    Private Shared Function DesdeJson(json As String) As EnviosAgencia
        Dim dto = JsonConvert.DeserializeObject(Of List(Of EnvioAgenciaListadoDTO))(json)(0)
        Return AgenciaService.AEnvioAgencia(dto, New Dictionary(Of String, AgenciasTransporte))
    End Function

    <TestMethod()>
    Public Sub Listado_ConEnRepartoDeLaApi_LlegaAlEnvio()
        Dim envio = DesdeJson("[{""Numero"":1,""Empresa"":""1"",""Agencia"":11,""Estado"":1,""DetalleEstado"":""REPARTO"",""EnReparto"":true}]")

        Assert.IsTrue(envio.EnReparto)
        Assert.AreEqual("REPARTO", envio.DetalleEstado)
        Assert.AreEqual(CShort(1), envio.Estado, "No es un estado nuevo: sigue tramitado")
    End Sub

    <TestMethod()>
    Public Sub Listado_DeUnaApiAnteriorSinEnReparto_QuedaEnFalse()
        Dim envio = DesdeJson("[{""Numero"":1,""Empresa"":""1"",""Agencia"":1,""Estado"":1,""DetalleEstado"":""EN REPARTO""}]")

        Assert.IsFalse(envio.EnReparto)
    End Sub

    <TestMethod()>
    Public Sub EnReparto_NoViajaDeVueltaALaApi()
        Dim json = JsonConvert.SerializeObject(New EnviosAgencia With {.Numero = 1, .EnReparto = True})

        Assert.IsFalse(json.Contains("EnReparto"), json)
    End Sub

    <TestMethod()>
    Public Sub CabeceraTramitados_CuentaLosQueEstanEnReparto()
        Assert.AreEqual("Tramitados", AgenciasViewModel.TextoCabeceraTramitados(Nothing))
        Assert.AreEqual("Tramitados", AgenciasViewModel.TextoCabeceraTramitados(New List(Of EnviosAgencia) From {
            New EnviosAgencia With {.Estado = 1}, New EnviosAgencia With {.Estado = 2}}))
        Assert.AreEqual("Tramitados (2 en reparto)", AgenciasViewModel.TextoCabeceraTramitados(New List(Of EnviosAgencia) From {
            New EnviosAgencia With {.Estado = 1, .EnReparto = True},
            New EnviosAgencia With {.Estado = 1},
            New EnviosAgencia With {.Estado = 1, .EnReparto = True}}))
    End Sub

End Class
