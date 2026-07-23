Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports System.IO
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports Microsoft.Win32
Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models.PlanesVentajas
Imports Prism.Mvvm
Imports Unity

Public Class PlanesVentajasViewModel
    Inherits BindableBase

    Public Property Titulo As String
    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicio As IPlanesVentajasService
    Private ruta As String
    Private _isDirty As Boolean
    Private _suspenderDirty As Boolean

    Public Sub New(configuracion As IConfiguracion, container As IUnityContainer)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        Me.configuracion = configuracion
        Titulo = "Planes de Ventajas"
        Dim servicioAutenticacion = container.Resolve(Of IServicioAutenticacion)()
        _servicio = New PlanesVentajasService(configuracion, servicioAutenticacion)
    End Sub

    ' Constructor para tests: permite inyectar el servicio mockeado.
    Public Sub New(configuracion As IConfiguracion, servicio As IPlanesVentajasService)
        Me.configuracion = configuracion
        _servicio = servicio
        Titulo = "Planes de Ventajas"
    End Sub

    Public Async Function CargarDatos() As Task
        Try
            Dim empresaDefecto As String = Await configuracion.leerParametro("1", "EmpresaPorDefecto")
            ruta = Await configuracion.leerParametro(empresaDefecto, "RutaPlanVentajas")
            vendedor = Await configuracion.leerParametro(empresaDefecto, "Vendedor")
            listaEmpresas = New ObservableCollection(Of EmpresaResumenModel)(Await _servicio.LeerEmpresas())
            listaEstados = New ObservableCollection(Of EstadoPlanVentajasModel)(Await _servicio.LeerEstados())
            empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
            barrasGrafico = New ObservableCollection(Of datosGrafico)
            gaugeGrafico = New ObservableCollection(Of datosGrafico)
            Await RecargarListaPlanesAsync()
            planActual = listaPlanes.LastOrDefault
        Catch ex As Exception
            ' Nesto#426: sin este catch la excepción quedaba sin observar (UnobservedTaskException
            ' en el finalizer) y el usuario veía la pantalla vacía sin ninguna explicación.
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Function

    Private _empresaActual As String
    Public Property empresaActual As String
        Get
            Return _empresaActual
        End Get
        Set(value As String)
            _empresaActual = value
            RaisePropertyChanged(NameOf(empresaActual))
        End Set
    End Property

    Private _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
            RaisePropertyChanged(NameOf(vendedor))
        End Set
    End Property

    Private _listaPlanes As ObservableCollection(Of PlanVentajasModel)
    Public Property listaPlanes As ObservableCollection(Of PlanVentajasModel)
        Get
            Return _listaPlanes
        End Get
        Set(value As ObservableCollection(Of PlanVentajasModel))
            _listaPlanes = value
            RaisePropertyChanged(NameOf(listaPlanes))
        End Set
    End Property

    Private _planActual As PlanVentajasModel
    Public Property planActual As PlanVentajasModel
        Get
            Return _planActual
        End Get
        Set(value As PlanVentajasModel)
            If _planActual IsNot Nothing Then
                RemoveHandler _planActual.PropertyChanged, AddressOf OnPlanActualChanged
            End If
            _planActual = value
            mensajeError = ""
            isDirty = False
            If _planActual IsNot Nothing Then
                AddHandler _planActual.PropertyChanged, AddressOf OnPlanActualChanged
                CargarDatosPlanActualAsync()
            Else
                listaClientes = Nothing
                DesengancharListaClientesEditar()
                listaClientesEditar = Nothing
                lineasVenta = Nothing
            End If
            RaisePropertyChanged(NameOf(planActual))
            RaisePropertyChanged(NameOf(diasPlan))
            RaisePropertyChanged(NameOf(diasTranscurridos))
            RaisePropertyChanged(NameOf(importeDiaObjetivo))
            RaisePropertyChanged(NameOf(porcentajeRealizado))
            CommandManager.InvalidateRequerySuggested()
        End Set
    End Property

    Private Sub OnPlanActualChanged(sender As Object, e As PropertyChangedEventArgs)
        If _suspenderDirty Then Return
        isDirty = True
    End Sub

    Private Async Sub CargarDatosPlanActualAsync()
        Try
            Await CargarDatosPlanActualCoreAsync()
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Sub

    Public Async Function CargarDatosPlanActualCoreAsync() As Task
        Dim plan = _planActual
        If plan Is Nothing Then Return

        Dim clientes = Await _servicio.ObtenerClientes(plan.Numero, empresaActual)
        listaClientes = New ObservableCollection(Of ClientePlanVentajasModel)(clientes)

        Dim editables = If(plan.Clientes, New List(Of String)).
                        Select(Function(c) New ClienteAsignacionModel With {.Cliente = c})
        listaClientesEditar = New ObservableCollection(Of ClienteAsignacionModel)(editables)

        Dim lineas = Await _servicio.ObtenerLineasVenta(plan.Numero, empresaActual)
        lineasVenta = New ObservableCollection(Of LineaVentaPlanModel)(lineas)

        If barrasGrafico IsNot Nothing Then
            barrasGrafico.Clear()
            barrasGrafico.Add(New datosGrafico With {.clave = "Presupuestado", .valor = CInt(plan.Importe)})
            barrasGrafico.Add(New datosGrafico With {.clave = "Realizado", .valor = CInt(importeVentas)})
            barrasGrafico.Add(New datosGrafico With {.clave = "Proyección", .valor = CInt(importeProyeccion)})
        End If
        If gaugeGrafico IsNot Nothing Then
            gaugeGrafico.Clear()
            gaugeGrafico.Add(New datosGrafico With {.clave = "Realizado", .valor = CInt(porcentajeRealizado)})
        End If

        ' Tras la recarga inicial nada debe marcar como sucio.
        isDirty = False
        RaisePropertyChanged(NameOf(barrasGrafico))
    End Function

    Private _listaClientes As ObservableCollection(Of ClientePlanVentajasModel)
    Public Property listaClientes As ObservableCollection(Of ClientePlanVentajasModel)
        Get
            Return _listaClientes
        End Get
        Set(value As ObservableCollection(Of ClientePlanVentajasModel))
            _listaClientes = value
            RaisePropertyChanged(NameOf(listaClientes))
        End Set
    End Property

    Private _listaClientesEditar As ObservableCollection(Of ClienteAsignacionModel)
    Public Property listaClientesEditar As ObservableCollection(Of ClienteAsignacionModel)
        Get
            Return _listaClientesEditar
        End Get
        Set(value As ObservableCollection(Of ClienteAsignacionModel))
            DesengancharListaClientesEditar()
            SetProperty(_listaClientesEditar, value)
            EngancharListaClientesEditar()
        End Set
    End Property

    Private Sub EngancharListaClientesEditar()
        If _listaClientesEditar Is Nothing Then Return
        AddHandler _listaClientesEditar.CollectionChanged, AddressOf OnListaClientesEditarCollectionChanged
        For Each c In _listaClientesEditar
            AddHandler c.PropertyChanged, AddressOf OnClienteAsignacionChanged
        Next
    End Sub

    Private Sub DesengancharListaClientesEditar()
        If _listaClientesEditar Is Nothing Then Return
        RemoveHandler _listaClientesEditar.CollectionChanged, AddressOf OnListaClientesEditarCollectionChanged
        For Each c In _listaClientesEditar
            RemoveHandler c.PropertyChanged, AddressOf OnClienteAsignacionChanged
        Next
    End Sub

    Private Sub OnListaClientesEditarCollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        If _suspenderDirty Then Return
        If e.NewItems IsNot Nothing Then
            For Each c As ClienteAsignacionModel In e.NewItems
                AddHandler c.PropertyChanged, AddressOf OnClienteAsignacionChanged
            Next
        End If
        If e.OldItems IsNot Nothing Then
            For Each c As ClienteAsignacionModel In e.OldItems
                RemoveHandler c.PropertyChanged, AddressOf OnClienteAsignacionChanged
            Next
        End If
        isDirty = True
    End Sub

    Private Sub OnClienteAsignacionChanged(sender As Object, e As PropertyChangedEventArgs)
        If _suspenderDirty Then Return
        isDirty = True
    End Sub

    Private _lineasVenta As ObservableCollection(Of LineaVentaPlanModel)
    Public Property lineasVenta As ObservableCollection(Of LineaVentaPlanModel)
        Get
            Return _lineasVenta
        End Get
        Set(value As ObservableCollection(Of LineaVentaPlanModel))
            _lineasVenta = value
            If lineasVenta IsNot Nothing Then
                importeVentas = lineasVenta.Sum(Function(l) l.BaseImponible)
            Else
                importeVentas = 0
            End If
            RaisePropertyChanged(NameOf(lineasVenta))
        End Set
    End Property

    Private _importeVentas As Decimal
    Public Property importeVentas As Decimal
        Get
            Return _importeVentas
        End Get
        Set(value As Decimal)
            _importeVentas = value
            RaisePropertyChanged(NameOf(importeVentas))
            RaisePropertyChanged(NameOf(importeProyeccion))
        End Set
    End Property

    Public ReadOnly Property diasPlan As Integer
        Get
            If planActual Is Nothing Then Return 0
            Return CInt((planActual.FechaFin - planActual.FechaInicio).TotalDays) + 1
        End Get
    End Property

    Public ReadOnly Property diasTranscurridos As Integer
        Get
            If planActual Is Nothing Then Return 0
            Return CInt((Today - planActual.FechaInicio).TotalDays) + 1
        End Get
    End Property

    Public ReadOnly Property importeDiaObjetivo As Decimal
        Get
            If planActual Is Nothing OrElse diasPlan = 0 Then Return 0
            Return planActual.Importe / diasPlan
        End Get
    End Property

    Public ReadOnly Property importeProyeccion As Decimal
        Get
            If planActual Is Nothing OrElse diasTranscurridos = 0 OrElse importeVentas <= 0 Then Return 0
            Return importeVentas / diasTranscurridos * diasPlan
        End Get
    End Property

    Private _itemSeleccionado As Object
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

    Public ReadOnly Property porcentajeRealizado As Decimal
        Get
            If planActual Is Nothing OrElse planActual.Importe = 0 Then Return 0
            If importeVentas > planActual.Importe Then Return 100
            Return importeVentas / planActual.Importe * 100
        End Get
    End Property

    Private _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            _mensajeError = value
            RaisePropertyChanged(NameOf(mensajeError))
        End Set
    End Property

    Private _selectedPath As String
    Public Property selectedPath As String
        Get
            Return _selectedPath
        End Get
        Set(value As String)
            _selectedPath = value
            RaisePropertyChanged(NameOf(selectedPath))
        End Set
    End Property

    Private _PestañaSeleccionada As TabItem
    Public Property PestañaSeleccionada As TabItem
        Get
            Return _PestañaSeleccionada
        End Get
        Set(value As TabItem)
            _PestañaSeleccionada = value
            RaisePropertyChanged(NameOf(PestañaSeleccionada))
        End Set
    End Property

    Private _listaEmpresas As ObservableCollection(Of EmpresaResumenModel)
    Public Property listaEmpresas As ObservableCollection(Of EmpresaResumenModel)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of EmpresaResumenModel))
            _listaEmpresas = value
            RaisePropertyChanged(NameOf(listaEmpresas))
        End Set
    End Property

    Private _listaEstados As ObservableCollection(Of EstadoPlanVentajasModel)
    Public Property listaEstados As ObservableCollection(Of EstadoPlanVentajasModel)
        Get
            Return _listaEstados
        End Get
        Set(value As ObservableCollection(Of EstadoPlanVentajasModel))
            _listaEstados = value
            RaisePropertyChanged(NameOf(listaEstados))
        End Set
    End Property

    Private _filtro As String
    Public Property filtro As String
        Get
            Return _filtro
        End Get
        Set(value As String)
            _filtro = value
            RaisePropertyChanged(NameOf(filtro))
            RefrescarListaPlanesAsync()
        End Set
    End Property

    Private _verPlanesNulos As Boolean = False
    Public Property verPlanesNulos As Boolean
        Get
            Return _verPlanesNulos
        End Get
        Set(value As Boolean)
            _verPlanesNulos = value
            RaisePropertyChanged(NameOf(verPlanesNulos))
            RefrescarListaPlanesAsync()
        End Set
    End Property

    Private Property isDirty As Boolean
        Get
            Return _isDirty
        End Get
        Set(value As Boolean)
            _isDirty = value
            CommandManager.InvalidateRequerySuggested()
        End Set
    End Property

