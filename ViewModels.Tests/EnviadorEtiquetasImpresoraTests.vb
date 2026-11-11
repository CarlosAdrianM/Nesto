Imports Nesto.ViewModels

' NestoAPI#847: la primera etiqueta del día de la Zebra «no salía» y nadie se enteraba (se ignoraba el resultado).
<TestClass()>
Public Class EnviadorEtiquetasImpresoraTests

    Private Class ImpresoraFalsa
        Implements IImpresoraCruda

        Public ReadOnly Resultados As New Queue(Of Func(Of ResultadoEnvioImpresora))
        Public Llamadas As Integer

        Public Function Enviar(impresora As String, texto As String) As ResultadoEnvioImpresora Implements IImpresoraCruda.Enviar
            Llamadas += 1
            Return Resultados.Dequeue()()
        End Function
    End Class

    Private _impresora As ImpresoraFalsa
    Private _esperas As List(Of TimeSpan)
    Private _avisos As List(Of String)
    Private _registros As List(Of String)
    Private _enviador As EnviadorEtiquetasImpresora

    <TestInitialize()>
    Public Sub Initialize()
        _impresora = New ImpresoraFalsa()
        _esperas = New List(Of TimeSpan)
        _avisos = New List(Of String)
        _registros = New List(Of String)
        _enviador = New EnviadorEtiquetasImpresora(_impresora,
            Function(t)
                _esperas.Add(t)
                Return Task.CompletedTask
            End Function,
            Sub(m) _avisos.Add(m),
            Sub(m, c) _registros.Add(c & "|" & m))
    End Sub

    Private Shared Function Falla(codigo As Integer) As ResultadoEnvioImpresora
        Return New ResultadoEnvioImpresora With {.Exito = False, .CodigoError = codigo, .Detalle = "no se ha podido abrir la impresora"}
    End Function

    <TestMethod()>
    Public Async Function EnviarAsync_SaleALaPrimera_NiEsperaNiReintentaNiAvisa() As Task
        _impresora.Resultados.Enqueue(Function() ResultadoEnvioImpresora.Correcto())

        Dim ok = Await _enviador.EnviarAsync("\\srv\Zebra", "^XA^XZ")

        Assert.IsTrue(ok)
        Assert.AreEqual(1, _impresora.Llamadas)
        Assert.AreEqual(0, _esperas.Count)
        Assert.AreEqual(0, _avisos.Count)
        Assert.AreEqual(0, _registros.Count)
    End Function

    <TestMethod()>
    Public Async Function EnviarAsync_FallaYAlReintentoSale_EsperaUnaVezYNoAvisa() As Task
        _impresora.Resultados.Enqueue(Function() Falla(1722))
        _impresora.Resultados.Enqueue(Function() ResultadoEnvioImpresora.Correcto())

        Dim ok = Await _enviador.EnviarAsync("\\srv\Zebra", "^XA^XZ")

        Assert.IsTrue(ok)
        Assert.AreEqual(2, _impresora.Llamadas)
        CollectionAssert.AreEqual({EnviadorEtiquetasImpresora.EsperaAntesDeReintentar}, _esperas)
        Assert.IsTrue(EnviadorEtiquetasImpresora.EsperaAntesDeReintentar.TotalSeconds >= 1 AndAlso EnviadorEtiquetasImpresora.EsperaAntesDeReintentar.TotalSeconds <= 2)
        Assert.AreEqual(0, _avisos.Count)
        Assert.AreEqual(0, _registros.Count)
    End Function

    <TestMethod()>
    Public Async Function EnviarAsync_FallaDosVeces_AvisaDeUstedConImpresoraYCodigoYRegistraUnaVez() As Task
        _impresora.Resultados.Enqueue(Function() Falla(1722))
        _impresora.Resultados.Enqueue(Function() Falla(1722))

        Dim ok = Await _enviador.EnviarAsync("\\srv\Zebra", "^XA^XZ", "Prueba.Contexto")

        Assert.IsFalse(ok)
        Assert.AreEqual(2, _impresora.Llamadas, "un único reintento")
        Assert.AreEqual(1, _avisos.Count)
        StringAssert.StartsWith(_avisos(0), "No se ha podido enviar la etiqueta a la impresora «\\srv\Zebra». Compruebe que está encendida y vuelva a imprimirla.")
        StringAssert.Contains(_avisos(0), "1722")
        Assert.AreEqual(1, _registros.Count)
        StringAssert.StartsWith(_registros(0), "Prueba.Contexto|")
        StringAssert.Contains(_registros(0), "\\srv\Zebra")
        StringAssert.Contains(_registros(0), "1722")
        StringAssert.Contains(_registros(0), "2 intento(s)")
    End Function

    <TestMethod()>
    Public Async Function EnviarAsync_LaImpresoraLanza_NoLanzaYAvisa() As Task
        Dim lanza As Func(Of ResultadoEnvioImpresora) = Function()
                                                             Throw New InvalidOperationException("se cayó")
                                                         End Function
        _impresora.Resultados.Enqueue(lanza)
        _impresora.Resultados.Enqueue(lanza)

        Dim ok = Await _enviador.EnviarAsync("Zebra", "^XA^XZ")

        Assert.IsFalse(ok)
        Assert.AreEqual(1, _avisos.Count)
        StringAssert.Contains(_avisos(0), "se cayó")
    End Function

    <TestMethod()>
    Public Async Function EnviarAsync_SinImpresoraConfigurada_AvisaSinLlamarALaImpresora() As Task
        Dim ok = Await _enviador.EnviarAsync("  ", "^XA^XZ")

        Assert.IsFalse(ok)
        Assert.AreEqual(0, _impresora.Llamadas)
        StringAssert.Contains(_avisos(0), "«(sin configurar)»")
    End Function

    <TestMethod()>
    Public Async Function EnviarAsync_ElAvisoYElRegistroLanzan_NoLanza() As Task
        _impresora.Resultados.Enqueue(Function() Falla(5))
        _impresora.Resultados.Enqueue(Function() Falla(5))
        Dim enviador = New EnviadorEtiquetasImpresora(_impresora, Function(t) Task.CompletedTask,
            Sub(m) Throw New Exception("diálogo"), Sub(m, c) Throw New Exception("registro"))

        Dim ok = Await enviador.EnviarAsync("Zebra", "^XA^XZ")

        Assert.IsFalse(ok)
    End Function
End Class
