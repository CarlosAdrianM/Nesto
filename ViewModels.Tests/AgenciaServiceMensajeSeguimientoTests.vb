Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels

' NestoAPI#602: si CTT corta las consultas de seguimiento por cupo (429), «Actualizar estado» enseña el mensaje que manda
' la API para el usuario, no el JSON en bruto.
<TestClass()>
Public Class AgenciaServiceMensajeSeguimientoTests

    <TestMethod()>
    Public Sub CupoAgotado_EnsenaSoloElMensajeDeLaApi()
        Dim cuerpo = "{""Message"":""CTT limita las consultas de seguimiento y ahora mismo no deja hacer más; vuelve a intentarlo en 5 minutos."",""Codigo"":""CUPO_AGENCIA_AGOTADO"",""ReintentarEnMinutos"":5}"
        Assert.AreEqual("CTT limita las consultas de seguimiento y ahora mismo no deja hacer más; vuelve a intentarlo en 5 minutos.",
                        AgenciaService.MensajeErrorSeguimiento(429, cuerpo))
    End Sub

    <TestMethod()>
    Public Sub OtroErrorConMensaje_SeEnsenaComoSiempre()
        Dim cuerpo = "{""Message"":""Error interno""}"
        Assert.AreEqual("No se pudo actualizar el seguimiento (500): " & cuerpo, AgenciaService.MensajeErrorSeguimiento(500, cuerpo))
    End Sub

    <TestMethod()>
    Public Sub CuerpoQueNoEsJson_SeEnsenaEnBruto()
        Assert.AreEqual("No se pudo actualizar el seguimiento (502): Bad Gateway", AgenciaService.MensajeErrorSeguimiento(502, "Bad Gateway"))
    End Sub

End Class
