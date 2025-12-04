Imports Nesto.Models

''' <summary>
''' Tests para PedidoVentaDTO.Equals y CrearSnapshot (Issue #254)
''' Verifican la detección de cambios sin guardar en pedidos.
''' </summary>
<TestClass()>
Public Class PedidoVentaDTOTests

#Region "Equals - Casos básicos"

    <TestMethod()>
    Public Sub Equals_MismoPedido_DevuelveTrue()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .empresa = "1",
            .numero = 12345,
            .cliente = "00001",
            .formaPago = "TRN",
            .plazosPago = "CONTADO",
            .ccc = "ES1234567890"
        }

        ' Act & Assert
        Assert.IsTrue(pedido.Equals(pedido))
    End Sub

    <TestMethod()>
    Public Sub Equals_PedidosIguales_DevuelveTrue()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {
            .empresa = "1",
            .numero = 12345,
            .cliente = "00001",
            .formaPago = "TRN",
            .plazosPago = "CONTADO",
            .ccc = "ES1234567890",
            .iva = "G21",
            .vendedor = "NV",
            .periodoFacturacion = "NRM",
            .ruta = "FW",
            .serie = "NV",
            .contacto = "0",
            .contactoCobro = "0",
            .comentarios = "Test",
            .comentarioPicking = "Picking test",
            .noComisiona = 0,
            .mantenerJunto = False,
            .servirJunto = True,
            .notaEntrega = False
        }

        Dim pedido2 As New PedidoVentaDTO() With {
            .empresa = "1",
            .numero = 12345,
            .cliente = "00001",
            .formaPago = "TRN",
            .plazosPago = "CONTADO",
            .ccc = "ES1234567890",
            .iva = "G21",
            .vendedor = "NV",
            .periodoFacturacion = "NRM",
            .ruta = "FW",
            .serie = "NV",
            .contacto = "0",
            .contactoCobro = "0",
            .comentarios = "Test",
            .comentarioPicking = "Picking test",
            .noComisiona = 0,
            .mantenerJunto = False,
            .servirJunto = True,
            .notaEntrega = False
        }

        ' Act & Assert
        Assert.IsTrue(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_PedidoNulo_DevuelveFalse()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .formaPago = "TRN"
        }

        ' Act & Assert
        Assert.IsFalse(pedido.Equals(Nothing))
    End Sub

#End Region

#Region "Equals - Detección de cambios en campos críticos"

    <TestMethod()>
    Public Sub Equals_FormaPagoDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.formaPago = "TRN"}
        Dim pedido2 As New PedidoVentaDTO() With {.formaPago = "EFC"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_PlazosPagoDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.plazosPago = "CONTADO"}
        Dim pedido2 As New PedidoVentaDTO() With {.plazosPago = "30D"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_CccDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.ccc = "ES1234567890"}
        Dim pedido2 As New PedidoVentaDTO() With {.ccc = "ES0987654321"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_IvaDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.iva = "G21"}
        Dim pedido2 As New PedidoVentaDTO() With {.iva = "G10"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_VendedorDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.vendedor = "NV"}
        Dim pedido2 As New PedidoVentaDTO() With {.vendedor = "CV"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_PeriodoFacturacionDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.periodoFacturacion = "NRM"}
        Dim pedido2 As New PedidoVentaDTO() With {.periodoFacturacion = "FDM"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_RutaDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.ruta = "FW"}
        Dim pedido2 As New PedidoVentaDTO() With {.ruta = "AT"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_SerieDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.serie = "NV"}
        Dim pedido2 As New PedidoVentaDTO() With {.serie = "CV"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_ContactoDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.contacto = "0"}
        Dim pedido2 As New PedidoVentaDTO() With {.contacto = "1"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_ContactoCobroDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.contactoCobro = "0"}
        Dim pedido2 As New PedidoVentaDTO() With {.contactoCobro = "1"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_ComentariosDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.comentarios = "Comentario original"}
        Dim pedido2 As New PedidoVentaDTO() With {.comentarios = "Comentario modificado"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_ComentarioPickingDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.comentarioPicking = "Picking original"}
        Dim pedido2 As New PedidoVentaDTO() With {.comentarioPicking = "Picking modificado"}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_NoComisionaDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.noComisiona = 0}
        Dim pedido2 As New PedidoVentaDTO() With {.noComisiona = 100}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_MantenerJuntoDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.mantenerJunto = False}
        Dim pedido2 As New PedidoVentaDTO() With {.mantenerJunto = True}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_ServirJuntoDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.servirJunto = False}
        Dim pedido2 As New PedidoVentaDTO() With {.servirJunto = True}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_NotaEntregaDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.notaEntrega = False}
        Dim pedido2 As New PedidoVentaDTO() With {.notaEntrega = True}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_PrimerVencimientoDiferente_DevuelveFalse()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.primerVencimiento = New Date(2025, 1, 15)}
        Dim pedido2 As New PedidoVentaDTO() With {.primerVencimiento = New Date(2025, 1, 30)}

        ' Act & Assert
        Assert.IsFalse(pedido1.Equals(pedido2))
    End Sub

