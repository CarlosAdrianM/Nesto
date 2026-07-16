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

    <TestMethod()>
    Public Sub Equals_NothingYStringVacio_DevuelveTrue()
        ' Arrange - Simula el caso donde API devuelve Nothing y el DTO tiene ""
        Dim pedido1 As New PedidoVentaDTO() With {
            .formaPago = "TRN",
            .comentarios = Nothing,
            .comentarioPicking = Nothing,
            .contactoCobro = Nothing
        }
        Dim pedido2 As New PedidoVentaDTO() With {
            .formaPago = "TRN",
            .comentarios = "",
            .comentarioPicking = "",
            .contactoCobro = ""
        }

        ' Act & Assert - Nothing y "" deben considerarse iguales
        Assert.IsTrue(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_AmbosNothing_DevuelveTrue()
        ' Arrange
        Dim pedido1 As New PedidoVentaDTO() With {
            .formaPago = "TRN",
            .comentarios = Nothing
        }
        Dim pedido2 As New PedidoVentaDTO() With {
            .formaPago = "TRN",
            .comentarios = Nothing
        }

        ' Act & Assert
        Assert.IsTrue(pedido1.Equals(pedido2))
    End Sub

    <TestMethod()>
    Public Sub Equals_StringsConEspaciosAlFinal_DevuelveTrue()
        ' Arrange - Simula el caso de campos de BD con longitud fija (espacios al final)
        Dim pedido1 As New PedidoVentaDTO() With {
            .formaPago = "TRN",
            .vendedor = "NV",
            .contactoCobro = "0"
        }
        Dim pedido2 As New PedidoVentaDTO() With {
            .formaPago = "TRN",
            .vendedor = "NV ",
            .contactoCobro = "0  "
        }

        ' Act & Assert - Los espacios al final deben ignorarse
        Assert.IsTrue(pedido1.Equals(pedido2))
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

#Region "Issue #254 - ContactoCobro no debe sobrescribirse al cargar pedido existente"

    ''' <summary>
    ''' BUG REPORTADO: Al abrir pedido 909349 y dar a "Crear Factura" sin modificar nada,
    ''' dice "El pedido tiene cambios sin guardar".
    '''
    ''' CAUSA: El setter de ClienteCompleto en DetallePedidoViewModel sobrescribe contactoCobro
    ''' con el contacto del cliente, incluso en pedidos existentes.
    '''
    ''' ESCENARIO:
    ''' - Pedido en BD tiene contactoCobro = "0"
    ''' - Cliente tiene contacto = "6"
    ''' - Al cargar el pedido, ClienteCompleto se asigna y sobrescribe contactoCobro = "6"
    ''' - El snapshot tiene "0", el pedido ahora tiene "6" → detecta cambios falsos
    '''
    ''' REGLA DE NEGOCIO: En pedidos EXISTENTES, contactoCobro NO debe sobrescribirse
    ''' al asignar ClienteCompleto. Solo debe hacerse para pedidos NUEVOS.
    '''
    ''' FIX REQUERIDO: En DetallePedidoViewModel.vb línea 266, añadir protección EstaCreandoPedido
    ''' </summary>
    <TestMethod()>
    Public Sub ContactoCobro_PedidoExistente_NoDebeSobrescribirseAlAsignarCliente()
        ' Arrange - Simula un pedido existente cargado de BD
        Dim pedidoCargadoDeBD As New PedidoVentaDTO() With {
            .empresa = "1",
            .numero = 909349, ' Pedido EXISTENTE (numero > 0)
            .cliente = "15191",
            .contacto = "0",
            .contactoCobro = "0", ' Valor guardado en BD
            .formaPago = "TRN",
            .plazosPago = "30D  "
        }

        ' Crear snapshot ANTES de que se asigne ClienteCompleto
        Dim snapshot = pedidoCargadoDeBD.CrearSnapshot()

        ' Act - Llamar al método extraído que decide si sobrescribir
        Dim contactoDelCliente = "6" ' El cliente tiene contacto diferente
        Dim esPedidoNuevo = (pedidoCargadoDeBD.numero = 0)

        If PedidoVentaDTO.DebeSobrescribirDatosCliente(esPedidoNuevo) Then
            pedidoCargadoDeBD.contactoCobro = contactoDelCliente
        End If

        ' Assert - El pedido debe seguir siendo igual al snapshot
        Assert.IsTrue(pedidoCargadoDeBD.Equals(snapshot),
            $"ContactoCobro no debe cambiar en pedidos existentes. " &
            $"Esperado: '{snapshot.contactoCobro}', Actual: '{pedidoCargadoDeBD.contactoCobro}'")
    End Sub

    ''' <summary>
    ''' Verifica que en pedidos NUEVOS sí se debe copiar el contactoCobro del cliente.
    ''' </summary>
    <TestMethod()>
    Public Sub ContactoCobro_PedidoNuevo_SiDebeSobrescribirseAlAsignarCliente()
        ' Arrange - Simula un pedido NUEVO (numero = 0)
        Dim pedidoNuevo As New PedidoVentaDTO() With {
            .empresa = "1",
            .numero = 0, ' Pedido NUEVO
            .cliente = "15191",
            .contacto = "0",
            .contactoCobro = "", ' Vacío en pedido nuevo
            .formaPago = "TRN",
            .plazosPago = "30D  "
        }

        ' Act - Llamar al método extraído
        Dim contactoDelCliente = "6"
        Dim esPedidoNuevo = (pedidoNuevo.numero = 0)

        If PedidoVentaDTO.DebeSobrescribirDatosCliente(esPedidoNuevo) Then
            pedidoNuevo.contactoCobro = contactoDelCliente
        End If

        ' Assert - En pedidos nuevos, contactoCobro debe actualizarse
        Assert.AreEqual(contactoDelCliente, pedidoNuevo.contactoCobro,
            "En pedidos nuevos, contactoCobro SÍ debe copiarse del cliente")
    End Sub

#End Region

#Region "ObtenerCamposDiferentes - diagnóstico (Issue #254)"

    ''' <summary>
    ''' Caso real pedido 917517: el snapshot guardó ccc='1  ' (char de BD con
    ''' espacios) y SelectorCCC reseteó pedido.Model.ccc a Nothing al cargar.
    ''' Debe detectarse como diferencia real y reportar el campo y los valores.
    ''' </summary>
    <TestMethod()>
    Public Sub ObtenerCamposDiferentes_CccPadConEspaciosVsNothing_LoReporta()
        ' Arrange
        Dim snapshot As New PedidoVentaDTO() With {.ccc = "1  "}
        Dim actual As New PedidoVentaDTO() With {.ccc = Nothing}

        ' Act
        Dim difs = actual.ObtenerCamposDiferentes(snapshot)

        ' Assert
        Assert.AreEqual(1, difs.Count)
        Assert.IsTrue(difs(0).StartsWith("ccc:"), $"Esperado que empiece por 'ccc:', fue: {difs(0)}")
        Assert.IsTrue(difs(0).Contains("'1  '"), $"Debe mostrar el valor del snapshot '1  ', fue: {difs(0)}")
        Assert.IsTrue(difs(0).Contains("<Nothing>"), $"Debe mostrar <Nothing> como valor actual, fue: {difs(0)}")
    End Sub

    ''' <summary>
    ''' Diferencia solo de espacios ('1  ' vs '1'): NO debe reportarse, en sync
    ''' con Equals/StringsIguales (los char de BD vienen con padding).
    ''' </summary>
    <TestMethod()>
    Public Sub ObtenerCamposDiferentes_CccSoloEspacios_NoLoReporta()
        ' Arrange
        Dim snapshot As New PedidoVentaDTO() With {.ccc = "1  "}
        Dim actual As New PedidoVentaDTO() With {.ccc = "1"}

        ' Act
        Dim difs = actual.ObtenerCamposDiferentes(snapshot)

        ' Assert
        Assert.AreEqual(0, difs.Count, $"No debía reportar nada; reportó: {String.Join(" | ", difs)}")
    End Sub

    <TestMethod()>
    Public Sub ObtenerCamposDiferentes_PedidosIguales_ListaVacia()
        ' Arrange
        Dim p As New PedidoVentaDTO() With {.formaPago = "TRN", .ccc = "ES123", .iva = "G21"}
        Dim snapshot = p.CrearSnapshot()

        ' Act
        Dim difs = p.ObtenerCamposDiferentes(snapshot)

        ' Assert
        Assert.AreEqual(0, difs.Count)
    End Sub

    <TestMethod()>
    Public Sub ObtenerCamposDiferentes_SnapshotNothing_DevuelveMarcador()
        ' Arrange
        Dim actual As New PedidoVentaDTO() With {.ccc = "1"}

        ' Act
        Dim difs = actual.ObtenerCamposDiferentes(Nothing)

        ' Assert
        Assert.AreEqual(1, difs.Count)
        Assert.IsTrue(difs(0).Contains("snapshot es Nothing"), $"Fue: {difs(0)}")
    End Sub

    <TestMethod()>
    Public Sub ObtenerCamposDiferentes_CampoNoString_SeReporta()
        ' Arrange
        Dim snapshot As New PedidoVentaDTO() With {.servirJunto = True}
        Dim actual As New PedidoVentaDTO() With {.servirJunto = False}

        ' Act
        Dim difs = actual.ObtenerCamposDiferentes(snapshot)

        ' Assert
        Assert.AreEqual(1, difs.Count)
        Assert.IsTrue(difs(0).StartsWith("servirJunto:"), $"Fue: {difs(0)}")
    End Sub

    ''' <summary>
    ''' Comentarios multilínea: se escapan los saltos de línea para que la
    ''' traza quepa en una sola línea de la ventana de salida.
    ''' </summary>
    <TestMethod()>
    Public Sub ObtenerCamposDiferentes_ComentariosMultilinea_EscapaSaltos()
        ' Arrange
        Dim snapshot As New PedidoVentaDTO() With {.comentarios = "antiguo"}
        Dim actual As New PedidoVentaDTO() With {.comentarios = "linea1" & vbCrLf & "linea2"}

        ' Act
        Dim difs = actual.ObtenerCamposDiferentes(snapshot)

        ' Assert
        Assert.AreEqual(1, difs.Count)
        Assert.IsTrue(difs(0).StartsWith("comentarios:"), $"Fue: {difs(0)}")
        Assert.IsFalse(difs(0).Contains(vbLf), "No debe contener saltos de línea reales")
        Assert.IsTrue(difs(0).Contains("\r") OrElse difs(0).Contains("\n"), $"Debe escapar los saltos, fue: {difs(0)}")
    End Sub

    ''' <summary>
    ''' Consistencia: ObtenerCamposDiferentes está vacío si y solo si Equals
    ''' devuelve True. Garantiza que no se desincronice del Equals.
    ''' </summary>
    <TestMethod()>
    Public Sub ObtenerCamposDiferentes_ConsistenteConEquals()
        ' Arrange - varios campos distintos
        Dim a As New PedidoVentaDTO() With {.formaPago = "TRN", .ccc = "1  ", .iva = "G21", .servirJunto = True}
        Dim b As New PedidoVentaDTO() With {.formaPago = "EFC", .ccc = Nothing, .iva = "G21", .servirJunto = False}

        ' Act & Assert - distintos
        Assert.IsFalse(a.Equals(b))
        Assert.IsTrue(a.ObtenerCamposDiferentes(b).Count > 0)

        ' Act & Assert - iguales (snapshot de sí mismo)
        Dim snap = a.CrearSnapshot()
        Assert.IsTrue(a.Equals(snap))
        Assert.AreEqual(0, a.ObtenerCamposDiferentes(snap).Count)
    End Sub

