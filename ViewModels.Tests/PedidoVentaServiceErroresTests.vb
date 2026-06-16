Imports System.ComponentModel.DataAnnotations
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Modulos.PedidoVenta

''' <summary>
''' Tests de PedidoVentaService.InterpretarRespuestaError.
'''
''' Regresión pedido 918386: al modificar un pedido ya en albarán, NestoAPI responde
''' con errorPersonalizado, que manda TEXTO PLANO (StringContent), p.ej. "No se puede
''' modificar un pedido ya facturado". El cliente deserializaba ciegamente como JSON y
''' Newtonsoft, al ver la 'N' inicial, lanzaba "Error parsing NaN value. Path '', line 1,
''' position 1.", enmascarando el mensaje real. El usuario veía el críptico NaN en vez
''' del motivo de verdad.
''' </summary>
<TestClass()>
Public Class PedidoVentaServiceErroresTests

    <TestMethod()>
    Public Sub InterpretarRespuestaError_TextoPlanoQueEmpiezaPorN_DevuelveMensajeReal()
        ' Arrange - cuerpo tal cual lo manda errorPersonalizado (texto plano, sin JSON)
        Dim respuestaError = "No se puede modificar un pedido ya facturado"

        ' Act
        Dim ex = PedidoVentaService.InterpretarRespuestaError(respuestaError)

        ' Assert
        Assert.IsNotNull(ex, "Debe devolver una excepción, no lanzarla al parsear")
        Assert.AreEqual(respuestaError, ex.Message,
                        "Debe mostrar el mensaje real del servidor, no 'Error parsing NaN value'")
        Assert.IsFalse(ex.Message.Contains("NaN"),
                       "No debe enmascarar el error con 'NaN'")
    End Sub

    <TestMethod()>
    Public Sub InterpretarRespuestaError_TextoPlanoCualquiera_NoLanzaExcepcionAlParsear()
        ' Arrange - otro texto plano que tampoco es JSON
        Dim respuestaError = "Alguien modificó o eliminó la línea mientras se actualizaba el pedido"

        ' Act
        Dim ex = PedidoVentaService.InterpretarRespuestaError(respuestaError)

        ' Assert
        Assert.AreEqual(respuestaError, ex.Message)
    End Sub

    <TestMethod()>
    Public Sub InterpretarRespuestaError_ErrorValidacionPedido_DevuelveValidationException()
        ' Arrange - formato nuevo de GlobalExceptionFilter con code de validación
        Dim respuestaError = "{""error"":{""code"":""PEDIDO_VALIDACION_FALLO"",""message"":""El pedido no supera la validación""}}"

        ' Act
        Dim ex = PedidoVentaService.InterpretarRespuestaError(respuestaError)

        ' Assert
        Assert.IsInstanceOfType(ex, GetType(ValidationException),
                                "PEDIDO_VALIDACION_FALLO debe seguir lanzando ValidationException")
        Assert.IsTrue(ex.Message.Contains("no supera la validación"),
                      $"Debe conservar el mensaje de validación. Fue: {ex.Message}")
    End Sub

    <TestMethod()>
    Public Sub InterpretarRespuestaError_ErrorJsonGenerico_DevuelveExceptionConMensaje()
        ' Arrange - error JSON sin code de validación
        Dim respuestaError = "{""error"":{""code"":""ALGUN_ERROR"",""message"":""Mensaje de error genérico""}}"

        ' Act
        Dim ex = PedidoVentaService.InterpretarRespuestaError(respuestaError)

        ' Assert
        Assert.IsNotInstanceOfType(ex, GetType(ValidationException))
        Assert.IsTrue(ex.Message.Contains("Mensaje de error genérico"),
                      $"Debe conservar el mensaje del servidor. Fue: {ex.Message}")
    End Sub

End Class