#Region "Comandos"

    Private _cmdVerPlan As ICommand
    Public ReadOnly Property cmdVerPlan As ICommand
        Get
            If _cmdVerPlan Is Nothing Then _cmdVerPlan = New RelayCommand(AddressOf VerPlan, AddressOf CanVerPlan)
            Return _cmdVerPlan
        End Get
    End Property
    Private Function CanVerPlan(ByVal param As Object) As Boolean
        If planActual Is Nothing Then Return False
        Return File.Exists(rutaPlan())
    End Function
    Private Sub VerPlan(ByVal param As Object)
        Try
            Using process As New System.Diagnostics.Process
                process.StartInfo.FileName = rutaPlan()
                process.StartInfo.UseShellExecute = True
                process.Start()
                process.WaitForExit()
            End Using
            mensajeError = ""
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Sub

    Private _cmdAsignarPlan As ICommand
    Public ReadOnly Property cmdAsignarPlan As ICommand
        Get
            If _cmdAsignarPlan Is Nothing Then _cmdAsignarPlan = New RelayCommand(AddressOf AsignarPlan, AddressOf CanAsignarPlan)
            Return _cmdAsignarPlan
        End Get
    End Property
    Private Function CanAsignarPlan(ByVal param As Object) As Boolean
        Return planActual IsNot Nothing
    End Function
    Private Sub AsignarPlan(ByVal param As Object)
        Dim elegirFichero As New OpenFileDialog With {
            .Filter = "pdf files (*.pdf)|*.pdf|All files (*.*)|*.*",
            .FilterIndex = 1,
            .RestoreDirectory = True
        }
        If Not elegirFichero.ShowDialog() Then Return
        Try
            selectedPath = elegirFichero.FileName
            FileIO.FileSystem.CopyFile(
                selectedPath,
                rutaPlan(),
                Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,
                Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing)
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        Finally
            If selectedPath Is Nothing Then selectedPath = String.Empty
        End Try
    End Sub

    Private _cmdAñadir As ICommand
    Public ReadOnly Property cmdAñadir As ICommand
        Get
            If _cmdAñadir Is Nothing Then _cmdAñadir = New RelayCommand(AddressOf Añadir, AddressOf CanAñadir)
            Return _cmdAñadir
        End Get
    End Property
    Private Function CanAñadir(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub Añadir(ByVal param As Object)
        Dim nuevo As New PlanVentajasModel With {
            .Empresa = empresaActual,
            .FechaInicio = Today,
            .FechaFin = Today
        }
        If listaPlanes Is Nothing Then
            listaPlanes = New ObservableCollection(Of PlanVentajasModel)
        End If
        listaPlanes.Add(nuevo)
        planActual = nuevo
        isDirty = True
    End Sub

    Private _cmdGuardar As ICommand
    Public ReadOnly Property cmdGuardar As ICommand
        Get
            If _cmdGuardar Is Nothing Then _cmdGuardar = New RelayCommand(AddressOf Guardar, AddressOf CanGuardar)
            Return _cmdGuardar
        End Get
    End Property
    Private Function CanGuardar(ByVal param As Object) As Boolean
        Return planActual IsNot Nothing AndAlso isDirty
    End Function
    Private Sub Guardar(ByVal param As Object)
        GuardarFireAndForget()
    End Sub
    Private Async Sub GuardarFireAndForget()
        Try
            Await GuardarAsync()
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Sub
    Public Async Function GuardarAsync() As Task
        If planActual Is Nothing Then Return
        ' Sincronizar la lista editable con el modelo antes de enviar
        If listaClientesEditar IsNot Nothing Then
            planActual.Clientes = listaClientesEditar _
                .Where(Function(c) c IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(c.Cliente)) _
                .Select(Function(c) c.Cliente.Trim()) _
                .Distinct() _
                .ToList()
        End If
        Dim guardado As PlanVentajasModel
        If planActual.Numero = 0 Then
            guardado = Await _servicio.CrearPlan(planActual)
        Else
            guardado = Await _servicio.ActualizarPlan(planActual)
        End If
        mensajeError = ""
        Await RecargarListaPlanesAsync()
        If guardado IsNot Nothing AndAlso listaPlanes IsNot Nothing Then
            planActual = listaPlanes.FirstOrDefault(Function(p) p.Numero = guardado.Numero)
        End If
    End Function

#End Region

#Region "Funciones de ayuda"
    Private Function rutaPlan() As String
        Return ruta + planActual.Numero.ToString().Trim() + ".pdf"
    End Function

    Private Async Sub RefrescarListaPlanesAsync()
        Try
            Await RecargarListaPlanesAsync()
        Catch ex As Exception
            mensajeError = If(ex.InnerException IsNot Nothing, ex.InnerException.Message, ex.Message)
        End Try
    End Sub

    Public Async Function RecargarListaPlanesAsync() As Task
        Dim filtroLocal As String = If(String.IsNullOrWhiteSpace(_filtro), Nothing, _filtro.Trim())
        Dim resultados = Await _servicio.ListarPlanes(_vendedor, filtroLocal, _verPlanesNulos)
        listaPlanes = New ObservableCollection(Of PlanVentajasModel)(resultados)
    End Function
#End Region

End Class

Public Class datosGrafico
    Public Property clave As String
    Public Property valor As Integer
End Class

Public Class ClienteAsignacionModel
    Implements INotifyPropertyChanged

    Private _cliente As String
    Public Property Cliente As String
        Get
            Return _cliente
        End Get
        Set(value As String)
            If _cliente <> value Then
                _cliente = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Cliente)))
            End If
        End Set
    End Property

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
End Class
