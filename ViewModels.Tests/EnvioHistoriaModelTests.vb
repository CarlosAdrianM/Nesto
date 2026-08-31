Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels
Imports Newtonsoft.Json

''' <summary>
''' Nesto#340 (slice A3): el contrato con GET api/EnviosAgencias/{id}/Historia.
'''
''' La rejilla del historial de un envio no se valida sola: si un nombre del JSON deja de casar,
''' Newtonsoft deja la propiedad a Nothing y la columna sale EN BLANCO. Una columna vacia en un
''' historial parece un dato que no se guardo, no un fallo de mapeo, asi que nadie lo reporta
''' como bug: se asume que el envio no tenia ese dato.
''' </summary>
<TestClass()>
Public Class EnvioHistoriaModelTests

    ' JSON tal y como lo serializa el endpoint (camelCase).
    Private Const JSON_FILA As String = "{""numero"":7,""numeroEnvio"":248142,""campo"":""Reembolso"",""valorAnterior"":""142,05"",""observaciones"":""Lo cambia el cliente"",""usuario"":""NUEVAVISION\\Alfredo"",""fechaModificacion"":""2026-08-31T12:04:00""}"

    <TestMethod()>
    Public Sub EnvioHistoriaModel_DelJsonDelEndpoint_MapeaTodosLosCampos()
        Dim fila = JsonConvert.DeserializeObject(Of EnvioHistoriaModel)(JSON_FILA)

        Assert.AreEqual(7, fila.Numero)
        Assert.AreEqual(248142, fila.NumeroEnvio)
        Assert.AreEqual("Reembolso", fila.Campo)
        Assert.AreEqual("142,05", fila.ValorAnterior)
        Assert.AreEqual("Lo cambia el cliente", fila.Observaciones)
        Assert.AreEqual("NUEVAVISION\Alfredo", fila.Usuario)
        Assert.AreEqual(New Date(2026, 8, 31, 12, 4, 0), fila.FechaModificacion)
    End Sub

    ''' <summary>
    ''' Un envio que nadie ha tocado no tiene historial, y es lo normal. El endpoint devuelve
    ''' lista vacia (no 404), asi que aqui tiene que salir una coleccion vacia y no un Nothing
    ''' que reviente al enlazar la rejilla.
    ''' </summary>
    <TestMethod()>
    Public Sub EnvioHistoriaModel_ListaVacia_SeDeserializaSinReventar()
        Dim filas = JsonConvert.DeserializeObject(Of List(Of EnvioHistoriaModel))("[]")

        Assert.IsNotNull(filas)
        Assert.AreEqual(0, filas.Count)
    End Sub

End Class
