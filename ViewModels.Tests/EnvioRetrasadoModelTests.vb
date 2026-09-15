Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels
Imports Newtonsoft.Json

''' <summary>
''' Nesto#468: el contrato con GET api/EnviosAgencias/Retrasados (NestoAPI#173). Si un nombre del
''' JSON deja de casar, Newtonsoft deja la propiedad en blanco y la columna sale vacía sin que
''' salte nada: por eso se fija aquí con el JSON tal cual lo serializa el endpoint (camelCase).
''' </summary>
<TestClass()>
Public Class EnvioRetrasadoModelTests

    Private Const JSON_FILA As String = "{""numero"":247926,""empresa"":""1  "",""pedido"":925001,""cliente"":""15191"",""contacto"":""0"",""nombre"":""CLIENTE PRUEBA"",""agencia"":11,""nombreAgencia"":""Innovatrans"",""codigoBarras"":""0123456789"",""fecha"":""2026-08-19T00:00:00"",""diasTranscurridos"":19,""detalleEstado"":""REPARTO"",""poblacion"":""ALGETE"",""codPostal"":""28110"",""telefono"":""916281914"",""movil"":""600000000"",""email"":""cliente@correo.es"",""observaciones"":""Llamar antes"",""vendedor"":""NV""}"

    <TestMethod()>
    Public Sub EnvioRetrasadoModel_DelJsonDelEndpoint_MapeaTodosLosCampos()
        Dim fila = JsonConvert.DeserializeObject(Of EnvioRetrasadoModel)(JSON_FILA)

        Assert.AreEqual(247926, fila.Numero)
        Assert.AreEqual("1  ", fila.Empresa)
        Assert.AreEqual(925001, fila.Pedido)
        Assert.AreEqual("15191", fila.Cliente)
        Assert.AreEqual("0", fila.Contacto)
        Assert.AreEqual("CLIENTE PRUEBA", fila.Nombre)
        Assert.AreEqual(11, fila.Agencia)
        Assert.AreEqual("Innovatrans", fila.NombreAgencia)
        Assert.AreEqual("0123456789", fila.CodigoBarras)
        Assert.AreEqual(New Date(2026, 8, 19), fila.Fecha)
        Assert.AreEqual(19, fila.DiasTranscurridos)
        Assert.AreEqual("REPARTO", fila.DetalleEstado)
        Assert.AreEqual("ALGETE", fila.Poblacion)
        Assert.AreEqual("28110", fila.CodPostal)
        Assert.AreEqual("916281914", fila.Telefono)
        Assert.AreEqual("600000000", fila.Movil)
        Assert.AreEqual("cliente@correo.es", fila.Email)
        Assert.AreEqual("Llamar antes", fila.Observaciones)
        Assert.AreEqual("NV", fila.Vendedor)
    End Sub

    <TestMethod()>
    Public Sub EnvioRetrasadoModel_Tramo_PorDias()
        Assert.AreEqual(0, New EnvioRetrasadoModel With {.DiasTranscurridos = 5}.Tramo)
        Assert.AreEqual(1, New EnvioRetrasadoModel With {.DiasTranscurridos = 6}.Tramo)
        Assert.AreEqual(1, New EnvioRetrasadoModel With {.DiasTranscurridos = 10}.Tramo)
        Assert.AreEqual(2, New EnvioRetrasadoModel With {.DiasTranscurridos = 19}.Tramo)
    End Sub

    <TestMethod()>
    Public Sub EnvioRetrasadoModel_ListaVacia_SeDeserializaSinReventar()
        Dim filas = JsonConvert.DeserializeObject(Of List(Of EnvioRetrasadoModel))("[]")

        Assert.IsNotNull(filas)
        Assert.AreEqual(0, filas.Count)
    End Sub

End Class
