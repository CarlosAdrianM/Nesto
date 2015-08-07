Imports CrystalDecisions.CrystalReports.Engine
Imports CrystalDecisions.Shared
Imports System.Data.SqlClient
Imports System.Data
'Imports Nesto.ViewModels.MainViewModel
Imports Nesto.ViewModels
Imports Microsoft.Practices.Unity
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Modularity
Imports Prism.RibbonRegionAdapter
Imports Nesto.Contratos

<[Module](ModuleName:="MenuBarView")>
Public Class MenuBarView
    Implements IModule, IMenuBar
    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager


    Public Sub New(container As IUnityContainer, regionManager As IRegionManager)


        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.container = container
        Me.regionManager = regionManager
        Me.DataContext = New MainViewModel(container, regionManager)


    End Sub

    Public Sub Initialize() Implements IModule.Initialize
        'container.RegisterType(Of Object, frmCRInforme)("frmCRInforme")


        'regionManager.RegisterViewWithRegion("MainMenu", Function() Me.container.Resolve(Of MenuBarView)())
        'regionManager.AddToRegion("MainMenu", Me)
        'regionManager.RequestNavigate("MainMenu", New Uri("MenuBarView", UriKind.Relative))

        'Dim mainMenuRegion As IRegion = regionManager.Regions("MainMenu")
        'AddHandler regionManager.Regions("MainMenu").Views.CollectionChanged, AddressOf OnColeccionCambiada
        'mainMenuRegion.Add(Me, "MenuBar")



        Dim view = Me
        If Not IsNothing(view) Then

            'Dim regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            Dim regionAdapter = Me.container.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = Me.container.Resolve(Of IMainWindow)()
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "NewMainMenu")
            region.Add(view, "MenuBar")
        End If

    End Sub


    Private Sub Button1_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnInforme.Click
        'Dim w2 As New frmInforme
        'w2.Owner = Me
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of frmCRInforme)()


        'Dim fchFechaInicial As New Date
        'Dim fchFechaFinal As New Date


        Select Case cmbOpciones.Text
            Case "Actual"
                Me.DataContext.fechaInformeInicial = DateSerial(Year(Now()), Int((Month(Now()) - 1) / 3) * 3 + 1, 1)
                Me.DataContext.fechaInformeFinal = DateSerial(Year(Now()), Int((Month(Now()) - 1) / 3) * 3 + 4, 0)
            Case "Anterior"
                Me.DataContext.fechaInformeInicial = DateSerial(Year(Now()), Int((Month(Now()) - 4) / 3) * 3 + 1, 1)
                Me.DataContext.fechaInformeFinal = DateSerial(Year(Now()), Int((Month(Now()) - 4) / 3) * 3 + 4, 0)
            Case Else
                MsgBox("Parte del programa no implementada aún")
        End Select



        Dim mv As New NVDataSetMV
        Dim ds As New DataSet

        ds = mv.CargarDatos(Me.DataContext.fechaInformeInicial, Me.DataContext.fechaInformeFinal)

        Dim rptPremio As New Premio_Vendedores_UL

        rptPremio.SetDataSource(ds.Tables("Informe"))



        rptPremio.SetParameterValue("FechaDesde", Me.DataContext.fechaInformeInicial)
        rptPremio.SetParameterValue("FechaHasta", Me.DataContext.fechaInformeFinal)


        vista.crvInforme.ViewerCore.ReportSource = rptPremio
        vista.crvInforme.ViewerCore.AllowedExportFormats = CrystalDecisions.Shared.ViewerExportFormats.AllFormats
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
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


        Dim vm = New MainViewModel(container, regionManager)
        Dim strVendedor As String = vm.Vendedor

        'Dim Ventana As New frmInforme
        'Ventana.Owner = Me
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of frmCRInforme)()


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




        vista.crvInforme.ViewerCore.ReportSource = rptPremio


        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)


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
                'MsgBox("Parte del programa no implementada aún")
                GenerarInformeVentasGrupo(Me.DataContext.fechaInformeInicial, Me.DataContext.fechaInformeFinal, True)
        End Select
    End Sub
    Private Sub GenerarInformeVentasGrupo(FechaDesde As Date, FechaHasta As Date, SóloFacturas As Boolean)
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of frmCRInforme)()

        'Dim Ventana As New frmInforme
        'Ventana.Owner = Me

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




        vista.crvInforme.ViewerCore.ReportSource = rptPremio
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
        'Try
        'Ventana.Show()
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

        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of frmCRInforme)()

        'Dim Ventana As New frmInforme
        'Ventana.Owner = Me

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





        vista.crvInforme.ViewerCore.ReportSource = rptPremio

        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)

    End Sub
    Private Sub btnRapport_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnRapport.Click
        Select Case cmbOpciones.Text
            Case "Actual"
                GenerarInformeRapports(Today, Today)
            Case "Anterior"
                GenerarInformeRapports(Today.AddDays(-1), Today.AddDays(-1))
            Case Else
                GenerarInformeRapports(Me.DataContext.fechaInformeInicial, Me.DataContext.fechaInformeFinal)
        End Select
    End Sub
    Private Sub GenerarInformeRapports(FechaDesde As Date, FechaHasta As Date)
        Dim Ventana As New frmInforme
        'Ventana.Owner = Me

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
        'Me.regionManager.RegisterViewWithRegion("MainRegion", Function() Me.container.Resolve(Of Clientes)())
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Clientes)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnControlPedidos_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnControlPedidos.Click

        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of frmCRInforme)()


        'Dim w2 As New frmInforme
        'w2.Owner = Me

        Dim mv As New ControlPedidosMV
        Dim ds As New DataSet

        ds = mv.CargarDatos

        Dim rptControl As New InformeControlPedidos

        rptControl.SetDataSource(ds.Tables("ControlPedidos"))


        vista.crvInforme.ViewerCore.ReportSource = rptControl
        vista.crvInforme.ViewerCore.AllowedExportFormats = CrystalDecisions.Shared.ViewerExportFormats.AllFormats
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnInventario_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnInventario.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of frmCRInforme)()

        Dim rptInforme As New ImpresoUbicaciones_Inventario

        vista.crvInforme.ViewerCore.ReportSource = rptInforme
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnUbicaciones_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnUbicaciones.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of frmCRInforme)()
        'region.Add(vista, nombreVista(region, vista.ToString))
        'region.Activate(vista)

        'Dim w2 As New frmInforme
        'w2.Owner = Me

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




        vista.crvInforme.ViewerCore.ReportSource = rptInforme
        vista.crvInforme.ViewerCore.AllowedExportFormats = CrystalDecisions.Shared.ViewerExportFormats.AllFormats
        'w2.crvInforme.ViewerCore.ReportSource = rptInforme
        'w2.crvInforme.ViewerCore.AllowedExportFormats = CrystalDecisions.Shared.ViewerExportFormats.AllFormats
        'w2.Show()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnClientesAlquileres_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnClientesAlquileres.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Alquileres)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnClientesRemesas_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnClientesRemesas.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Remesas)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnClientesAgencias_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnClientesAgencias.Click
        'Me.regionManager.RegisterViewWithRegion("MainRegion", Function() Me.container.Resolve(Of Agencias)())
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Agencias)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)

    End Sub
    Private Sub btnRatioDeuda_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnRatioDeuda.Click
        'Me.regionManager.RegisterViewWithRegion("MainRegion", Function() Me.container.Resolve(Of Deuda)())
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Deuda)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)

    End Sub
    Private Sub btnPrestashop_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnPrestashop.Click
        Dim frmPrestashop As New Prestashop
        'frmPrestashop.Owner = Me
        frmPrestashop.Show()
    End Sub
    Private Sub btnVendedoresComisiones_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVendedoresComisiones.Click
        'Me.regionManager.RegisterViewWithRegion("MainRegion", Function() Me.container.Resolve(Of Comisiones)())
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Comisiones)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnVendedoresClientes_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVendedoresClientes.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of ClienteComercial)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnVendedoresPlanVentajas_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVendedoresPlanVentajas.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of PlanesVentajas)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)

        'Me.regionManager.RegisterViewWithRegion("MainRegion", Function() Me.container.Resolve(Of PlanesVentajas)())
    End Sub

    Private Function nombreVista(region As Region, nombre As String) As String
        Dim contador As Integer = 2
        Dim repetir As Boolean = True
        Dim nombreAmpliado As String = nombre
        While repetir
            repetir = False
            For Each view In region.Views
                If Not IsNothing(region.GetView(nombreAmpliado)) Then
                    nombreAmpliado = nombre + contador.ToString
                    contador = contador + 1
                    repetir = True
                    Exit For
                End If
            Next
        End While
        Return nombreAmpliado
    End Function

    Private Sub OnColeccionCambiada(sender As Object, e As EventArgs)

    End Sub

End Class
