Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

''' <summary>
''' Tests para la funcionalidad de Recoger Producto (Issue #135).
''' Verifican la detección de etiquetas de recogida a partir de envíos.
''' </summary>
<TestClass()>
Public Class RecogerProductoTests

    <TestMethod()>
    Public Sub TieneEtiquetaRecogida_ConRetornoPositivo_DevuelveTrue()
        ' Arrange
        Dim envios As New List(Of EnvioAgenciaDTO) From {
            New EnvioAgenciaDTO With {.Numero = 1, .Estado = -1, .Retorno = 1, .AgenciaId = 1}
        }

        ' Act
        Dim resultado = DetallePedidoViewModel.TieneEtiquetaRecogida(envios)

        ' Assert
        Assert.IsTrue(resultado)
    End Sub

    <TestMethod()>
    Public Sub TieneEtiquetaRecogida_SinRetorno_DevuelveFalse()
        ' Arrange
        Dim envios As New List(Of EnvioAgenciaDTO) From {
            New EnvioAgenciaDTO With {.Numero = 1, .Estado = 0, .Retorno = 0, .AgenciaId = 1}
        }

        ' Act
        Dim resultado = DetallePedidoViewModel.TieneEtiquetaRecogida(envios)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub TieneEtiquetaRecogida_ListaVacia_DevuelveFalse()
        ' Arrange
        Dim envios As New List(Of EnvioAgenciaDTO)

        ' Act
        Dim resultado = DetallePedidoViewModel.TieneEtiquetaRecogida(envios)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub TieneEtiquetaRecogida_ListaNothing_DevuelveFalse()
        ' Act
        Dim resultado = DetallePedidoViewModel.TieneEtiquetaRecogida(Nothing)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub TieneEtiquetaRecogida_VariosEnviosUnoConRetorno_DevuelveTrue()
        ' Arrange
        Dim envios As New List(Of EnvioAgenciaDTO) From {
            New EnvioAgenciaDTO With {.Numero = 1, .Estado = 0, .Retorno = 0, .AgenciaId = 1},
            New EnvioAgenciaDTO With {.Numero = 2, .Estado = -1, .Retorno = 1, .AgenciaId = 1}
        }

        ' Act
        Dim resultado = DetallePedidoViewModel.TieneEtiquetaRecogida(envios)

        ' Assert
        Assert.IsTrue(resultado)
    End Sub

    <TestMethod()>
    Public Sub TieneEtiquetaRecogida_EnvioEnCursoConRetorno_DevuelveTrue()
        ' Arrange - Etiqueta ya impresa (Estado >= 0) pero con retorno
        Dim envios As New List(Of EnvioAgenciaDTO) From {
            New EnvioAgenciaDTO With {.Numero = 1, .Estado = 0, .Retorno = 1, .AgenciaId = 1}
        }

        ' Act
        Dim resultado = DetallePedidoViewModel.TieneEtiquetaRecogida(envios)

        ' Assert
        Assert.IsTrue(resultado, "Una etiqueta ya impresa con retorno también indica recogida")
    End Sub

End Class