#End Region

#Region "CCC asignado automáticamente por Recibo (RCB) - mensaje explicativo"

    ''' <summary>
    ''' Caso real pedido 918386: forma de pago Recibo (RCB) con CCC a NULL en BD.
    ''' Al abrir el detalle, el SelectorCCC le asigna automáticamente el CCC del
    ''' cliente (RCB exige CCC), lo que dispara un falso "el pedido tiene cambios
    ''' sin guardar" aunque el usuario no toque nada. En ese escenario concreto
    ''' queremos un mensaje explicativo, no el genérico.
    ''' </summary>
    <TestMethod()>
    Public Sub ObtenerMensajeCccAsignadoPorRecibo_FormaPagoRcbSinCccEnSnapshot_DevuelveMensajeExplicativo()
        ' Arrange - snapshot tal como vino del API (RCB sin CCC)
        Dim snapshot As New PedidoVentaDTO() With {.formaPago = "RCB", .ccc = Nothing}
        ' Estado actual tras la auto-asignación del SelectorCCC
        Dim actual As New PedidoVentaDTO() With {.formaPago = "RCB", .ccc = "1"}

        ' Act
        Dim mensaje = actual.ObtenerMensajeCccAsignadoPorRecibo(snapshot)

        ' Assert
        Assert.IsNotNull(mensaje, "Debe devolver un mensaje explicativo, no Nothing")
        Assert.IsTrue(mensaje.Contains("RCB") OrElse mensaje.ToUpperInvariant().Contains("RECIBO"),
                      $"El mensaje debe mencionar la forma de pago Recibo. Fue: {mensaje}")
        Assert.IsTrue(mensaje.Contains("1"), $"El mensaje debe indicar el CCC asignado. Fue: {mensaje}")
    End Sub

    ''' <summary>
    ''' El CCC del pedido viene de un char de BD con padding ('1  '); el snapshot
    ''' "sin CCC" puede llegar como cadena vacía o con espacios. Debe seguir
    ''' detectándose como asignación automática por Recibo.
    ''' </summary>
    <TestMethod()>
    Public Sub ObtenerMensajeCccAsignadoPorRecibo_SnapshotConEspaciosYRcbConPadding_DevuelveMensaje()
        ' Arrange
        Dim snapshot As New PedidoVentaDTO() With {.formaPago = "RCB  ", .ccc = "   "}
        Dim actual As New PedidoVentaDTO() With {.formaPago = "RCB  ", .ccc = "1  "}

        ' Act
        Dim mensaje = actual.ObtenerMensajeCccAsignadoPorRecibo(snapshot)

        ' Assert
        Assert.IsNotNull(mensaje, "Empty/padding deben tratarse como 'sin CCC' y RCB con padding como Recibo")
    End Sub

    ''' <summary>
    ''' Si el snapshot YA tenía CCC, el cambio no es la auto-asignación por Recibo:
    ''' es un cambio real del usuario y debe usarse el mensaje genérico (Nothing).
    ''' </summary>
    <TestMethod()>
    Public Sub ObtenerMensajeCccAsignadoPorRecibo_SnapshotYaTeniaCcc_DevuelveNothing()
        ' Arrange
        Dim snapshot As New PedidoVentaDTO() With {.formaPago = "RCB", .ccc = "1"}
        Dim actual As New PedidoVentaDTO() With {.formaPago = "RCB", .ccc = "2"}

        ' Act
        Dim mensaje = actual.ObtenerMensajeCccAsignadoPorRecibo(snapshot)

        ' Assert
        Assert.IsNull(mensaje, "Cambiar de un CCC a otro no es la auto-asignación por Recibo")
    End Sub

    ''' <summary>
    ''' Si la forma de pago no es Recibo, asignar/cambiar el CCC no se explica por
    ''' la regla de Recibo; debe usarse el mensaje genérico (Nothing).
    ''' </summary>
    <TestMethod()>
    Public Sub ObtenerMensajeCccAsignadoPorRecibo_FormaPagoNoRcb_DevuelveNothing()
        ' Arrange
        Dim snapshot As New PedidoVentaDTO() With {.formaPago = "EFC", .ccc = Nothing}
        Dim actual As New PedidoVentaDTO() With {.formaPago = "EFC", .ccc = "1"}

        ' Act
        Dim mensaje = actual.ObtenerMensajeCccAsignadoPorRecibo(snapshot)

        ' Assert
        Assert.IsNull(mensaje, "Sin forma de pago Recibo no aplica el mensaje específico")
    End Sub

    ''' <summary>
    ''' Guard de robustez: snapshot Nothing no debe lanzar y devuelve Nothing.
    ''' </summary>
    <TestMethod()>
    Public Sub ObtenerMensajeCccAsignadoPorRecibo_SnapshotNothing_DevuelveNothing()
        ' Arrange
        Dim actual As New PedidoVentaDTO() With {.formaPago = "RCB", .ccc = "1"}

        ' Act
        Dim mensaje = actual.ObtenerMensajeCccAsignadoPorRecibo(Nothing)

        ' Assert
        Assert.IsNull(mensaje)
    End Sub

#End Region

#Region "Nesto#389: casilla avisar con importe al coger picking"

    <TestMethod()>
    Public Sub Equals_CambiarAvisarConImporteAlCogerPicking_DetectaElCambio()
        Dim pedido As New PedidoVentaDTO With {.avisarConImporteAlCogerPicking = False}
        Dim snapshot = pedido.CrearSnapshot()

        pedido.avisarConImporteAlCogerPicking = True

        Assert.IsFalse(pedido.Equals(snapshot))
        Assert.IsTrue(pedido.ObtenerCamposDiferentes(snapshot).Any(Function(d) d.Contains("avisarConImporteAlCogerPicking")))
    End Sub

    <TestMethod()>
    Public Sub CrearSnapshot_CopiaAvisarConImporteAlCogerPicking()
        Dim pedido As New PedidoVentaDTO With {.avisarConImporteAlCogerPicking = True}

        Dim snapshot = pedido.CrearSnapshot()

        Assert.IsTrue(snapshot.avisarConImporteAlCogerPicking)
        Assert.IsTrue(pedido.Equals(snapshot))
    End Sub

#End Region

End Class
