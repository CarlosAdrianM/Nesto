Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels
Imports Newtonsoft.Json

''' <summary>
''' Nesto#340 (slice A3): el contrato con GET api/PedidosVenta/ParaAgencia/SituacionLineas.
'''
''' Importa mas de lo que parece, porque este es un fallo que NO avisa: si alguien renombra una
''' propiedad del DTO del servidor, Newtonsoft no encuentra el campo y deja el Boolean en False,
''' que es un valor perfectamente valido. Y los dos False son justo los que hacen dano:
'''
'''   TieneAlgunaLineaConPicking = False  ->  Agencias pregunta ""este pedido no tiene picking"" en
'''                                          TODOS los pedidos, incluidos los preparados.
'''   EsTodoOnline = False                ->  se manda el correo de aviso de entrega tambien en
'''                                          los pedidos de tienda online, que no deben recibirlo.
'''
''' Ninguno de los dos da error ni deja rastro en ELMAH. Por eso hay test.
''' </summary>
<TestClass()>
Public Class SituacionLineasPedidoModelTests

    ' JSON tal y como lo serializa el endpoint (camelCase).
    Private Const JSON_CON_PICKING As String = "{""tieneAlgunaLineaConPicking"":true,""esTodoOnline"":false}"
    Private Const JSON_TODO_ONLINE As String = "{""tieneAlgunaLineaConPicking"":false,""esTodoOnline"":true}"

    <TestMethod()>
    Public Sub SituacionLineasPedidoModel_DelJsonDelEndpoint_LeeLosDosCampos()
        Dim situacion = JsonConvert.DeserializeObject(Of SituacionLineasPedidoModel)(JSON_CON_PICKING)

        Assert.IsTrue(situacion.TieneAlgunaLineaConPicking)
        Assert.IsFalse(situacion.EsTodoOnline)
    End Sub

    <TestMethod()>
    Public Sub SituacionLineasPedidoModel_PedidoDeTiendaOnline_LoDistingue()
        Dim situacion = JsonConvert.DeserializeObject(Of SituacionLineasPedidoModel)(JSON_TODO_ONLINE)

        Assert.IsFalse(situacion.TieneAlgunaLineaConPicking)
        Assert.IsTrue(situacion.EsTodoOnline)
    End Sub

    ''' <summary>
    ''' Si el servidor dejara de mandar un campo, el modelo se queda en False. No es un fallo del
    ''' cliente —no puede hacer otra cosa—, pero queda escrito aqui para que el dia que alguien
    ''' investigue ""por que pregunta siempre lo del picking"" encuentre la pista a la primera.
    ''' </summary>
    <TestMethod()>
    Public Sub SituacionLineasPedidoModel_SiElServidorNoMandaLosCampos_TodoQuedaEnFalse()
        Dim situacion = JsonConvert.DeserializeObject(Of SituacionLineasPedidoModel)("{}")

        Assert.IsFalse(situacion.TieneAlgunaLineaConPicking)
        Assert.IsFalse(situacion.EsTodoOnline)
    End Sub

End Class
