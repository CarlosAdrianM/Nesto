Imports Nesto.Models

''' <summary>
''' Tests para la lógica de inicialización de selectores FormaVenta y Almacén en DetallePedidoViewModel.
''' Carlos 09/12/25: Issue #253/#52
'''
''' Estos tests verifican el comportamiento esperado de InicializarFormaVentaParaLineas y
''' InicializarAlmacenParaLineas. Como la lógica está acoplada al ViewModel, los tests
''' documentan el comportamiento esperado usando PedidoVentaDTO directamente.
''' </summary>
<TestClass()>
Public Class DetallePedidoViewModelSelectoresTests

    Private Const VALOR_VARIOS As String = "VARIOS"

#Region "InicializarFormaVentaParaLineas - Tests"

    <TestMethod()>
    Public Sub FormaVenta_TodasLineasMismaFormaVenta_DevuelveLaFormaVenta()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = "TIE"})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = "TIE"})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = "TIE"})

        ' Act
        Dim resultado = CalcularFormaVentaParaLineas(pedido)

        ' Assert
        Assert.AreEqual("TIE", resultado)
    End Sub

    <TestMethod()>
    Public Sub FormaVenta_LineasConDiferentesFormasVenta_DevuelveVARIOS()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = "TIE"})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = "INT"})

        ' Act
        Dim resultado = CalcularFormaVentaParaLineas(pedido)

        ' Assert
        Assert.AreEqual(VALOR_VARIOS, resultado)
    End Sub

    <TestMethod()>
    Public Sub FormaVenta_SinLineas_DevuelveNothing()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        ' Sin líneas

        ' Act
        Dim resultado = CalcularFormaVentaParaLineas(pedido)

        ' Assert
        Assert.IsNull(resultado)
    End Sub

    <TestMethod()>
    Public Sub FormaVenta_LineasSinFormaVentaDefinida_DevuelveNothing()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = Nothing})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = ""})

        ' Act
        Dim resultado = CalcularFormaVentaParaLineas(pedido)

        ' Assert
        Assert.IsNull(resultado)
    End Sub

    <TestMethod()>
    Public Sub FormaVenta_FormaVentaConEspacios_ComparaTrimmed()
        ' Arrange - Simula valores de BD con espacios al final
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = "TIE "})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = "TIE"})

        ' Act
        Dim resultado = CalcularFormaVentaParaLineas(pedido)

        ' Assert - Deben considerarse iguales tras Trim
        Assert.AreEqual("TIE", resultado)
    End Sub

    <TestMethod()>
    Public Sub FormaVenta_NoEsSerieCursos_DevuelveLaFormaVentaIgual()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "NV" ' No es CV
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.formaVenta = "TIE"})

        ' Act
        Dim resultado = CalcularFormaVentaParaLineas(pedido)

        ' Assert - Carlos 09/12/25: Ahora muestra valores para TODAS las series (selector siempre visible, editable solo en CV)
        Assert.AreEqual("TIE", resultado)
    End Sub

#End Region

#Region "InicializarAlmacenParaLineas - Tests"

    <TestMethod()>
    Public Sub Almacen_TodasLineasMismoAlmacen_DevuelveElAlmacen()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG"})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG"})

        ' Act
        Dim resultado = CalcularAlmacenParaLineas(pedido)

        ' Assert
        Assert.AreEqual("ALG", resultado)
    End Sub

    <TestMethod()>
    Public Sub Almacen_LineasConDiferentesAlmacenes_DevuelveVARIOS()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG"})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "REI"})

        ' Act
        Dim resultado = CalcularAlmacenParaLineas(pedido)

        ' Assert
        Assert.AreEqual(VALOR_VARIOS, resultado)
    End Sub

    <TestMethod()>
    Public Sub Almacen_ConsideraTodasLasLineas_NoSoloFicticias()
        ' Arrange - Mezcla de ficticias y no ficticias, ambas cuentan ahora
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG", .EsFicticio = True})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "REI", .EsFicticio = False})

        ' Act
        Dim resultado = CalcularAlmacenParaLineas(pedido)

        ' Assert - Considera TODAS las líneas, así que hay diferentes almacenes
        Assert.AreEqual(VALOR_VARIOS, resultado)
    End Sub

    <TestMethod()>
    Public Sub Almacen_SinLineas_DevuelveNothing()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }

        ' Act
        Dim resultado = CalcularAlmacenParaLineas(pedido)

        ' Assert
        Assert.IsNull(resultado)
    End Sub

    <TestMethod()>
    Public Sub Almacen_AlmacenConEspacios_ComparaTrimmed()
        ' Arrange - Simula valores de BD con espacios
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG "})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG"})

        ' Act
        Dim resultado = CalcularAlmacenParaLineas(pedido)

        ' Assert
        Assert.AreEqual("ALG", resultado)
    End Sub

    <TestMethod()>
    Public Sub Almacen_NoEsSerieCursos_DevuelveElAlmacenIgual()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "NV" ' No es CV
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG"})

        ' Act
        Dim resultado = CalcularAlmacenParaLineas(pedido)

        ' Assert - Carlos 09/12/25: Ahora muestra valores para TODAS las series (selector siempre visible, editable solo en CV)
        Assert.AreEqual("ALG", resultado)
    End Sub

