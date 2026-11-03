Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Models.Nesto.Models
Imports Nesto.ViewModels

''' <summary>
''' Nesto#603 / NestoAPI#785: la respuesta del PUT api/EnviosAgencias/{id} (espejo de EnviosAgenciasController.PutEnviosAgencia):
''' 200 { RowVersion, Reembolso, Avisos }, 204 sin cuerpo (API anterior), 409 { Message, Mensaje, Codigo = ReembolsoCeroContraReembolso }
''' y 409 CAMBIO_SIN_AGENCIA (NestoAPI#725).
''' </summary>
<TestClass()>
Public Class AgenciaServiceModificarRespuestaTests
    Private Const MENSAJE_409 As String = "El pedido es contra reembolso y la etiqueta lleva 0 € (envío 250432): la factura tiene 134,16 € pendientes de cobro. Ponga el reembolso en la etiqueta pendiente antes de imprimirla."
    Private Const ROW_VERSION As String = "AAAAAAAJ+Ok="

    Private Shared Function Envio() As EnviosAgencia
        Return New EnviosAgencia With {.Numero = 250432, .Reembolso = 0D, .RowVersion = New Byte() {1, 2, 3}}
    End Function

    <TestMethod()>
    Public Sub Respuesta200_RefrescaRowVersionYReembolsoYDevuelveLosAvisos()
        Dim e = Envio()
        Dim cuerpo = "{""RowVersion"":""" & ROW_VERSION & """,""Reembolso"":134.16,""Avisos"":[""La etiqueta pendiente 250432 se creó con reembolso 0,00 € antes de que el pedido tuviera picking: se ha recalculado a 134,16 €.""]}"

        Dim avisos = AgenciaService.LeerRespuestaModificar(e, 200, cuerpo)

        Assert.AreEqual(134.16D, e.Reembolso)
        CollectionAssert.AreEqual(Convert.FromBase64String(ROW_VERSION), e.RowVersion)
        Assert.AreEqual(1, avisos.Count)
        StringAssert.Contains(avisos(0), "134,16 €")
    End Sub

    <TestMethod()>
    Public Sub Respuesta200_SinAvisos_ListaVacia()
        Dim e = Envio()

        Dim avisos = AgenciaService.LeerRespuestaModificar(e, 200, "{""RowVersion"":""" & ROW_VERSION & """,""Reembolso"":25.5,""Avisos"":[]}")

        Assert.AreEqual(25.5D, e.Reembolso)
        Assert.AreEqual(0, avisos.Count)
    End Sub

    <TestMethod()>
    Public Sub Respuesta200_DeUnaApiSinReembolso_NoTocaElReembolso()
        ' Nesto#340 (A2): la API de antes de NestoAPI#785 solo devolvía la RowVersion.
        Dim e = Envio()
        e.Reembolso = 50D

        Dim avisos = AgenciaService.LeerRespuestaModificar(e, 200, "{""RowVersion"":""" & ROW_VERSION & """}")

        Assert.AreEqual(50D, e.Reembolso)
        CollectionAssert.AreEqual(Convert.FromBase64String(ROW_VERSION), e.RowVersion)
        Assert.AreEqual(0, avisos.Count)
    End Sub

    <TestMethod()>
    Public Sub Respuesta204SinCuerpo_ComoSiempre()
        Dim e = Envio()
        e.Reembolso = 50D

        Dim avisos = AgenciaService.LeerRespuestaModificar(e, 204, "")

        Assert.AreEqual(50D, e.Reembolso)
        CollectionAssert.AreEqual(New Byte() {1, 2, 3}, e.RowVersion)
        Assert.AreEqual(0, avisos.Count)
    End Sub

    <TestMethod()>
    Public Sub Respuesta409ReembolsoCero_LanzaConSoloElMessage()
        Dim e = Envio()
        Dim cuerpo = "{""Message"":""" & MENSAJE_409 & """,""Mensaje"":""" & MENSAJE_409 & """,""Codigo"":""ReembolsoCeroContraReembolso""}"

        Dim ex = Assert.ThrowsException(Of ReembolsoCeroContraReembolsoException)(Function() AgenciaService.LeerRespuestaModificar(e, 409, cuerpo))

        Assert.AreEqual(MENSAJE_409, ex.Message, "Solo el texto de la API, sin el JSON")
        Assert.AreEqual(250432, ex.NumeroEnvio)
        CollectionAssert.AreEqual(New Byte() {1, 2, 3}, e.RowVersion, "No se ha guardado nada")
    End Sub

    <TestMethod()>
    Public Sub Respuesta409CambioSinAgencia_SigueSiendoLaPregunta()
        Dim cuerpo = "{""Message"":""x"",""Mensaje"":""x"",""Codigo"":""CAMBIO_SIN_AGENCIA"",""Campos"":[""Retorno""]}"

        Assert.ThrowsException(Of CambioSinAgenciaException)(Function() AgenciaService.LeerRespuestaModificar(Envio(), 409, cuerpo))
    End Sub

    <TestMethod()>
    Public Sub OtroError_ExcepcionConElEstadoYElCuerpo()
        Dim ex = Assert.ThrowsException(Of Exception)(Function() AgenciaService.LeerRespuestaModificar(Envio(), 500, "boom"))

        StringAssert.Contains(ex.Message, "(500)")
    End Sub
End Class