#End Region

#Region "Equals - Campos que NO afectan a la comparación"

    <TestMethod()>
    Public Sub Equals_NumerosDiferentes_DevuelveTrue()
        ' El número del pedido no se compara porque no es editable por el usuario
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {
            .numero = 12345,
            .formaPago = "TRN"
        }
        Dim pedido2 As New PedidoVentaDTO() With {
            .numero = 99999,
            .formaPago = "TRN"
        }

        ' Act & Assert
        Assert.IsTrue(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_EmpresasDiferentes_DevuelveTrue()
        ' La empresa no se compara porque no es editable
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {
            .empresa = "1",
            .formaPago = "TRN"
        }
        Dim pedido2 As New PedidoVentaDTO() With {
            .empresa = "2",
            .formaPago = "TRN"
        }

        ' Act & Assert
        Assert.IsTrue(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_ClientesDiferentes_DevuelveTrue()
        ' El cliente no se compara porque no es editable una vez creado el pedido
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {
            .cliente = "00001",
            .formaPago = "TRN"
        }
        Dim pedido2 As New PedidoVentaDTO() With {
            .cliente = "00002",
            .formaPago = "TRN"
        }

        ' Act & Assert
        Assert.IsTrue(pedido1.Equals(pedido2))
    End Sub

#End Region

#Region "CrearSnapshot"

    <TestMethod()>
    Public Sub CrearSnapshot_CopiaLosCamposCorrectamente()
        ' Arrange
        Dim pedidoOriginal As New PedidoVentaDTO() With {
            .empresa = "1",
            .numero = 12345,
            .cliente = "00001",
            .contacto = "0",
            .formaPago = "TRN",
            .plazosPago = "CONTADO",
            .primerVencimiento = New Date(2025, 1, 15),
            .ccc = "ES1234567890",
            .iva = "G21",
            .vendedor = "NV",
            .periodoFacturacion = "NRM",
            .ruta = "FW",
            .serie = "NV",
            .contactoCobro = "0",
            .comentarios = "Comentario test",
            .comentarioPicking = "Picking test",
            .noComisiona = 50,
            .mantenerJunto = True,
            .servirJunto = False,
            .notaEntrega = True
        }

        ' Act
        Dim snapshot = pedidoOriginal.CrearSnapshot()

        ' Assert
        Assert.IsTrue(pedidoOriginal.Equals(snapshot))
        Assert.AreNotSame(pedidoOriginal, snapshot) ' Deben ser instancias diferentes
    End Sub

    <TestMethod()>
    Public Sub CrearSnapshot_EsCopiaProfunda_ModificarOriginalNoAfectaSnapshot()
        ' Arrange
        Dim pedidoOriginal As New PedidoVentaDTO() With {
            .formaPago = "TRN",
            .ccc = "ES1234567890"
        }
        Dim snapshot = pedidoOriginal.CrearSnapshot()

        ' Act - Modificar el original
        pedidoOriginal.formaPago = "EFC"
        pedidoOriginal.ccc = "ES0987654321"

        ' Assert - El snapshot no debe haber cambiado
        Assert.AreEqual("TRN", snapshot.formaPago)
        Assert.AreEqual("ES1234567890", snapshot.ccc)
        Assert.IsFalse(pedidoOriginal.Equals(snapshot))
    End Sub

#End Region

#Region "GetHashCode"

    <TestMethod()>
    Public Sub GetHashCode_PedidosIguales_MismoHash()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {
            .formaPago = "TRN",
            .ccc = "ES1234567890",
            .plazosPago = "CONTADO",
            .iva = "G21"
        }
        Dim pedido2 As New PedidoVentaDTO() With {
            .formaPago = "TRN",
            .ccc = "ES1234567890",
            .plazosPago = "CONTADO",
            .iva = "G21"
        }

        ' Act & Assert
        Assert.AreEqual(pedido1.GetHashCode(), pedido2.GetHashCode())
    End Sub

    <TestMethod()>
    Public Sub GetHashCode_PedidosDiferentes_DiferenteHash()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {.formaPago = "TRN"}
        Dim pedido2 As New PedidoVentaDTO() With {.formaPago = "EFC"}

        ' Act & Assert
        ' Nota: No es estrictamente necesario que sean diferentes, pero es deseable
        Assert.AreNotEqual(pedido1.GetHashCode(), pedido2.GetHashCode())
    End Sub

#End Region

#Region "Escenarios de uso real - Detección de cambios sin guardar"

    <TestMethod()>
    Public Sub EscenarioReal_CambiarFormaPago_DetectaCambio()
        ' Simula el escenario real: usuario carga pedido, cambia forma de pago
        ' Arrange
        Dim pedidoCargado As New PedidoVentaDTO() With {
            .empresa = "1",
            .numero = 904927,
            .cliente = "36766",
            .formaPago = "TRN",
            .plazosPago = "CONTADO",
            .ccc = "ES1234567890"
        }
        Dim snapshot = pedidoCargado.CrearSnapshot()

        ' Act - Usuario cambia la forma de pago
        pedidoCargado.formaPago = "EFC"

        ' Assert - Debe detectar el cambio
        Assert.IsFalse(pedidoCargado.Equals(snapshot), "Debe detectar que la forma de pago cambió")
    End Sub

    <TestMethod()>
    Public Sub EscenarioReal_CambiarCCC_DetectaCambio()
        ' Simula el escenario: usuario carga pedido, cambia CCC
        ' Arrange
        Dim pedidoCargado As New PedidoVentaDTO() With {
            .empresa = "1",
            .numero = 904927,
            .formaPago = "RCB",
            .ccc = "ES1234567890123456789012"
        }
        Dim snapshot = pedidoCargado.CrearSnapshot()

        ' Act - Usuario cambia el CCC
        pedidoCargado.ccc = "ES9876543210987654321098"

        ' Assert
        Assert.IsFalse(pedidoCargado.Equals(snapshot), "Debe detectar que el CCC cambió")
    End Sub

    <TestMethod()>
    Public Sub EscenarioReal_SinCambios_NoDetectaCambio()
        ' Simula: usuario carga pedido, no cambia nada
        ' Arrange
        Dim pedidoCargado As New PedidoVentaDTO() With {
            .empresa = "1",
            .numero = 904927,
            .formaPago = "TRN",
            .plazosPago = "CONTADO",
            .ccc = Nothing
        }
        Dim snapshot = pedidoCargado.CrearSnapshot()

        ' Act - Usuario no hace cambios

        ' Assert
        Assert.IsTrue(pedidoCargado.Equals(snapshot), "No debe detectar cambios si no se modificó nada")
    End Sub

    <TestMethod()>
    Public Sub EscenarioReal_GuardarYRecargar_NoDetectaCambio()
        ' Simula: usuario cambia algo, guarda, se actualiza el snapshot
        ' Arrange
        Dim pedidoCargado As New PedidoVentaDTO() With {
            .formaPago = "TRN"
        }
        Dim snapshot = pedidoCargado.CrearSnapshot()

        ' Act - Usuario cambia y "guarda"
        pedidoCargado.formaPago = "EFC"
        ' Simular actualización del snapshot después de guardar
        snapshot = pedidoCargado.CrearSnapshot()

        ' Assert - Después de guardar, no debe haber cambios pendientes
        Assert.IsTrue(pedidoCargado.Equals(snapshot), "Después de guardar, no debe haber cambios pendientes")
    End Sub

#End Region

End Class
