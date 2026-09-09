Imports System.Collections.Generic
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Services
Imports Nesto.Models.Nesto.Models
Imports Nesto.ViewModels
Imports Newtonsoft.Json

''' <summary>
''' Nesto#340 (Agencias): las empresas ya no se leen de Entity Framework, sino de GET Empresas.
''' Lo que hay que vigilar es lo mismo que en las agencias (A1): la COMPARACIÓN. Empresa es char(3)
''' y el servidor devuelve la entidad con el relleno ("1  "), mientras que quien pregunta puede
''' traer "1" o "1  ". Y como CargarEmpresa alimenta el remitente de las etiquetas de ASM y
''' Correos Express, si dejara de encontrar la empresa no saldría ninguna etiqueta.
''' </summary>
<TestClass()>
Public Class AgenciaServiceEmpresasTests

    Private Shared Function Empresa(numero As String, nombre As String) As Empresas
        Return New Empresas With {.Número = numero, .Nombre = nombre}
    End Function

    Private Shared Function CrearServicio(ParamArray empresas As Empresas()) As AgenciaService
        Dim servicio As New AgenciaService(A.Fake(Of IServicioAgenciasMantenimiento)())
        servicio.LectorEmpresas = Function() New List(Of Empresas)(empresas)
        Return servicio
    End Function

    <TestMethod()>
    Public Sub CargarEmpresa_ElServidorDevuelveElNumeroConRelleno_LaEncuentraPreguntandoRecortado()
        Dim servicio = CrearServicio(Empresa("1  ", "Nueva Visión"), Empresa("3  ", "Espejo"))

        Assert.AreEqual("Espejo", servicio.CargarEmpresa("3").Nombre)
    End Sub

    <TestMethod()>
    Public Sub CargarEmpresa_ElEnvioTraeLaEmpresaConRelleno_LaEncuentraIgualmente()
        ' envio.Empresa es char(3): los llamantes traen "1  ".
        Dim servicio = CrearServicio(Empresa("1", "Nueva Visión"))

        Assert.AreEqual("Nueva Visión", servicio.CargarEmpresa("1  ").Nombre)
    End Sub

    <TestMethod()>
    Public Sub CargarEmpresa_SiNoExiste_LanzaConElNumeroEnElMensaje()
        Dim servicio = CrearServicio(Empresa("1  ", "Nueva Visión"))

        Dim ex = Assert.ThrowsException(Of Exception)(Sub() servicio.CargarEmpresa("9"))

        StringAssert.Contains(ex.Message, "'9'")
    End Sub

    <TestMethod()>
    Public Sub CargarListaEmpresas_MientrasDuraLaCache_SoloPreguntaUnaVezALaApi()
        ' CargarEmpresa se llama por cada etiqueta impresa: sin caché sería una llamada HTTP por etiqueta.
        Dim llamadas As Integer = 0
        Dim servicio As New AgenciaService(A.Fake(Of IServicioAgenciasMantenimiento)())
        servicio.LectorEmpresas = Function()
                                      llamadas += 1
                                      Return New List(Of Empresas) From {Empresa("1  ", "Nueva Visión")}
                                  End Function

        Dim unused1 = servicio.CargarEmpresa("1")
        Dim unused2 = servicio.CargarEmpresa("1")
        Dim unused3 = servicio.CargarListaEmpresas()

        Assert.AreEqual(1, llamadas)
    End Sub

    <TestMethod()>
    Public Sub Contrato_ElJsonDeGetEmpresas_RellenaLosCamposQueUsanLasEtiquetas()
        ' Nombres de propiedad tal y como los serializa NestoAPI (entidad Empresa, sin recortar).
        ' Si alguno dejara de casar, Newtonsoft lo dejaría en Nothing sin avisar y la etiqueta
        ' saldría con el remitente vacío. Hay un test gemelo en NestoAPI.Tests (EmpresaJsonContratoTests).
        Dim json As String = "[{""Número"":""1  "",""Nombre"":""NUEVA VISIÓN"",""NIF"":""B12345678"",""Dirección"":""C/ Prueba 1"",""CodPostal"":""28100"",""Población"":""ALGETE"",""Provincia"":""MADRID"",""Teléfono"":""916000000"",""Email"":""info@nuevavision.es"",""CabFacturaVta"":[]}]"

        Dim empresas = JsonConvert.DeserializeObject(Of List(Of Empresas))(json)

        Dim e = empresas(0)
        Assert.AreEqual("1  ", e.Número)
        Assert.AreEqual("NUEVA VISIÓN", e.Nombre)
        Assert.AreEqual("B12345678", e.NIF)
        Assert.AreEqual("C/ Prueba 1", e.Dirección)
        Assert.AreEqual("28100", e.CodPostal)
        Assert.AreEqual("ALGETE", e.Población)
        Assert.AreEqual("MADRID", e.Provincia)
        Assert.AreEqual("916000000", e.Teléfono)
        Assert.AreEqual("info@nuevavision.es", e.Email)
    End Sub

End Class
