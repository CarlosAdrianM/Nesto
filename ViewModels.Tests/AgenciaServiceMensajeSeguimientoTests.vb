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

    ' Sugerencia 507 (Alfredo, 06/10/26): con CTT caído, al tramitar/imprimir la etiqueta solo se veía «HTTP 500».

    <TestMethod()>
    Public Sub Tramitacion_AgenciaNoResponde_EnsenaSoloElMensajeDeLaApi()
        Dim cuerpo = "{""Message"":""CTT no responde ahora mismo (error de su servidor). Vuelve a intentarlo en unos minutos o, si es urgente, sácalo por GLS."",""Codigo"":""AGENCIA_NO_RESPONDE"",""Detalle"":""CTT (Insertar) respondió HTTP 500: CONNECTIVITY""}"
        Assert.AreEqual("CTT no responde ahora mismo (error de su servidor). Vuelve a intentarlo en unos minutos o, si es urgente, sácalo por GLS.",
                        AgenciaService.MensajeErrorTramitacion(502, cuerpo))
    End Sub

    <TestMethod()>
    Public Sub Tramitacion_OtroError_SeEnsenaComoSiempre()
        Dim cuerpo = """CTT aceptó el envío pero la respuesta no trae shipping_code"""
        Assert.AreEqual("NestoAPI rechazó la tramitación (502): " & cuerpo, AgenciaService.MensajeErrorTramitacion(502, cuerpo))
    End Sub

    <TestMethod()>
    Public Sub Tramitacion_CupoAgotado_NoEsDeTramitarYSeEnsenaComoSiempre()
        Dim cuerpo = "{""Message"":""CTT limita las consultas"",""Codigo"":""CUPO_AGENCIA_AGOTADO""}"
        Assert.AreEqual("NestoAPI rechazó la tramitación (429): " & cuerpo, AgenciaService.MensajeErrorTramitacion(429, cuerpo))
    End Sub

End Class