#End Region

#Region "Valores por defecto cuando no hay líneas"

    <TestMethod()>
    Public Sub FormaVenta_SinLineasPeroConDefecto_DevuelveDefecto()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        Dim formaVentaDefecto = "TIE"

        ' Act
        Dim resultado = CalcularFormaVentaParaLineasConDefecto(pedido, formaVentaDefecto)

        ' Assert - Si no hay líneas, usar el valor por defecto
        Assert.AreEqual("TIE", resultado)
    End Sub

    <TestMethod()>
    Public Sub Almacen_SinLineasPeroConDefecto_DevuelveDefecto()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        Dim almacenDefecto = "ALG"

        ' Act
        Dim resultado = CalcularAlmacenParaLineasConDefecto(pedido, almacenDefecto)

        ' Assert - Si no hay líneas, usar el valor por defecto
        Assert.AreEqual("ALG", resultado)
    End Sub

    <TestMethod()>
    Public Sub FormaVenta_SinLineasSerieNV_DevuelveDefecto()
        ' Arrange - Carlos 09/12/25: También funciona para series que no son CV
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "NV"
        }
        Dim formaVentaDefecto = "TIE"

        ' Act
        Dim resultado = CalcularFormaVentaParaLineasConDefecto(pedido, formaVentaDefecto)

        ' Assert
        Assert.AreEqual("TIE", resultado)
    End Sub

    <TestMethod()>
    Public Sub Almacen_SinLineasSerieNV_DevuelveDefecto()
        ' Arrange - Carlos 09/12/25: También funciona para series que no son CV
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "NV"
        }
        Dim almacenDefecto = "ALG"

        ' Act
        Dim resultado = CalcularAlmacenParaLineasConDefecto(pedido, almacenDefecto)

        ' Assert
        Assert.AreEqual("ALG", resultado)
    End Sub

#End Region

#Region "Editabilidad - Solo en serie CV"

    ''' <summary>
    ''' Verifica que EsSerieCursos devuelve True solo cuando la serie es "CV".
    ''' Carlos 09/12/25: Los selectores son siempre visibles pero solo editables cuando EsSerieCursos = True.
    ''' </summary>
    <TestMethod()>
    Public Sub EsSerieCursos_SerieCV_DevuelveTrue()
        ' Arrange
        Dim serie = "CV"

        ' Act
        Dim resultado = EsSerieCursos(serie)

        ' Assert
        Assert.IsTrue(resultado)
    End Sub

    <TestMethod()>
    Public Sub EsSerieCursos_SerieCVConEspacios_DevuelveTrue()
        ' Arrange - Simula valores de BD con espacios
        Dim serie = "CV "

        ' Act
        Dim resultado = EsSerieCursos(serie)

        ' Assert - Debe usar Trim()
        Assert.IsTrue(resultado)
    End Sub

    <TestMethod()>
    Public Sub EsSerieCursos_SerieNV_DevuelveFalse()
        ' Arrange
        Dim serie = "NV"

        ' Act
        Dim resultado = EsSerieCursos(serie)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub EsSerieCursos_SerieVacia_DevuelveFalse()
        ' Arrange
        Dim serie As String = ""

        ' Act
        Dim resultado = EsSerieCursos(serie)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub EsSerieCursos_SerieNothing_DevuelveFalse()
        ' Arrange
        Dim serie As String = Nothing

        ' Act
        Dim resultado = EsSerieCursos(serie)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

#End Region

