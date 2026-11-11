Imports Nesto.ViewModels

' Incidencia 1191 (Alfredo): «Borrar Envío» de un envío que nació de «Volver a enviar» un devuelto enseñaba
' «An error occurred while updating the entries. See the inner exception for details.».
<TestClass()>
Public Class ErroresBorradoEnvioTests

    Private Const GENERICO_EF As String = "An error occurred while updating the entries. See the inner exception for details."

    Private Shared Function CadenaEf(mensajeSql As String) As Exception
        Return New Exception(GENERICO_EF, New Exception(GENERICO_EF, New Exception(mensajeSql)))
    End Function

    <TestMethod()>
    Public Sub MensajeParaUsuario_ClaveAjenaDeReenvios_DiceQueNacioDeVolverAEnviar()
        Dim ex = CadenaEf("The DELETE statement conflicted with the REFERENCE constraint ""FK_EnviosAgenciaReenvios_Nuevo"". The conflict occurred in database ""NV"", table ""dbo.EnviosAgenciaReenvios"", column 'EnvioNuevo'.")

        Assert.AreEqual(ErroresBorradoEnvio.MENSAJE_REENVIO, ErroresBorradoEnvio.MensajeParaUsuario(ex))
        StringAssert.StartsWith(ErroresBorradoEnvio.MENSAJE_REENVIO, "Este envío no se puede borrar porque nació de la decisión de volver a enviar un envío devuelto.")
    End Sub

    <TestMethod()>
    Public Sub MensajeParaUsuario_ClaveAjenaDeReenviosEnElCuerpoDeLaApi_DiceQueNacioDeVolverAEnviar()
        Dim ex As New Exception("No se pudo borrar el envío 250772 (500): {""error"":{""code"":""INTERNAL_ERROR"",""message"":""... FK_EnviosAgenciaReenvios_Nuevo ...""}}")

        Assert.AreEqual(ErroresBorradoEnvio.MENSAJE_REENVIO, ErroresBorradoEnvio.MensajeParaUsuario(ex))
    End Sub

    <TestMethod()>
    Public Sub MensajeParaUsuario_OtraClaveAjena_DiceConQueTablaEstaRelacionado()
        Dim ex = CadenaEf("The DELETE statement conflicted with the REFERENCE constraint ""FK_Otra_Envio"". The conflict occurred in database ""NV"", table ""dbo.OtraTabla"", column 'Envio'.")

        Dim mensaje = ErroresBorradoEnvio.MensajeParaUsuario(ex)

        StringAssert.Contains(mensaje, "relacionado con «OtraTabla»")
        StringAssert.Contains(mensaje, "FK_Otra_Envio")
        Assert.IsFalse(mensaje.Contains("An error occurred"))
    End Sub

    <TestMethod()>
    Public Sub MensajeParaUsuario_ClaveAjenaEnCastellano_DiceConQueTablaEstaRelacionado()
        Dim ex = CadenaEf("Instrucción DELETE en conflicto con la restricción REFERENCE ""FK_Otra_Envio"". El conflicto ha aparecido en la base de datos ""NV"", tabla ""dbo.OtraTabla"", column 'Envio'.")

        StringAssert.Contains(ErroresBorradoEnvio.MensajeParaUsuario(ex), "relacionado con «OtraTabla»")
    End Sub

    <TestMethod()>
    Public Sub MensajeParaUsuario_SoloElGenericoDeEF_NoLoEnseñaEnIngles()
        ' En RELEASE la API solo devuelve el mensaje de fuera.
        Dim ex As New Exception("No se pudo borrar el envío 250772 (500): {""error"":{""code"":""INTERNAL_ERROR"",""message"":""" & GENERICO_EF & """}}")

        Assert.AreEqual(ErroresBorradoEnvio.MENSAJE_GENERICO, ErroresBorradoEnvio.MensajeParaUsuario(ex))
    End Sub

    <TestMethod()>
    Public Sub MensajeParaUsuario_OtroError_EnseñaElMensajeMasInterno()
        Dim ex As New Exception("fuera", New Exception("medio", New Exception("el de verdad")))

        Assert.AreEqual("el de verdad", ErroresBorradoEnvio.MensajeParaUsuario(ex))
    End Sub

    <TestMethod()>
    Public Sub ExcepcionMasInterna_SinInterna_DevuelveLaMisma()
        Dim ex As New Exception("sola")

        Assert.AreSame(ex, ErroresBorradoEnvio.ExcepcionMasInterna(ex))
        Assert.IsNull(ErroresBorradoEnvio.ExcepcionMasInterna(Nothing))
    End Sub
End Class
