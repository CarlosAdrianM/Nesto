Imports System.Threading.Tasks
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Models.Nesto.Models
Imports Nesto.ViewModels

' Innovatrans (DataTrans DTX): agencia "registrar al imprimir". El SOAP vive en NestoAPI; aquí solo
' verificamos los flags polimórficos que hacen que el ViewModel la trate distinto (imprimir = registrar
' en la agencia vía InsertarYEtiquetar, en vez del flujo clásico de montar la etiqueta en local).
<TestClass()>
Public Class AgenciaInnovatransTests

    Private Shared Function CrearAgencia() As AgenciaInnovatrans
        ' El constructor guarda el ViewModel pero no lo usa para estas propiedades; Nothing vale.
        Return New AgenciaInnovatrans(Nothing)
    End Function

    <TestMethod()>
    Public Sub Innovatrans_FlujoEsRegistrarAlImprimir()
        Assert.AreEqual(TipoFlujoTramitacion.RegistrarAlImprimir, CrearAgencia().FlujoTramitacion)
    End Sub

    <TestMethod()>
    Public Sub Innovatrans_ImplementaGestionRemota()
        Assert.IsInstanceOfType(CrearAgencia(), GetType(IAgenciaConGestionRemota))
    End Sub

    <TestMethod()>
    Public Sub Innovatrans_NoPermiteEditarCodigoBarras()
        ' El albarán lo asigna la plataforma al tramitar; no se edita a mano.
        Assert.IsFalse(CrearAgencia().PermiteEditarCodigoBarras)
    End Sub

    <TestMethod()>
    Public Sub Innovatrans_NoExigeDimensionesBultos()
        ' El servidor manda una caja estándar por defecto; no se piden medidas.
        Assert.IsFalse(CrearAgencia().DimensionesBultosObligatorias)
    End Sub

    <TestMethod()>
    Public Sub Innovatrans_PaisDefectoEsEspana()
        Assert.AreEqual(34, CrearAgencia().paisDefecto)
    End Sub

    ' Nesto#411 (NestoAPI#316/#317): anular y modificar envíos ya registrados en la agencia.

    <TestMethod()>
    Public Async Function AnularEnAgencia_Exito_DejaElEnvioComoPendienteSinAlbaran() As Task
        Dim servicio = A.Fake(Of IAgenciaService)()
        A.CallTo(Function() servicio.AnularEnvioRemoto(246221)).Returns(Task.CompletedTask)
        Dim envio As New EnviosAgencia With {.Numero = 246221, .CodigoBarras = "6522393001", .Estado = 0}

        Dim respuesta = Await CrearAgencia().AnularEnAgencia(envio, servicio)

        Assert.IsTrue(respuesta.Exito)
        Assert.AreEqual(String.Empty, envio.CodigoBarras, "El albarán anulado no debe quedar en el envío local.")
        Assert.AreEqual(CShort(-1), envio.Estado, "Anulado = vuelve a etiqueta pendiente, como en el servidor.")
        A.CallTo(Function() servicio.AnularEnvioRemoto(246221)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function AnularEnAgencia_LaAgenciaRechaza_NoTocaElEnvioYDevuelveElMotivo() As Task
        ' Caso clave para logística: la ventana de edición del día ya cerró (codError 413 de DTX).
        Dim servicio = A.Fake(Of IAgenciaService)()
        A.CallTo(Function() servicio.AnularEnvioRemoto(A(Of Integer).Ignored)) _
            .Throws(New Exception("NestoAPI rechazó la anulación (502): Excedido el tiempo de borrado"))
        Dim envio As New EnviosAgencia With {.Numero = 246221, .CodigoBarras = "6522393001", .Estado = 0}

        Dim respuesta = Await CrearAgencia().AnularEnAgencia(envio, servicio)

        Assert.IsFalse(respuesta.Exito)
        StringAssert.Contains(respuesta.TextoRespuestaError, "Excedido el tiempo de borrado")
        Assert.AreEqual("6522393001", envio.CodigoBarras, "Si la agencia rechaza, el envío local queda intacto.")
        Assert.AreEqual(CShort(0), envio.Estado)
    End Function

    <TestMethod()>
    Public Async Function ModificarEnAgencia_MandaLosDatosActualesDelEnvio() As Task
        ' El usuario ha corregido el CP en el envío: ModificarEnAgencia manda los valores ACTUALES.
        ' El fake devuelve resultado sin etiqueta -> la respuesta es error de etiqueta, pero lo que
        ' verifica este test es el DTO que viaja al servidor.
        Dim servicio = A.Fake(Of IAgenciaService)()
        Dim datosEnviados As ModificarEnvioAgenciaDto = Nothing
        A.CallTo(Function() servicio.ModificarEnvioRemoto(A(Of Integer).Ignored, A(Of ModificarEnvioAgenciaDto).Ignored)) _
            .Invokes(Sub(n As Integer, d As ModificarEnvioAgenciaDto) datosEnviados = d) _
            .Returns(Task.FromResult(New TramitarEnvioResultadoDto With {.Albaran = "6522393001"}))
        Dim envio As New EnviosAgencia With {
            .Numero = 246221, .CodigoBarras = "6522393001", .Estado = 0,
            .Nombre = "CLIENTE", .Direccion = "Calle Mayor 1", .CodPostal = "28001",
            .Poblacion = "MADRID", .Telefono = "600000000"
        }

        Dim respuesta = Await CrearAgencia().ModificarEnAgencia(envio, servicio)

        Assert.IsNotNull(datosEnviados)
        Assert.AreEqual("28001", datosEnviados.CodigoPostal)
        Assert.AreEqual("MADRID", datosEnviados.Poblacion)
        Assert.AreEqual("Calle Mayor 1", datosEnviados.Direccion)
        ' Sin EtiquetaContenido en el resultado, la respuesta avisa de la etiqueta (la modificación
        ' en sí ya quedó aplicada y persistida por el servidor).
        Assert.IsFalse(respuesta.Exito)
        StringAssert.Contains(respuesta.TextoRespuestaError, "etiqueta")
    End Function

    <TestMethod()>
    Public Async Function ModificarEnAgencia_LaAgenciaRechaza_DevuelveElMotivo() As Task
        Dim servicio = A.Fake(Of IAgenciaService)()
        A.CallTo(Function() servicio.ModificarEnvioRemoto(A(Of Integer).Ignored, A(Of ModificarEnvioAgenciaDto).Ignored)) _
            .Throws(New Exception("NestoAPI rechazó la modificación (502): Excedido el tiempo"))
        Dim envio As New EnviosAgencia With {.Numero = 246221, .CodigoBarras = "6522393001", .Estado = 0}

        Dim respuesta = Await CrearAgencia().ModificarEnAgencia(envio, servicio)

        Assert.IsFalse(respuesta.Exito)
        StringAssert.Contains(respuesta.TextoRespuestaError, "Excedido el tiempo")
    End Function

End Class
