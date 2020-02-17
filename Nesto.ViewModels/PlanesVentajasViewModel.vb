Imports Nesto.Models
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Input
Imports Microsoft.Win32
Imports System.Windows.Controls
Imports Microsoft.Office.Interop
Imports System.Threading.Tasks
Imports Nesto.Contratos
Imports Nesto.Models.Nesto.Models

Public Class PlanesVentajasViewModel
    Inherits ViewModelBase

    Private Shared DbContext As NestoEntities
    Dim configuracion As IConfiguracion
    Private ruta As String
    Private Const ESTADO_PLAN_CANCELADO = 6

    Public Sub New(configuracion As IConfiguracion)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        Me.configuracion = configuracion
        DbContext = New NestoEntities
        Titulo = "Planes de Ventajas"
    End Sub

    Async Function CargarDatos() As Task
        Dim empresaDefecto As String = Await configuracion.leerParametro("1", "EmpresaPorDefecto")
        ruta = Await configuracion.leerParametro(empresaDefecto, "RutaPlanVentajas")
        vendedor = Await configuracion.leerParametro(empresaDefecto, "Vendedor")
        listaEmpresas = New ObservableCollection(Of Empresas)(From c In DbContext.Empresas)
        listaEstados = New ObservableCollection(Of EstadosPlanVentajas)(From c In DbContext.EstadosPlanVentajas)
        empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
        barrasGrafico = New ObservableCollection(Of datosGrafico)
        gaugeGrafico = New ObservableCollection(Of datosGrafico)
        ActualizarListaPlanes()
        planActual = listaPlanes.LastOrDefault
    End Function

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
            If Not IsNothing(planActual) Then
                listaClientes = New ObservableCollection(Of Clientes)(From p In DbContext.PlanVentajasCliente Join c In DbContext.Clientes On p.Cliente Equals c.Nº_Cliente Where c.Empresa = empresaActual Where p.NumeroContrato = planActual.Numero Select c)
                lineasVenta = New ObservableCollection(Of LinPedidoVta)(From l In DbContext.LinPedidoVta Join c In (From p In DbContext.PlanVentajasCliente Join c In DbContext.Clientes On p.Cliente Equals c.Nº_Cliente Where c.Empresa = empresaActual Where p.NumeroContrato = planActual.Numero Select c) On l.Nº_Cliente Equals c.Nº_Cliente And l.Contacto Equals c.Contacto Where l.Familia = planActual.Familia And l.Fecha_Factura >= planActual.FechaInicio And l.Fecha_Factura <= planActual.FechaFin Select l Order By l.Fecha_Factura Descending)
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
            Else
                listaClientes = Nothing
                lineasVenta = Nothing
            End If
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
            If Not IsNothing(lineasVenta) Then
                importeVentas = Aggregate l In lineasVenta Into Sum(l.Base_Imponible)
            Else
                importeVentas = 0
            End If
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
                If importeVentas > 0 Then
                    Return importeVentas / diasTranscurridos * diasPlan
                Else
                    Return 0
                End If
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

    Private _filtro As String
    Public Property filtro As String
        Get
            Return _filtro
        End Get
        Set(value As String)
            _filtro = value
            If filtro.Trim <> "" Then
                ActualizarListaPlanes(filtro)
            Else
                ActualizarListaPlanes()
            End If

            OnPropertyChanged("filtro")
        End Set
    End Property

    Private _verPlanesNulos As Boolean = False
    Public Property verPlanesNulos() As Boolean
        Get
            Return _verPlanesNulos
        End Get
        Set(ByVal value As Boolean)
            _verPlanesNulos = value
            OnPropertyChanged("verPlanesNulos")
            ActualizarListaPlanes()
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
        nuevo.FechaInicio = Today
        nuevo.FechaFin = Today
        DbContext.PlanesVentajas.Add(nuevo)
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
        Return DbContext.ChangeTracker.HasChanges()
    End Function
    Private Sub Guardar(ByVal param As Object)
        Try
            DbContext.SaveChanges()
            ActualizarListaPlanes()
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

#Region "Funciones de ayuda"
    Private Function rutaPlan() As String
        Return ruta + planActual.Numero.ToString.Trim + ".pdf"
    End Function

    Private Sub ActualizarListaPlanes()
        ActualizarListaPlanes(Nothing)
    End Sub
    Private Sub ActualizarListaPlanes(filtro As String)
        Dim filtroEstado As Integer
        If verPlanesNulos Then
            filtroEstado = Integer.MaxValue
        Else
            filtroEstado = ESTADO_PLAN_CANCELADO
        End If
        Dim clientesString As List(Of String)
        Dim clientes, clientesPlan, clientesTotales As List(Of Clientes)
        Dim clienteEncontrado As Clientes
        Dim listaInicial, listaMedia As ObservableCollection(Of PlanesVentajas)
        If vendedor = "" And IsNothing(filtro) Then
            listaPlanes = New ObservableCollection(Of PlanesVentajas)(From p In DbContext.PlanesVentajas Where p.Estado <> filtroEstado Order By p.FechaFin)
        Else
            listaInicial = New ObservableCollection(Of PlanesVentajas)(From p In DbContext.PlanesVentajas Where p.Estado <> filtroEstado Order By p.FechaFin)
            listaMedia = New ObservableCollection(Of PlanesVentajas)
            clientesString = New List(Of String)(From p In DbContext.PlanVentajasCliente Select p.Cliente)
            If IsNothing(filtro) Then
                clientesTotales = New List(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = "1" And c.Estado >= 0)
            Else
                clientesTotales = New List(Of Clientes)(From c In DbContext.Clientes Where c.Empresa = "1" And c.Estado >= 0 And c.Nº_Cliente = filtro)
            End If

            clientes = New List(Of Clientes)(From s In clientesString Join c In clientesTotales On c.Nº_Cliente Equals s Select c)
            For Each plan In listaInicial
                clientesString = New List(Of String)(From p In DbContext.PlanVentajasCliente Where p.NumeroContrato = plan.Numero Select p.Cliente)
                clientesPlan = New List(Of Clientes)(From s In clientesString Join c In clientes On c.Nº_Cliente Equals s Select c)
                If Not IsNothing(filtro) Then
                    clienteEncontrado = (From c In clientesPlan).FirstOrDefault
                Else
                    clienteEncontrado = (From c In clientesPlan Where c.Vendedor.Trim = vendedor.Trim).FirstOrDefault
                End If

                If Not IsNothing(clienteEncontrado) Then
                    listaMedia.Add(plan)
                End If
                'listaMedia.Add(plan)
            Next
            listaPlanes = listaMedia
            planActual = listaPlanes.FirstOrDefault
        End If
    End Sub
#End Region


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


