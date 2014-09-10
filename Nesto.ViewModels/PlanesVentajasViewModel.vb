Imports Nesto.Models
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Input
Imports Microsoft.Win32
Imports System.Windows.Controls
Imports Microsoft.Office.Interop

Public Class PlanesVentajasViewModel
    Inherits ViewModelBase

    Private Shared DbContext As NestoEntities
    Dim mainModel As New Nesto.Models.MainModel
    Private ruta As String = mainModel.leerParametro(empresaActual, "RutaPlanVentajas")

    Public Sub New()
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        DbContext = New NestoEntities

        Dim empresaDefecto As String = mainModel.leerParametro("1", "EmpresaPorDefecto")
        vendedor = mainModel.leerParametro(empresaDefecto, "Vendedor")
        listaEmpresas = New ObservableCollection(Of Empresas)(From c In DbContext.Empresas)
        listaEstados = New ObservableCollection(Of EstadosPlanVentajas)(From c In DbContext.EstadosPlanVentajas)
        empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
        barrasGrafico = New ObservableCollection(Of datosGrafico)
        gaugeGrafico = New ObservableCollection(Of datosGrafico)
        listaPlanes = New ObservableCollection(Of PlanesVentajas)(From p In DbContext.PlanesVentajas)
        planActual = listaPlanes.LastOrDefault
    End Sub

    Private Property _empresaActual As String
    Public Property empresaActual As String
        Get
            Return _empresaActual
        End Get
        Set(value As String)
            _empresaActual = value
            OnPropertyChanged("empresaActual")
        End Set
    End Property

    Private Property _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
            OnPropertyChanged("vendedor")
        End Set
    End Property

    Private Property _listaPlanes As ObservableCollection(Of PlanesVentajas)
    Public Property listaPlanes As ObservableCollection(Of PlanesVentajas)
        Get
            Return _listaPlanes
        End Get
        Set(value As ObservableCollection(Of PlanesVentajas))
            _listaPlanes = value
            OnPropertyChanged("listaPlanes")
        End Set
    End Property

    Private Property _planActual As PlanesVentajas
    Public Property planActual As PlanesVentajas
        Get
            Return _planActual
        End Get
        Set(value As PlanesVentajas)
            _planActual = value
            mensajeError = ""
            listaClientes = New ObservableCollection(Of Clientes)(From p In DbContext.PlanVentajasCliente Join c In DbContext.Clientes On p.Cliente Equals c.Nº_Cliente Where c.Empresa = empresaActual Where p.NumeroContrato = planActual.Numero Select c)
            lineasVenta = New ObservableCollection(Of LinPedidoVta)(From l In DbContext.LinPedidoVta Join c In (From p In DbContext.PlanVentajasCliente Join c In DbContext.Clientes On p.Cliente Equals c.Nº_Cliente Where c.Empresa = empresaActual Where p.NumeroContrato = planActual.Numero Select c) On l.Nº_Cliente Equals c.Nº_Cliente And l.Contacto Equals c.Contacto Where l.Familia = planActual.Familia And l.Fecha_Factura >= planActual.FechaInicio And l.Fecha_Factura <= planActual.FechaFin Select l)
            barrasGrafico.Clear()
            barrasGrafico.Add(New datosGrafico With {.clave = "Presupuestado", .valor = planActual.Importe})
            barrasGrafico.Add(New datosGrafico With {.clave = "Realizado", .valor = importeVentas})
            barrasGrafico.Add(New datosGrafico With {.clave = "Proyección", .valor = importeProyeccion})
            gaugeGrafico.Clear()
            gaugeGrafico.Add(New datosGrafico With {.clave = "Realizado", .valor = porcentajeRealizado})
            OnPropertyChanged("planActual")
            OnPropertyChanged("diasPlan")
            OnPropertyChanged("diasTranscurridos")
            OnPropertyChanged("importeDiaObjetivo")
            OnPropertyChanged("barrasGrafico")
            OnPropertyChanged("porcentajeRealizado")
        End Set
    End Property

    Private Property _listaClientes As ObservableCollection(Of Clientes)
    Public Property listaClientes As ObservableCollection(Of Clientes)
        Get
            Return _listaClientes
        End Get
        Set(value As ObservableCollection(Of Clientes))
            _listaClientes = value
            OnPropertyChanged("listaClientes")
        End Set
    End Property

    Private Property _lineasVenta As ObservableCollection(Of LinPedidoVta)
    Public Property lineasVenta As ObservableCollection(Of LinPedidoVta)
        Get
            Return _lineasVenta
        End Get
        Set(value As ObservableCollection(Of LinPedidoVta))
            _lineasVenta = value
            importeVentas = Aggregate l In lineasVenta Into Sum(l.Base_Imponible)
            OnPropertyChanged("lineasVenta")
        End Set
    End Property

    Private Property _importeVentas As Double
    Public Property importeVentas As Double
        Get
            Return _importeVentas
        End Get
        Set(value As Double)
            _importeVentas = value
            OnPropertyChanged("importeVentas")
            OnPropertyChanged("importeProyeccion")
        End Set
    End Property

    Public ReadOnly Property diasPlan As Integer
        Get
            If Not IsNothing(planActual) Then
                Return (planActual.FechaFin - planActual.FechaInicio).TotalDays + 1
            Else
                Return 0
            End If
        End Get
    End Property

    Public ReadOnly Property diasTranscurridos As Integer
        Get
            If Not IsNothing(planActual) Then
                Return (Today - planActual.FechaInicio).TotalDays + 1
            Else
                Return 0
            End If
        End Get
    End Property

    Public ReadOnly Property importeDiaObjetivo As Double
        Get
            If Not IsNothing(planActual) Then
                Return planActual.Importe / diasPlan
            Else
                Return 0
            End If
        End Get
    End Property

    Public ReadOnly Property importeProyeccion As Double
        Get
            If Not IsNothing(planActual) Then
                Return importeVentas / diasTranscurridos * diasPlan
            Else
                Return 0
            End If
        End Get
    End Property

    Private Property _itemSeleccionado As Object
    Public Property itemSeleccionado As Object
        Get
            Return _itemSeleccionado
        End Get
        Set(value As Object)
            _itemSeleccionado = value
        End Set
    End Property

    Private _barrasGrafico As ObservableCollection(Of datosGrafico)
    Public Property barrasGrafico As ObservableCollection(Of datosGrafico)
        Get
            Return _barrasGrafico
        End Get
        Set(value As ObservableCollection(Of datosGrafico))
            _barrasGrafico = value
        End Set
    End Property

    Private _gaugeGrafico As ObservableCollection(Of datosGrafico)
    Public Property gaugeGrafico As ObservableCollection(Of datosGrafico)
        Get
            Return _gaugeGrafico
        End Get
        Set(value As ObservableCollection(Of datosGrafico))
            _gaugeGrafico = value
        End Set
    End Property

    Public ReadOnly Property porcentajeRealizado
        Get
            If IsNothing(planActual) Then
                Return 0
            ElseIf planActual.Importe = 0 Then
                Return 0
            ElseIf importeVentas > planActual.Importe Then
                Return 100
            Else
                Return importeVentas / planActual.Importe * 100
            End If
        End Get
    End Property

    Private Property _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            _mensajeError = value
            OnPropertyChanged("mensajeError")
        End Set
    End Property

    Private _selectedPath As String
    Public Property selectedPath() As String
        Get
            Return _selectedPath
        End Get
        Set(value As String)
            _selectedPath = value
            OnPropertyChanged("selectedPath")
        End Set
    End Property

    Private _PestañaSeleccionada As TabItem
    Public Property PestañaSeleccionada As TabItem
        Get
            Return _PestañaSeleccionada
        End Get
        Set(value As TabItem)
            _PestañaSeleccionada = value
            OnPropertyChanged("PestañaSeleccionada")
        End Set
    End Property

    Private Property _listaEmpresas As ObservableCollection(Of Empresas)
    Public Property listaEmpresas As ObservableCollection(Of Empresas)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of Empresas))
            _listaEmpresas = value
            OnPropertyChanged("listaEmpresas")
        End Set
    End Property

    Private Property _listaEstados As ObservableCollection(Of EstadosPlanVentajas)
    Public Property listaEstados As ObservableCollection(Of EstadosPlanVentajas)
        Get
            Return _listaEstados
        End Get
        Set(value As ObservableCollection(Of EstadosPlanVentajas))
            _listaEstados = value
            OnPropertyChanged("listaEstados")
        End Set
    End Property


