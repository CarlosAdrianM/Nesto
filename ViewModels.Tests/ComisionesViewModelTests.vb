Imports Nesto.ViewModels
Imports Nesto.Models
'Imports Nesto.Models.Nesto.Models.EF
Imports Nesto.ViewModels.ComisionesViewModel

<TestClass()> Public Class CuandoCambiamosLasFechas

    Dim comVM As New ComisionesViewModel

    <TestMethod()> Public Sub ElMesDeAgostoDebeEmpezarElDia1()
        'arrange

        'act
        comVM.mesActual = "Agosto"
        Dim fechaTest As Date = DateSerial(Now.Year, 8, 1)
        If fechaTest > Now Then
            fechaTest = fechaTest.AddYears(-1)
        End If

        'assert
        Assert.IsTrue(comVM.fechaDesde = fechaTest)
    End Sub

    <TestMethod()> Public Sub ElMesDeAgostoDebeAcabarElDia31()
        comVM.mesActual = "Agosto"
        Dim fechaTest As Date = DateSerial(Now.Year, 8, 31)
        If DateSerial(Now.Year, 8, 1) > Now Then
            fechaTest = fechaTest.AddYears(-1)
        End If
        Assert.IsTrue(comVM.fechaHasta = fechaTest)
    End Sub

    <TestMethod()> Public Sub ElMesDeAbrilDebeAcabarElDia30()
        comVM.mesActual = "Abril"
        Dim fechaTest As Date = DateSerial(Now.Year, 4, 30)
        'Si el día uno del mes es anterior a hoy, puede ser que sea el mes en curso
        If DateSerial(Now.Year, 4, 1) > Now Then
            fechaTest = fechaTest.AddYears(-1)
        End If
        Assert.IsTrue(comVM.fechaHasta = fechaTest)
    End Sub

    <TestMethod()> Public Sub DebeHaberUnVendedorSeleccionado()
        Assert.IsNotNull(comVM.vendedorActual)
    End Sub

End Class

<TestClass()> Public Class CuandoCambiamosElVendedor

    Dim DbContext As New NestoEntities
    Dim comVM As New ComisionesViewModel


    <TestMethod()> Public Sub DebeHaberUnMesSeleccionado()
        Assert.IsNotNull(comVM.mesActual)
    End Sub

    <TestMethod()> Public Sub LaVentaDeCursosDeDavidEnMarzoDebeSerCorrecta()
        'Arrange
        comVM.mesActual = "Marzo"
        Dim vendedor As New Vendedores
        vendedor = (From c In DbContext.Vendedores Where c.Empresa = "1" And c.Estado >= 0 And c.Número = "DV").FirstOrDefault

        'Act
        comVM.vendedorActual = vendedor

        'Assert
        Assert.IsTrue(CDbl(comVM.comisionesActual.VentaCur) = 1925)

    End Sub

End Class