#Region "Aplicar valores a líneas - Solo en CV y solo a líneas editables"

    ''' <summary>
    ''' Verifica que DebeAplicarValorALineas devuelve True solo cuando:
    ''' - La serie es CV
    ''' - El valor no es nulo/vacío
    ''' - El valor no es VARIOS
    ''' Carlos 09/12/25: Esta lógica está en los setters de FormaVentaSeleccionadaParaLineas y AlmacenSeleccionadoParaLineas.
    ''' </summary>
    <TestMethod()>
    Public Sub DebeAplicarValor_SerieCVValorValido_DevuelveTrue()
        ' Arrange
        Dim serie = "CV"
        Dim valor = "TIE"

        ' Act
        Dim resultado = DebeAplicarValorALineas(serie, valor)

        ' Assert
        Assert.IsTrue(resultado)
    End Sub

    <TestMethod()>
    Public Sub DebeAplicarValor_SerieNVValorValido_DevuelveFalse()
        ' Arrange - No es serie CV, no debe aplicar aunque el valor sea válido
        Dim serie = "NV"
        Dim valor = "TIE"

        ' Act
        Dim resultado = DebeAplicarValorALineas(serie, valor)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub DebeAplicarValor_SerieCVValorVARIOS_DevuelveFalse()
        ' Arrange - Valor VARIOS no se aplica a las líneas
        Dim serie = "CV"
        Dim valor = "VARIOS"

        ' Act
        Dim resultado = DebeAplicarValorALineas(serie, valor)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub DebeAplicarValor_SerieCVValorVacio_DevuelveFalse()
        ' Arrange - Valor vacío no se aplica a las líneas
        Dim serie = "CV"
        Dim valor = ""

        ' Act
        Dim resultado = DebeAplicarValorALineas(serie, valor)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub DebeAplicarValor_SerieCVValorNothing_DevuelveFalse()
        ' Arrange - Valor Nothing no se aplica a las líneas
        Dim serie = "CV"
        Dim valor As String = Nothing

        ' Act
        Dim resultado = DebeAplicarValorALineas(serie, valor)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

#End Region

#Region "HayLineasEditables y PuedeEditarSelectoresLinea"

    <TestMethod()>
    Public Sub HayLineasEditables_TodasFacturadas_DevuelveFalse()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.estado = 4})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.estado = 4})

        ' Act
        Dim resultado = HayLineasEditables(pedido)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub HayLineasEditables_AlgunasPendientes_DevuelveTrue()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.estado = 4})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.estado = 1})

        ' Act
        Dim resultado = HayLineasEditables(pedido)

        ' Assert
        Assert.IsTrue(resultado)
    End Sub

    <TestMethod()>
    Public Sub HayLineasEditables_SinLineas_DevuelveFalse()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }

        ' Act
        Dim resultado = HayLineasEditables(pedido)

        ' Assert
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub PuedeEditarSelectoresLinea_SerieCVConLineasEditables_DevuelveTrue()
        ' Arrange
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.estado = 1})

        ' Act
        Dim resultado = PuedeEditarSelectoresLinea(pedido)

        ' Assert
        Assert.IsTrue(resultado)
    End Sub

    <TestMethod()>
    Public Sub PuedeEditarSelectoresLinea_SerieCVSinLineasEditables_DevuelveFalse()
        ' Arrange - Serie CV pero todas las líneas facturadas
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.estado = 4})

        ' Act
        Dim resultado = PuedeEditarSelectoresLinea(pedido)

        ' Assert - No se puede editar aunque sea CV
        Assert.IsFalse(resultado)
    End Sub

    <TestMethod()>
    Public Sub PuedeEditarSelectoresLinea_SerieNVConLineasEditables_DevuelveFalse()
        ' Arrange - Serie NV con líneas editables
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "NV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.estado = 1})

        ' Act
        Dim resultado = PuedeEditarSelectoresLinea(pedido)

        ' Assert - No se puede editar porque no es CV
        Assert.IsFalse(resultado)
    End Sub

#End Region

#Region "Líneas facturadas - No se deben modificar"

    ''' <summary>
    ''' Verifica que las líneas facturadas (estado >= 4) NO se modifican al aplicar almacén.
    ''' Carlos 09/12/25: Bug detectado - cambiar almacén en pedido facturado borraba la serie.
    ''' </summary>
    <TestMethod()>
    Public Sub AplicarAlmacen_LineaFacturada_NoSeModifica()
        ' Arrange
        Dim linea As New LineaPedidoVentaDTO() With {
            .almacen = "ALG",
            .estado = 4 ' FACTURA
        }
        Dim nuevoAlmacen = "REI"

        ' Act
        Dim almacenOriginal = linea.almacen
        AplicarAlmacenALinea(linea, nuevoAlmacen)

        ' Assert - La línea facturada NO debe cambiar
        Assert.AreEqual("ALG", linea.almacen)
    End Sub

    <TestMethod()>
    Public Sub AplicarAlmacen_LineaAlbaraneada_NoSeModifica()
        ' Arrange
        Dim linea As New LineaPedidoVentaDTO() With {
            .almacen = "ALG",
            .estado = 2 ' ALBARAN
        }
        Dim nuevoAlmacen = "REI"

        ' Act
        AplicarAlmacenALinea(linea, nuevoAlmacen)

        ' Assert - La línea albaraneada NO debe cambiar
        Assert.AreEqual("ALG", linea.almacen)
    End Sub

    <TestMethod()>
    Public Sub AplicarAlmacen_LineaPendiente_SeModifica()
        ' Arrange
        Dim linea As New LineaPedidoVentaDTO() With {
            .almacen = "ALG",
            .estado = 1 ' SIN_FACTURAR / EN_CURSO
        }
        Dim nuevoAlmacen = "REI"

        ' Act
        AplicarAlmacenALinea(linea, nuevoAlmacen)

        ' Assert - La línea pendiente SÍ debe cambiar
        Assert.AreEqual("REI", linea.almacen)
    End Sub

    <TestMethod()>
    Public Sub AplicarAlmacen_LineaPresupuesto_SeModifica()
        ' Arrange
        Dim linea As New LineaPedidoVentaDTO() With {
            .almacen = "ALG",
            .estado = -1 ' PENDIENTE (presupuesto)
        }
        Dim nuevoAlmacen = "REI"

        ' Act
        AplicarAlmacenALinea(linea, nuevoAlmacen)

        ' Assert - La línea de presupuesto SÍ debe cambiar
        Assert.AreEqual("REI", linea.almacen)
    End Sub

    <TestMethod()>
    Public Sub AplicarFormaVenta_LineaFacturada_NoSeModifica()
        ' Arrange
        Dim linea As New LineaPedidoVentaDTO() With {
            .formaVenta = "TIE",
            .estado = 4 ' FACTURA
        }
        Dim nuevaFormaVenta = "INT"

        ' Act
        AplicarFormaVentaALinea(linea, nuevaFormaVenta)

        ' Assert - La línea facturada NO debe cambiar
        Assert.AreEqual("TIE", linea.formaVenta)
    End Sub

    <TestMethod()>
    Public Sub AplicarFormaVenta_LineaPendiente_SeModifica()
        ' Arrange
        Dim linea As New LineaPedidoVentaDTO() With {
            .formaVenta = "TIE",
            .estado = 1 ' SIN_FACTURAR / EN_CURSO
        }
        Dim nuevaFormaVenta = "INT"

        ' Act
        AplicarFormaVentaALinea(linea, nuevaFormaVenta)

        ' Assert - La línea pendiente SÍ debe cambiar
        Assert.AreEqual("INT", linea.formaVenta)
    End Sub

    <TestMethod()>
    Public Sub AplicarAlmacen_PedidoMixto_SoloModificaLineasEditables()
        ' Arrange - Pedido con líneas en diferentes estados
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG", .estado = 4}) ' Facturada
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG", .estado = 2}) ' Albaraneada
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG", .estado = 1}) ' Pendiente
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG", .estado = -1}) ' Presupuesto

        Dim nuevoAlmacen = "REI"

        ' Act
        AplicarAlmacenAPedido(pedido, nuevoAlmacen)

        ' Assert - Solo las líneas editables (estado < 2) deben cambiar
        Assert.AreEqual("ALG", pedido.Lineas(0).almacen, "Línea facturada NO debe cambiar")
        Assert.AreEqual("ALG", pedido.Lineas(1).almacen, "Línea albaraneada NO debe cambiar")
        Assert.AreEqual("REI", pedido.Lineas(2).almacen, "Línea pendiente SÍ debe cambiar")
        Assert.AreEqual("REI", pedido.Lineas(3).almacen, "Línea presupuesto SÍ debe cambiar")
    End Sub

    <TestMethod()>
    Public Sub AplicarAlmacen_TodasLineasFacturadas_NingunaSeModifica()
        ' Arrange - Pedido completamente facturado
        Dim pedido As New PedidoVentaDTO() With {
            .serie = "CV"
        }
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG", .estado = 4})
        pedido.Lineas.Add(New LineaPedidoVentaDTO() With {.almacen = "ALG", .estado = 4})

        Dim nuevoAlmacen = "REI"

        ' Act
        AplicarAlmacenAPedido(pedido, nuevoAlmacen)

        ' Assert - Ninguna línea debe cambiar
        Assert.IsTrue(pedido.Lineas.All(Function(l) l.almacen = "ALG"))
    End Sub

#End Region

#Region "Métodos auxiliares que replican la lógica del ViewModel"

    ''' <summary>
    ''' Replica la lógica de InicializarFormaVentaParaLineas del ViewModel.
    ''' </summary>
    Private Function CalcularFormaVentaParaLineas(pedido As PedidoVentaDTO) As String
        Return CalcularFormaVentaParaLineasConDefecto(pedido, Nothing)
    End Function

    ''' <summary>
    ''' Replica la lógica de InicializarFormaVentaParaLineas con valor por defecto.
    ''' Carlos 09/12/25: Ya no comprueba serie CV - selector siempre visible, editable solo en CV.
    ''' </summary>
    Private Function CalcularFormaVentaParaLineasConDefecto(pedido As PedidoVentaDTO, formaVentaDefecto As String) As String
        If pedido Is Nothing Then
            Return Nothing
        End If

        If pedido.Lineas Is Nothing OrElse Not pedido.Lineas.Any() Then
            Return formaVentaDefecto ' Usar defecto si no hay líneas
        End If

        ' Obtener formas de venta distintas (no nulas ni vacías)
        Dim formasVentaDistintas = pedido.Lineas _
            .Where(Function(l) Not String.IsNullOrWhiteSpace(l.formaVenta)) _
            .Select(Function(l) l.formaVenta.Trim()) _
            .Distinct() _
            .ToList()

        If formasVentaDistintas.Count = 0 Then
            Return formaVentaDefecto ' Usar defecto si ninguna línea tiene forma de venta
        ElseIf formasVentaDistintas.Count = 1 Then
            Return formasVentaDistintas.First()
        Else
            Return VALOR_VARIOS
        End If
    End Function

    ''' <summary>
    ''' Replica la lógica de InicializarAlmacenParaLineas del ViewModel.
    ''' </summary>
    Private Function CalcularAlmacenParaLineas(pedido As PedidoVentaDTO) As String
        Return CalcularAlmacenParaLineasConDefecto(pedido, Nothing)
    End Function

    ''' <summary>
    ''' Replica la lógica de InicializarAlmacenParaLineas con valor por defecto.
    ''' Carlos 09/12/25: Ahora considera TODAS las líneas, no solo las ficticias.
    ''' Carlos 09/12/25: Ya no comprueba serie CV - selector siempre visible, editable solo en CV.
    ''' </summary>
    Private Function CalcularAlmacenParaLineasConDefecto(pedido As PedidoVentaDTO, almacenDefecto As String) As String
        If pedido Is Nothing Then
            Return Nothing
        End If

        If pedido.Lineas Is Nothing OrElse Not pedido.Lineas.Any() Then
            Return almacenDefecto ' Usar defecto si no hay líneas
        End If

        ' Obtener almacenes distintos de TODAS las líneas (no nulos ni vacíos)
        Dim almacenesDistintos = pedido.Lineas _
            .Where(Function(l) Not String.IsNullOrWhiteSpace(l.almacen)) _
            .Select(Function(l) l.almacen.Trim()) _
            .Distinct() _
            .ToList()

        If almacenesDistintos.Count = 0 Then
            Return almacenDefecto ' Usar defecto si no hay líneas con almacén definido
        ElseIf almacenesDistintos.Count = 1 Then
            Return almacenesDistintos.First()
        Else
            Return VALOR_VARIOS
        End If
    End Function

    ''' <summary>
    ''' Replica la lógica de EsSerieCursos del ViewModel.
    ''' Carlos 09/12/25: Determina si los selectores son editables.
    ''' </summary>
    Private Function EsSerieCursos(serie As String) As Boolean
        Return serie?.Trim() = "CV"
    End Function

    ''' <summary>
    ''' Replica la lógica de los setters de FormaVentaSeleccionadaParaLineas y AlmacenSeleccionadoParaLineas.
    ''' Carlos 09/12/25: Determina si un valor debe aplicarse a las líneas del pedido.
    ''' Solo aplica cuando:
    ''' - La serie es CV (EsSerieCursos = True)
    ''' - El valor no es nulo ni vacío
    ''' - El valor no es VARIOS
    ''' </summary>
    Private Function DebeAplicarValorALineas(serie As String, valor As String) As Boolean
        ' No aplicar si no es serie CV
        If Not EsSerieCursos(serie) Then
            Return False
        End If

        ' No aplicar si el valor es nulo, vacío o VARIOS
        If String.IsNullOrEmpty(valor) OrElse valor = VALOR_VARIOS Then
            Return False
        End If

        Return True
    End Function

    ''' <summary>
    ''' Constante para el estado de línea albaraneada (estado >= 2 no se puede modificar).
    ''' </summary>
    Private Const ESTADO_ALBARANEADA As Integer = 2

    ''' <summary>
    ''' Replica la lógica de HayLineasEditables del ViewModel.
    ''' Carlos 09/12/25: Indica si hay al menos una línea que no esté albaraneada/facturada.
    ''' </summary>
    Private Function HayLineasEditables(pedido As PedidoVentaDTO) As Boolean
        If pedido Is Nothing OrElse pedido.Lineas Is Nothing Then
            Return False
        End If
        Return pedido.Lineas.Any(Function(l) l.estado < ESTADO_ALBARANEADA)
    End Function

    ''' <summary>
    ''' Replica la lógica de PuedeEditarSelectoresLinea del ViewModel.
    ''' Carlos 09/12/25: Combina EsSerieCursos Y HayLineasEditables.
    ''' </summary>
    Private Function PuedeEditarSelectoresLinea(pedido As PedidoVentaDTO) As Boolean
        Return EsSerieCursos(pedido?.serie) AndAlso HayLineasEditables(pedido)
    End Function

    ''' <summary>
    ''' Determina si una línea puede ser editada (no está albaraneada ni facturada).
    ''' Carlos 09/12/25: Las líneas con estado >= 2 no se pueden modificar.
    ''' </summary>
    Private Function PuedeEditarseLinea(linea As LineaPedidoVentaDTO) As Boolean
        Return linea.estado < ESTADO_ALBARANEADA
    End Function

    ''' <summary>
    ''' Aplica almacén a una línea individual solo si es editable.
    ''' Carlos 09/12/25: Replica la lógica corregida de AplicarAlmacenALineas.
    ''' </summary>
    Private Sub AplicarAlmacenALinea(linea As LineaPedidoVentaDTO, almacen As String)
        If PuedeEditarseLinea(linea) Then
            linea.almacen = almacen
        End If
    End Sub

    ''' <summary>
    ''' Aplica forma de venta a una línea individual solo si es editable.
    ''' Carlos 09/12/25: Replica la lógica corregida de AplicarFormaVentaALineas.
    ''' </summary>
    Private Sub AplicarFormaVentaALinea(linea As LineaPedidoVentaDTO, formaVenta As String)
        If PuedeEditarseLinea(linea) Then
            linea.formaVenta = formaVenta
        End If
    End Sub

    ''' <summary>
    ''' Aplica almacén a todas las líneas editables de un pedido.
    ''' Carlos 09/12/25: Replica la lógica corregida de AplicarAlmacenALineas del ViewModel.
    ''' </summary>
    Private Sub AplicarAlmacenAPedido(pedido As PedidoVentaDTO, almacen As String)
        If pedido Is Nothing OrElse pedido.Lineas Is Nothing Then
            Return
        End If

        For Each linea In pedido.Lineas
            AplicarAlmacenALinea(linea, almacen)
        Next
    End Sub

    ''' <summary>
    ''' Aplica forma de venta a todas las líneas editables de un pedido.
    ''' Carlos 09/12/25: Replica la lógica corregida de AplicarFormaVentaALineas del ViewModel.
    ''' </summary>
    Private Sub AplicarFormaVentaAPedido(pedido As PedidoVentaDTO, formaVenta As String)
        If pedido Is Nothing OrElse pedido.Lineas Is Nothing Then
            Return
        End If

        For Each linea In pedido.Lineas
            AplicarFormaVentaALinea(linea, formaVenta)
        Next
    End Sub

#End Region

End Class