#Region "Comandos"

    Private _cmdVerPlan As ICommand
    Public ReadOnly Property cmdVerPlan() As ICommand
        Get
            If _cmdVerPlan Is Nothing Then
                _cmdVerPlan = New RelayCommand(AddressOf VerPlan, AddressOf CanVerPlan)
            End If
            Return _cmdVerPlan
        End Get
    End Property
    Private Function CanVerPlan(ByVal param As Object) As Boolean
        'Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified Or System.Data.EntityState.Deleted)
        If IsNothing(planActual) Then
            Return False
        ElseIf Not My.Computer.FileSystem.FileExists(rutaPlan) Then
            Return False
        Else
            Return True
        End If
    End Function
    Private Sub VerPlan(ByVal param As Object)
        Try
            Dim fileName As String = rutaPlan()
            Dim process As System.Diagnostics.Process = New System.Diagnostics.Process
            process.StartInfo.FileName = fileName
            process.Start()
            process.WaitForExit()
            mensajeError = ""
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try
    End Sub

    Private _cmdAsignarPlan As ICommand
    Public ReadOnly Property cmdAsignarPlan() As ICommand
        Get
            If _cmdAsignarPlan Is Nothing Then
                _cmdAsignarPlan = New RelayCommand(AddressOf AsignarPlan, AddressOf CanAsignarPlan)
            End If
            Return _cmdAsignarPlan
        End Get
    End Property
    Private Function CanAsignarPlan(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub AsignarPlan(ByVal param As Object)
        Dim elegirFichero = New OpenFileDialog
        elegirFichero.Filter = "pdf files (*.pdf)|*.pdf|All files (*.*)|*.*"
        elegirFichero.FilterIndex = 1
        elegirFichero.RestoreDirectory = True

        If elegirFichero.ShowDialog() Then
            Try
                selectedPath = elegirFichero.FileName
                My.Computer.FileSystem.CopyFile(
                selectedPath,
                rutaPlan,
                Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,
                Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing)
            Catch ex As Exception
                If IsNothing(ex.InnerException) Then
                    mensajeError = ex.Message
                Else
                    mensajeError = ex.InnerException.Message
                End If
            Finally
                If selectedPath Is Nothing Then
                    selectedPath = String.Empty
                End If
            End Try
        End If
    End Sub

    Private _cmdAñadir As ICommand
    Public ReadOnly Property cmdAñadir() As ICommand
        Get
            If _cmdAñadir Is Nothing Then
                _cmdAñadir = New RelayCommand(AddressOf Añadir, AddressOf CanAñadir)
            End If
            Return _cmdAñadir
        End Get
    End Property
    Private Function CanAñadir(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub Añadir(ByVal param As Object)
        Dim nuevo As New PlanesVentajas
        nuevo.Empresa = empresaActual
        DbContext.AddToPlanesVentajas(nuevo)
        listaPlanes.Add(nuevo)
        planActual = listaPlanes.LastOrDefault
    End Sub

    Private _cmdGuardar As ICommand
    Public ReadOnly Property cmdGuardar() As ICommand
        Get
            If _cmdGuardar Is Nothing Then
                _cmdGuardar = New RelayCommand(AddressOf Guardar, AddressOf CanGuardar)
            End If
            Return _cmdGuardar
        End Get
    End Property
    Private Function CanGuardar(ByVal param As Object) As Boolean

        Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified Or System.Data.EntityState.Deleted)

        Return changes.Any



    End Function
    Private Sub Guardar(ByVal param As Object)
        Try
            DbContext.SaveChanges()
            listaPlanes = New ObservableCollection(Of PlanesVentajas)(From p In DbContext.PlanesVentajas)
            mensajeError = ""
        Catch ex As Exception
            mensajeError = ex.InnerException.Message
        End Try
    End Sub

    Private _cmdCrearCitasOutlook As ICommand
    Public ReadOnly Property cmdCrearCitasOutlook() As ICommand
        Get
            If _cmdCrearCitasOutlook Is Nothing Then
                _cmdCrearCitasOutlook = New RelayCommand(AddressOf CrearCitasOutlook, AddressOf CanCrearCitasOutlook)
            End If
            Return _cmdCrearCitasOutlook
        End Get
    End Property
    Private Function CanCrearCitasOutlook(ByVal param As Object) As Boolean
        Return Not planActual Is Nothing
    End Function
    Private Sub CrearCitasOutlook(ByVal param As Object)
        Dim objOL As Outlook.Application
        objOL = New Outlook.Application
        Dim newTask As Outlook.AppointmentItem

        Try
            newTask = objOL.CreateItem(Outlook.OlItemType.olAppointmentItem)
            If Not IsNothing(newTask) Then
                newTask.Subject = "Finaliza el Plan de Ventajas " + planActual.Numero.ToString.Trim
                newTask.Body = "El día " + planActual.FechaFin.ToShortDateString + " finaliza el Plan de Ventajas nº " + planActual.Numero.ToString.Trim + "." + vbCrLf +
                    "Importe Compromiso: " + FormatCurrency(planActual.Importe) + vbCrLf
                For Each cliente In planActual.PlanVentajasCliente
                    newTask.Body = newTask.Body + "Cliente " + cliente.Cliente.ToString.Trim + vbCrLf
                Next
                newTask.Start = planActual.FechaFin
                newTask.AllDayEvent = True
                newTask.ReminderSet = True
                newTask.ReminderMinutesBeforeStart = 15 * 24 * 60 '15 días antes
                newTask.Save()
            End If

            mensajeError = "Cita del plan " + CStr(planActual.Numero) + " creada correctamente"
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try
    End Sub


#End Region

    Private Function rutaPlan() As String
        Return ruta + planActual.Numero.ToString.Trim + ".pdf"
    End Function


End Class

Public Class datosGrafico
    Private Property _clave As String
    Public Property clave As String
        Get
            Return _clave
        End Get
        Set(value As String)
            _clave = value
        End Set
    End Property

    Private Property _valor As Integer
    Public Property valor As Integer
        Get
            Return _valor
        End Get
        Set(value As Integer)
            _valor = value
        End Set
    End Property
End Class


