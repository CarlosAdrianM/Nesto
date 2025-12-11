Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta

''' <summary>
''' Tests para LineaPedidoVentaWrapper - Issue #258
''' Verifican la gestión de diferentes tipos de línea y las reglas de validación.
'''
''' Estado: Pendiente de más pruebas en producción antes de dar por finalizada la Issue #258.
'''
''' Tipos de línea soportados:
''' - 0: Texto (solo campo texto editable)
''' - 1: Producto (busca en API, rellena precio/nombre)
''' - 2: Cuenta contable (formato abreviado 572.13 → 57200013)
''' - 3: Inmovilizado (similar a cuenta contable)
'''
''' Funcionalidades implementadas:
''' - ComboBox para TipoLinea con valores válidos (0-3)
''' - Reglas de limpieza al cambiar TipoLinea
''' - Validación en setters para prevenir estados incoherentes
''' - Columnas deshabilitadas visualmente para líneas de texto
''' - Herencia de TipoLinea en líneas nuevas
''' - Auto-foco a columna Texto cuando TipoLinea=0
''' - Navegación Enter inteligente según TipoLinea
'''
''' Pendiente de probar en producción:
''' - Flujo completo de entrada de múltiples líneas de texto
''' - Flujo completo de entrada de múltiples productos
''' - Cambio de tipo en líneas existentes
''' - Guardado y carga de pedidos con diferentes tipos de línea
''' </summary>
<TestClass()>
Public Class LineaPedidoVentaWrapperTests

#Region "Constructor y valores por defecto"

    <TestMethod()>
    Public Sub Constructor_PorDefecto_TipoLineaEsProducto()
        ' Arrange & Act
        Dim linea As New LineaPedidoVentaWrapper()

        ' Assert
        Assert.AreEqual(CByte(1), linea.tipoLinea)
    End Sub

    <TestMethod()>
    Public Sub Constructor_PorDefecto_CantidadEsUno()
        ' Arrange & Act
        Dim linea As New LineaPedidoVentaWrapper()

        ' Assert
        Assert.AreEqual(CShort(1), linea.Cantidad)
    End Sub

    <TestMethod()>
    Public Sub Constructor_ConModel_UsaModelProporcionado()
        ' Arrange
        Dim model As New LineaPedidoVentaDTO() With {
            .tipoLinea = 2,
            .Producto = "57200013",
            .texto = "Caja efectivo"
        }

        ' Act
        Dim linea As New LineaPedidoVentaWrapper(model)

        ' Assert
        Assert.AreEqual(CByte(2), linea.tipoLinea)
        Assert.AreEqual("57200013", linea.Producto)
        Assert.AreEqual("Caja efectivo", linea.texto)
    End Sub

#End Region

#Region "TipoLinea = 0 (Texto) - Reglas de limpieza"

    <TestMethod()>
    Public Sub CambiarATipoTexto_LimpiaProducto()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.Producto = "17404"
        linea.Model.texto = "LOREAL PROFESSIONNEL"
        linea.Model.PrecioUnitario = 25.5D
        linea.Model.Cantidad = 5

        ' Act
        linea.tipoLinea = 0

        ' Assert
        Assert.AreEqual(String.Empty, linea.Producto)
        Assert.AreEqual(String.Empty, linea.texto)
        Assert.AreEqual(0D, linea.PrecioUnitario)
        Assert.AreEqual(CShort(0), linea.Cantidad)
    End Sub

    <TestMethod()>
    Public Sub CambiarATipoTexto_LimpiaDescuentos()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.DescuentoLinea = 0.1D
        linea.Model.DescuentoProducto = 0.05D
        linea.Model.AplicarDescuento = True

        ' Act
        linea.tipoLinea = 0

        ' Assert
        Assert.AreEqual(0D, linea.DescuentoLinea)
        Assert.AreEqual(0D, linea.DescuentoProducto)
        Assert.IsFalse(linea.AplicarDescuento)
    End Sub

    <TestMethod()>
    Public Sub TipoTexto_NoPermiteEstablecerProducto()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.tipoLinea = 0

        ' Act
        linea.Producto = "17404"

        ' Assert - El producto debe seguir vacío
        Assert.AreEqual(String.Empty, linea.Producto)
    End Sub

    <TestMethod()>
    Public Sub TipoTexto_NoPermiteEstablecerCantidad()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.tipoLinea = 0

        ' Act
        linea.Cantidad = 5

        ' Assert - La cantidad debe seguir en 0
        Assert.AreEqual(CShort(0), linea.Cantidad)
    End Sub

    <TestMethod()>
    Public Sub TipoTexto_NoPermiteEstablecerPrecio()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.tipoLinea = 0

        ' Act
        linea.PrecioUnitario = 25.5D

        ' Assert - El precio debe seguir en 0
        Assert.AreEqual(0D, linea.PrecioUnitario)
    End Sub

    <TestMethod()>
    Public Sub TipoTexto_PermiteEstablecerTexto()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.tipoLinea = 0

        ' Act
        linea.texto = "Línea separadora"

        ' Assert - El texto sí debe establecerse
        Assert.AreEqual("Línea separadora", linea.texto)
    End Sub

#End Region

#Region "TipoLinea = 1 (Producto) - Reglas"

    <TestMethod()>
    Public Sub CambiarAProducto_DesdeTexto_ActivaAplicarDescuento()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.tipoLinea = 0

        ' Act
        linea.tipoLinea = 1

        ' Assert
        Assert.IsTrue(linea.AplicarDescuento)
    End Sub

    <TestMethod()>
    Public Sub CambiarAProducto_DesdeCuenta_LimpiaCuentaContable()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.tipoLinea = 2
        linea.Model.Producto = "57200013"
        linea.Model.texto = "Caja efectivo"

        ' Act
        linea.tipoLinea = 1

        ' Assert - Debe limpiar porque 57200013 es cuenta contable (8 dígitos numéricos)
        Assert.AreEqual(String.Empty, linea.Producto)
        Assert.AreEqual(String.Empty, linea.texto)
    End Sub

    <TestMethod()>
    Public Sub TipoProducto_PermiteEstablecerProducto()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.tipoLinea = 1

        ' Act
        linea.Producto = "17404"

        ' Assert
        Assert.AreEqual("17404", linea.Producto)
    End Sub

    <TestMethod()>
    Public Sub TipoProducto_PermiteEstablecerCantidad()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.tipoLinea = 1

        ' Act
        linea.Cantidad = 5

        ' Assert
        Assert.AreEqual(CShort(5), linea.Cantidad)
    End Sub

#End Region

#Region "TipoLinea = 2 (Cuenta contable) - Reglas"

    <TestMethod()>
    Public Sub CambiarACuenta_DesdeProducto_LimpiaProducto()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.tipoLinea = 1
        linea.Model.Producto = "17404"
        linea.Model.texto = "LOREAL PROFESSIONNEL"

        ' Act
        linea.tipoLinea = 2

        ' Assert - Debe limpiar porque 17404 no es cuenta contable válida (no son 8 dígitos)
        Assert.AreEqual(String.Empty, linea.Producto)
        Assert.AreEqual(String.Empty, linea.texto)
    End Sub

    <TestMethod()>
    Public Sub CambiarACuenta_DesactivaAplicarDescuento()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.AplicarDescuento = True

        ' Act
        linea.tipoLinea = 2

        ' Assert
        Assert.IsFalse(linea.AplicarDescuento)
    End Sub

    <TestMethod()>
    Public Sub CambiarACuenta_LimpiaDescuentos()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.DescuentoLinea = 0.1D
        linea.Model.DescuentoProducto = 0.05D

        ' Act
        linea.tipoLinea = 2

        ' Assert
        Assert.AreEqual(0D, linea.DescuentoLinea)
        Assert.AreEqual(0D, linea.DescuentoProducto)
    End Sub

    <TestMethod()>
    Public Sub CambiarACuenta_MantieneCuentaExistente()
        ' Arrange - Si ya tiene una cuenta válida, no debe limpiarla
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.tipoLinea = 2
        linea.Model.Producto = "57200013"
        linea.Model.texto = "Caja efectivo"

        ' Act - Cambiar a cuenta (aunque ya es cuenta)
        linea.tipoLinea = 2

        ' Assert - Debe mantener la cuenta porque ya es válida
        Assert.AreEqual("57200013", linea.Producto)
        Assert.AreEqual("Caja efectivo", linea.texto)
    End Sub

#End Region

#Region "TipoLinea = 3 (Inmovilizado) - Reglas"

    <TestMethod()>
    Public Sub CambiarAInmovilizado_DesdeProducto_LimpiaProducto()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.tipoLinea = 1
        linea.Model.Producto = "17404"

        ' Act
        linea.tipoLinea = 3

        ' Assert
        Assert.AreEqual(String.Empty, linea.Producto)
    End Sub

    <TestMethod()>
    Public Sub CambiarAInmovilizado_DesactivaAplicarDescuento()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.AplicarDescuento = True

        ' Act
        linea.tipoLinea = 3

        ' Assert
        Assert.IsFalse(linea.AplicarDescuento)
    End Sub

#End Region

#Region "Escenarios de uso real"

    <TestMethod()>
    Public Sub EscenarioReal_CrearLineaTextoSeparadora()
        ' Simula: usuario quiere añadir una línea de texto para separar secciones
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()

        ' Act
        linea.tipoLinea = 0
        linea.texto = "--- PRODUCTOS DE PELUQUERÍA ---"

        ' Assert
        Assert.AreEqual(CByte(0), linea.tipoLinea)
        Assert.AreEqual("--- PRODUCTOS DE PELUQUERÍA ---", linea.texto)
        Assert.AreEqual(String.Empty, linea.Producto)
        Assert.AreEqual(CShort(0), linea.Cantidad)
        Assert.AreEqual(0D, linea.PrecioUnitario)
    End Sub

    <TestMethod()>
    Public Sub EscenarioReal_CambiarProductoACuenta()
        ' Simula: usuario se equivocó y quiere cambiar una línea de producto a cuenta contable
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.tipoLinea = 1
        linea.Model.Producto = "17404"
        linea.Model.texto = "LOREAL PROFESSIONNEL"
        linea.Model.PrecioUnitario = 25.5D
        linea.Model.Cantidad = 5
        linea.Model.DescuentoLinea = 0.1D
        linea.Model.AplicarDescuento = True

        ' Act
        linea.tipoLinea = 2

        ' Assert - Debe limpiar datos de producto y descuentos
        Assert.AreEqual(String.Empty, linea.Producto)
        Assert.AreEqual(String.Empty, linea.texto)
        Assert.AreEqual(0D, linea.DescuentoLinea)
        Assert.IsFalse(linea.AplicarDescuento)
        ' Nota: Precio y Cantidad se mantienen para que el usuario los rellene con la cuenta
    End Sub

    <TestMethod()>
    Public Sub EscenarioReal_MultiplesLineasTexto()
        ' Simula: usuario crea varias líneas de texto seguidas
        ' Arrange & Act
        Dim linea1 As New LineaPedidoVentaWrapper()
        linea1.tipoLinea = 0
        linea1.texto = "Sección 1"

        Dim linea2 As New LineaPedidoVentaWrapper()
        linea2.tipoLinea = 0
        linea2.texto = "Sección 2"

        Dim linea3 As New LineaPedidoVentaWrapper()
        linea3.tipoLinea = 0
        linea3.texto = "Sección 3"

        ' Assert
        Assert.AreEqual("Sección 1", linea1.texto)
        Assert.AreEqual("Sección 2", linea2.texto)
        Assert.AreEqual("Sección 3", linea3.texto)
        ' Todas deben tener tipo 0 y campos numéricos en 0
        Assert.IsTrue(linea1.Cantidad = 0 AndAlso linea2.Cantidad = 0 AndAlso linea3.Cantidad = 0)
    End Sub

#End Region

#Region "Casos especiales"

    <TestMethod()>
    Public Sub CambiarTipoLinea_AlMismoValor_NoLimpia()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.tipoLinea = 1
        linea.Model.Producto = "17404"
        linea.Model.texto = "LOREAL PROFESSIONNEL"

        ' Act - Cambiar al mismo tipo (no debería hacer nada)
        linea.tipoLinea = 1

        ' Assert - Los datos deben mantenerse
        Assert.AreEqual("17404", linea.Producto)
        Assert.AreEqual("LOREAL PROFESSIONNEL", linea.texto)
    End Sub

    <TestMethod()>
    Public Sub TipoLinea_Null_NoAplicaReglas()
        ' Arrange
        Dim linea As New LineaPedidoVentaWrapper()
        linea.Model.Producto = "17404"

        ' Act
        linea.tipoLinea = Nothing

        ' Assert - El producto debe mantenerse
        Assert.AreEqual("17404", linea.Producto)
    End Sub

#End Region

End Class
