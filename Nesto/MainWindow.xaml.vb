Imports CrystalDecisions.CrystalReports.Engine
Imports CrystalDecisions.Shared
Imports System.Data.SqlClient
Imports System.Data
Imports Nesto.ViewModels.MainViewModel
Imports Nesto.ViewModels



Class MainWindow

    Private Property _fechaInformeInicial As Date
    Public Property fechaInformeInicial As Date
        Get
            Return _fechaInformeInicial
        End Get
        Set(value As Date)
            _fechaInformeInicial = value
        End Set
    End Property

    Private Property _fechaInformeFinal As Date
    Public Property fechaInformeFinal As Date
        Get
            Return _fechaInformeFinal
        End Get
        Set(value As Date)
            _fechaInformeFinal = value
        End Set
    End Property




    Private Sub Button1_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnInforme.Click
        Dim w2 As New frmInforme
        w2.Owner = Me

        'Dim fchFechaInicial As New Date
        'Dim fchFechaFinal As New Date


        Select Case cmbOpciones.Text
            Case "Actual"
                fechaInformeInicial = DateSerial(Year(Now()), Int((Month(Now()) - 1) / 3) * 3 + 1, 1)
                fechaInformeFinal = DateSerial(Year(Now()), Int((Month(Now()) - 1) / 3) * 3 + 4, 0)
            Case "Anterior"
                fechaInformeInicial = DateSerial(Year(Now()), Int((Month(Now()) - 4) / 3) * 3 + 1, 1)
                fechaInformeFinal = DateSerial(Year(Now()), Int((Month(Now()) - 4) / 3) * 3 + 4, 0)
            Case Else
                MsgBox("Parte del programa no implementada aún")
        End Select



        Dim mv As New NVDataSetMV
        Dim ds As New DataSet

        ds = mv.CargarDatos(fechaInformeInicial, fechaInformeFinal)

        Dim rptPremio As New Premio_Vendedores_UL

        rptPremio.SetDataSource(ds.Tables("Informe"))



        rptPremio.SetParameterValue("FechaDesde", fechaInformeInicial)
        rptPremio.SetParameterValue("FechaHasta", fechaInformeFinal)


        w2.crvInforme.ViewerCore.ReportSource = rptPremio
        w2.crvInforme.ViewerCore.AllowedExportFormats = CrystalDecisions.Shared.ViewerExportFormats.AllFormats
        w2.Show()
    End Sub

    Private Sub btnComisionesTelefono_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnComisionesTelefono.Click
        Select Case cmbOpciones.Text
            Case "Actual"
                GenerarInformeComisiones9(DateSerial(Year(Now()), Month(Now()) + 0, 1), DateSerial(Year(Now()), Month(Now()) + 1, 0), False, False)
            Case "Anterior"
                GenerarInformeComisiones9(DateSerial(Year(Now()), Month(Now()) - 1, 1), DateSerial(Year(Now()), Month(Now()), 0), False, True)
            Case Else
                MsgBox("Parte del programa no implementada aún")
        End Select

    End Sub

    Private Sub GenerarInformeComisiones9(FechaDesde As Date, FechaHasta As Date, Resumen As Boolean, SoloFacturas As Boolean)


        Dim vm = New MainViewModel
        Dim strVendedor As String = vm.Vendedor

        Dim Ventana As New frmInforme
        Ventana.Owner = Me

        Dim rptPremio
        If Resumen Then
            rptPremio = New ReporteComisiones2011Estado9Resumen
        Else
            rptPremio = New ReporteComisiones2011Estado9Detalle
        End If


        Dim crParameterDiscreteValue As ParameterDiscreteValue

        Dim crParameterFieldDefinitions As ParameterFieldDefinitions

        Dim crParameterFieldLocation As ParameterFieldDefinition

        Dim crParameterValues As ParameterValues




        ' Get the report parameters collection. 

        '

        crParameterFieldDefinitions = rptPremio.DataDefinition.ParameterFields



        crParameterFieldLocation = crParameterFieldDefinitions.Item("@Empresa")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = "1"

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)



        crParameterFieldLocation = crParameterFieldDefinitions.Item("@FechaDesde")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = FechaDesde

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)



        crParameterFieldLocation = crParameterFieldDefinitions.Item("@FechaHasta")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = FechaHasta

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)



        crParameterFieldLocation = crParameterFieldDefinitions.Item("@Resumen")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = Resumen

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)



        crParameterFieldLocation = crParameterFieldDefinitions.Item("@Vendedor")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = strVendedor

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)



        crParameterFieldLocation = crParameterFieldDefinitions.Item("@SoloFacturas")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = SoloFacturas

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)




        Ventana.crvInforme.ViewerCore.ReportSource = rptPremio


        Ventana.Show()

    End Sub

    Private Sub btnResumen_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnResumen.Click
        Select Case cmbOpciones.Text
            Case "Actual"
                GenerarInformeComisiones9(DateSerial(Year(Now()), Month(Now()) + 0, 1), DateSerial(Year(Now()), Month(Now()) + 1, 0), True, False)
            Case "Anterior"
                GenerarInformeComisiones9(DateSerial(Year(Now()), Month(Now()) - 1, 1), DateSerial(Year(Now()), Month(Now()), 0), True, True)
            Case Else
                MsgBox("Parte del programa no implementada aún")
        End Select
    End Sub

    Private Sub btnVentasEmpresas_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVentasEmpresas.Click
        Select Case cmbOpciones.Text
            Case "Actual"
                GenerarInformeVentasGrupo(DateSerial(Year(Now()), Month(Now()) + 0, 1), DateSerial(Year(Now()), Month(Now()) + 1, 0), False)
            Case "Anterior"
                GenerarInformeVentasGrupo(DateSerial(Year(Now()), Month(Now()) - 1, 1), DateSerial(Year(Now()), Month(Now()), 0), True)
            Case Else
                MsgBox("Parte del programa no implementada aún")
        End Select
    End Sub
    Private Sub GenerarInformeVentasGrupo(FechaDesde As Date, FechaHasta As Date, SóloFacturas As Boolean)

        Dim Ventana As New frmInforme
        Ventana.Owner = Me

        Dim rptPremio As New ReporteResumenVentas


        Dim crParameterDiscreteValue As ParameterDiscreteValue

        Dim crParameterFieldDefinitions As ParameterFieldDefinitions

        Dim crParameterFieldLocation As ParameterFieldDefinition

        Dim crParameterValues As ParameterValues




        ' Get the report parameters collection. 

        '

        crParameterFieldDefinitions = rptPremio.DataDefinition.ParameterFields





        crParameterFieldLocation = crParameterFieldDefinitions.Item("@FechaDesde")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = FechaDesde

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)



        crParameterFieldLocation = crParameterFieldDefinitions.Item("@FechaHasta")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = FechaHasta

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)










        crParameterFieldLocation = crParameterFieldDefinitions.Item("@SoloFacturas")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = SóloFacturas

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)




        Ventana.crvInforme.ViewerCore.ReportSource = rptPremio

        'Try
        Ventana.Show()
        'Catch ex As Exception
        '        Throw New ArgumentException("Error Carlos")
        '       End Try
    End Sub
    Private Sub btnVentasEmpresas_Loaded(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnVentasEmpresas.Loaded
        If (System.Environment.UserName = "Alfredo") Or (System.Environment.UserName = "Enrique") Or (System.Environment.UserName = "Carlos") Or (System.Environment.UserName = "Manuel") Then
            btnVentasEmpresas.Visibility = Windows.Visibility.Visible
        End If
    End Sub
    Private Sub btnPeluquería_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnPeluquería.Click
        Select Case cmbOpciones.Text
            Case "Actual"
                GenerarInformePeluquería(DateSerial(Year(Now()), Month(Now()) + 0, 1), DateSerial(Year(Now()), Month(Now()) + 1, 0))
            Case "Anterior"
                GenerarInformePeluquería(DateSerial(Year(Now()), Month(Now()) - 1, 1), DateSerial(Year(Now()), Month(Now()), 0))
            Case Else
                MsgBox("Parte del programa no implementada aún")
        End Select
    End Sub
    Private Sub GenerarInformePeluquería(FechaDesde As Date, FechaHasta As Date)
        Dim Ventana As New frmInforme
        Ventana.Owner = Me

        Dim rptPremio As New VentaPeluqueria


        Dim crParameterDiscreteValue As ParameterDiscreteValue

        Dim crParameterFieldDefinitions As ParameterFieldDefinitions

        Dim crParameterFieldLocation As ParameterFieldDefinition

        Dim crParameterValues As ParameterValues




        ' Get the report parameters collection. 

        '

        crParameterFieldDefinitions = rptPremio.DataDefinition.ParameterFields




        crParameterFieldLocation = crParameterFieldDefinitions.Item("@FechaDesde")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = FechaDesde

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)



        crParameterFieldLocation = crParameterFieldDefinitions.Item("@FechaHasta")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = FechaHasta

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)




        'crParameterFieldLocation = crParameterFieldDefinitions.Item("@SoloFactura")

        'crParameterValues = crParameterFieldLocation.CurrentValues

        'crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        'crParameterDiscreteValue.Value = "1"

        'crParameterValues.Add(crParameterDiscreteValue)

        'crParameterFieldLocation.ApplyCurrentValues(crParameterValues)





        Ventana.crvInforme.ViewerCore.ReportSource = rptPremio


        Ventana.Show()

    End Sub
    Private Sub btnRapport_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnRapport.Click
        Select Case cmbOpciones.Text
            Case "Actual"
                GenerarInformeRapports(Today, Today)
            Case "Anterior"
                GenerarInformeRapports(Today.AddDays(-1), Today.AddDays(-1))
            Case Else
                MsgBox("Parte del programa no implementada aún")
        End Select
    End Sub
    Private Sub GenerarInformeRapports(FechaDesde As Date, FechaHasta As Date)
        Dim Ventana As New frmInforme
        Ventana.Owner = Me

        Dim rptPremio As New rapportEstado9


        Dim crParameterDiscreteValue As ParameterDiscreteValue

        Dim crParameterFieldDefinitions As ParameterFieldDefinitions

        Dim crParameterFieldLocation As ParameterFieldDefinition

        Dim crParameterValues As ParameterValues




        ' Get the report parameters collection. 

        '

        crParameterFieldDefinitions = rptPremio.DataDefinition.ParameterFields





        crParameterFieldLocation = crParameterFieldDefinitions.Item("@FechaDesde")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = FechaDesde

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)



        crParameterFieldLocation = crParameterFieldDefinitions.Item("@FechaHasta")

        crParameterValues = crParameterFieldLocation.CurrentValues

        crParameterDiscreteValue = New CrystalDecisions.Shared.ParameterDiscreteValue

        crParameterDiscreteValue.Value = FechaHasta

        crParameterValues.Add(crParameterDiscreteValue)

        crParameterFieldLocation.ApplyCurrentValues(crParameterValues)




        Ventana.crvInforme.ViewerCore.ReportSource = rptPremio


        Ventana.Show()

    End Sub
    Private Sub btnClientesFicha_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnClientesFicha.Click
        Dim frmClientes As New Clientes
        frmClientes.Owner = Me
        frmClientes.Show()
    End Sub
    Private Sub btnControlPedidos_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnControlPedidos.Click
        Dim w2 As New frmInforme
        w2.Owner = Me

        Dim mv As New ControlPedidosMV
        Dim ds As New DataSet

        ds = mv.CargarDatos

        Dim rptControl As New InformeControlPedidos

        rptControl.SetDataSource(ds.Tables("ControlPedidos"))


        w2.crvInforme.ViewerCore.ReportSource = rptControl
        w2.crvInforme.ViewerCore.AllowedExportFormats = CrystalDecisions.Shared.ViewerExportFormats.AllFormats
        w2.Show()
    End Sub
    Public Sub New()

        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().

    End Sub
    Private Sub btnInventario_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnInventario.Click
        Dim Ventana As New frmInforme
        Ventana.Owner = Me

        Dim rptInforme As New ImpresoUbicaciones_Inventario

        Ventana.crvInforme.ViewerCore.ReportSource = rptInforme
        Ventana.Show()
    End Sub
    Private Sub btnUbicaciones_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnUbicaciones.Click
        Dim w2 As New frmInforme
        w2.Owner = Me

        Dim ds As New DataSet
        Dim intNúmero As Integer

        If CInt(txtLineas.Text) <> 0 Then
            intNúmero = CInt(txtLineas.Text)
        Else
            intNúmero = 15
        End If

        Dim mv As New UbicacionesMV
        ds = mv.CargarDatos(intNúmero)

        Dim rptInforme As New UbicacionesParaInventario

        rptInforme.SetDataSource(ds.Tables("Ubicaciones"))





        w2.crvInforme.ViewerCore.ReportSource = rptInforme
        w2.crvInforme.ViewerCore.AllowedExportFormats = CrystalDecisions.Shared.ViewerExportFormats.AllFormats
        w2.Show()
    End Sub
    Private Sub btnClientesAlquileres_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnClientesAlquileres.Click
        'Este código hay que cambiarlo para que quede integrado en el MVVM
        Dim frmAlquileres As New Alquileres
        frmAlquileres.Owner = Me
        frmAlquileres.Show()
    End Sub
    Private Sub btnClientesRemesas_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnClientesRemesas.Click
        'Este código hay que cambiarlo para que quede integrado en el MVVM
        Dim frmRemesas As New Remesas
        frmRemesas.Owner = Me
        frmRemesas.Show()
    End Sub

    Private Sub btnClientesAgencias_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnClientesAgencias.Click
        'Este código hay que cambiarlo para que quede integrado en el MVVM
        Dim frmCargar As New Agencias
        frmCargar.Owner = Me
        frmCargar.Show()
    End Sub

    Private Sub btnRatioDeuda_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnRatioDeuda.Click
        Dim frmDeuda As New Deuda
        frmDeuda.Owner = Me
        frmDeuda.Show()
    End Sub

    Private Sub btnPrestashop_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnPrestashop.Click
        Dim frmPrestashop As New Prestashop
        frmPrestashop.Owner = Me
        frmPrestashop.Show()
    End Sub

    Private Sub btnVendedoresComisiones_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVendedoresComisiones.Click
        'Este código hay que cambiarlo para que quede integrado en el MVVM
        Dim frmComisiones As New Comisiones
        frmComisiones.Owner = Me
        frmComisiones.Show()
    End Sub

    Private Sub btnVendedoresClientes_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVendedoresClientes.Click
        'Este código hay que cambiarlo para que quede integrado en el MVVM
        Dim frmFichas As New ClienteComercial
        frmFichas.Owner = Me
        frmFichas.Show()
    End Sub

    Private Sub btnVendedoresPlanVentajas_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVendedoresPlanVentajas.Click
        'Este código hay que cambiarlo para que quede integrado en el MVVM
        Dim frmAbrir As New PlanesVentajas
        frmAbrir.Owner = Me
        frmAbrir.Show()
    End Sub

End Class

