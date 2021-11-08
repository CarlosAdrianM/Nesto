Imports System.Windows.Input
Imports System.ComponentModel
Imports System.Collections.ObjectModel
Imports System.Windows
Imports Microsoft.Win32
Imports Microsoft.Office.Interop
Imports System.Windows.Controls
Imports Nesto.Contratos
Imports Nesto.Models.Nesto.Models
Imports Prism.Mvvm
Imports Prism.Commands
Imports Microsoft.Graph
Imports Prism.Services.Dialogs
Imports Azure.Identity

Public Class RemesasViewModel
    Inherits BindableBase

    Const numRemesas = 100

    Private Shared DbContext As NestoEntities
    Dim empresaDefecto As String = "1" 'mainModel.leerParametro("1", "EmpresaPorDefecto")
    Dim blnPuedeVerTodasLasRemesas As Boolean = True

    Private ReadOnly dialogService As IDialogService

    Public Structure tipoRemesa
        Public Sub New(
       ByVal _id As String,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As String
        Property descripcion As String
    End Structure

    Public Sub New(interactiveBrowserCredential As InteractiveBrowserCredential, configuracion As IConfiguracion, dialogService As IDialogService)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        Titulo = "Remesas"
        Me.InteractiveBrowserCredential = interactiveBrowserCredential
        Me.configuracion = configuracion
        Me.dialogService = dialogService
        DbContext = New NestoEntities
        listaEmpresas = New ObservableCollection(Of Empresas)(From c In DbContext.Empresas)
        empresaActual = String.Format("{0,-3}", empresaDefecto) 'para que rellene con espacios en blanco por la derecha
        listaRemesas = New ObservableCollection(Of Remesas)(From c In DbContext.Remesas Where c.Empresa = empresaActual Order By c.Número Descending Take numRemesas)
        remesaActual = listaRemesas.FirstOrDefault
        listaImpagados = New ObservableCollection(Of impagado)(From c In DbContext.ExtractoCliente Where c.Empresa = empresaActual And c.TipoApunte = "4" Group By c.Asiento, c.Fecha Into Count() Order By Asiento Descending Take numRemesas Select New impagado With {.asiento = Asiento, .fecha = Fecha, .cuenta = Count})
        impagadoActual = listaImpagados.FirstOrDefault
        listaTiposRemesa = New ObservableCollection(Of tipoRemesa)
        tipoRemesaActual = New tipoRemesa("B2B", "Profesionales (B2B)")
        listaTiposRemesa.Add(tipoRemesaActual)
        listaTiposRemesa.Add(New tipoRemesa("CORE", "Particulares (CORE)"))
        tipoRemesaActual = listaTiposRemesa.LastOrDefault ' Por defecto es CORE -> FirstOrDefault para B2B
        fechaCobro = Today

        CrearTareasPlannerCommand = New DelegateCommand(AddressOf OnCrearTareasPlanner, AddressOf CanCrearTareasPlanner)
    End Sub


#Region "Propiedades"

    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            SetProperty(_titulo, value)
        End Set
    End Property

    'Private Property app As IPublicClientApplication
    Private Property InteractiveBrowserCredential As InteractiveBrowserCredential
    Private Property configuracion As IConfiguracion

    Private Property _listaEmpresas As ObservableCollection(Of Empresas)
    Public Property listaEmpresas As ObservableCollection(Of Empresas)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of Empresas))
            _listaEmpresas = value
            RaisePropertyChanged("listaEmpresas")
        End Set
    End Property

    Private Property _empresaActual As String
    Public Property empresaActual As String
        Get
            Return _empresaActual
        End Get
        Set(value As String)
            _empresaActual = value
            listaRemesas = New ObservableCollection(Of Remesas)(From c In DbContext.Remesas Where c.Empresa = empresaActual Order By c.Número Descending Take numRemesas)
            blnPuedeVerTodasLasRemesas = True
            listaImpagados = New ObservableCollection(Of impagado)(From c In DbContext.ExtractoCliente Where c.Empresa = empresaActual And c.TipoApunte = "4" Group By c.Asiento, c.Fecha Into Count() Order By Asiento Descending Take numRemesas Select New impagado With {.asiento = Asiento, .fecha = Fecha, .cuenta = Count})
            RaisePropertyChanged("empresaActual")
        End Set
    End Property

    Private Property _remesaActual As Remesas
    Public Property remesaActual As Remesas
        Get
            Return _remesaActual
        End Get
        Set(value As Remesas)
            _remesaActual = value
            If IsNothing(remesaActual) Then
                listaMovimientos = Nothing
            Else
                listaMovimientos = New ObservableCollection(Of ExtractoCliente)(From e In DbContext.ExtractoCliente Where e.Empresa = empresaActual And e.Remesa = remesaActual.Número And e.TipoApunte = 3)
            End If
            RaisePropertyChanged("remesaActual")
        End Set
    End Property

    Private Property _contenidoFichero As Xml.Linq.XDocument
    Public Property contenidoFichero As Xml.Linq.XDocument
        Get
            Return _contenidoFichero
        End Get
        Set(value As Xml.Linq.XDocument)
            _contenidoFichero = value
            RaisePropertyChanged("contenidoFichero")
        End Set
    End Property

    Private Property _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            _mensajeError = value
            RaisePropertyChanged("mensajeError")
        End Set
    End Property

    Private Property _listaRemesas As ObservableCollection(Of Remesas)
    Public Property listaRemesas As ObservableCollection(Of Remesas)
        Get
            Return _listaRemesas
        End Get
        Set(value As ObservableCollection(Of Remesas))
            _listaRemesas = value
            RaisePropertyChanged("listaRemesas")
        End Set
    End Property

    Private Property _listaMovimientos As ObservableCollection(Of ExtractoCliente)
    Public Property listaMovimientos As ObservableCollection(Of ExtractoCliente)
        Get
            Return _listaMovimientos
        End Get
        Set(value As ObservableCollection(Of ExtractoCliente))
            _listaMovimientos = value
            RaisePropertyChanged("listaMovimientos")
        End Set
    End Property

    Private Property _pestañaSeleccionada As TabItem
    Public Property PestañaSeleccionada As TabItem
        Get
            Return _pestañaSeleccionada
        End Get
        Set(value As TabItem)
            SetProperty(_pestañaSeleccionada, value)
            If _pestañaSeleccionada.Header = "Impagados" AndAlso String.IsNullOrEmpty(usuarioTareas) Then
                usuarioTareas = configuracion.LeerParametroSync(empresaActual, "UsuarioAvisoImpagadoDefecto")
                RaisePropertyChanged(NameOf(usuarioTareas))
            End If
        End Set
    End Property

    Private Property _listaImpagados As ObservableCollection(Of impagado)
    Public Property listaImpagados As ObservableCollection(Of impagado)
        Get
            Return _listaImpagados
        End Get
        Set(value As ObservableCollection(Of impagado))
            _listaImpagados = value
            RaisePropertyChanged("listaImpagados")
        End Set
    End Property

    Private Property _listaImpagadosDetalle As ObservableCollection(Of ExtractoCliente)
    Public Property listaImpagadosDetalle As ObservableCollection(Of ExtractoCliente)
        Get
            Return _listaImpagadosDetalle
        End Get
        Set(value As ObservableCollection(Of ExtractoCliente))
            _listaImpagadosDetalle = value
            RaisePropertyChanged("listaImpagadosDetalle")
        End Set
    End Property

    Private Property _impagadoActual As impagado
    Public Property impagadoActual As impagado
        Get
            Return _impagadoActual
        End Get
        Set(value As impagado)
            _impagadoActual = value
            If IsNothing(impagadoActual) Then
                listaImpagadosDetalle = Nothing
            Else
                listaImpagadosDetalle = New ObservableCollection(Of ExtractoCliente)(From e In DbContext.ExtractoCliente Where e.Empresa = empresaActual And e.Asiento = impagadoActual.asiento And e.TipoApunte = 4)
            End If
            RaisePropertyChanged("impagadoActual")
        End Set
    End Property

    Private Property _usuarioTareas As String
    Public Property usuarioTareas As String
        Get
            Return _usuarioTareas
        End Get
        Set(value As String)
            SetProperty(_usuarioTareas, value)
        End Set
    End Property

    Private Property _listaTiposRemesa As ObservableCollection(Of tipoRemesa)
    Public Property listaTiposRemesa As ObservableCollection(Of tipoRemesa)
        Get
            Return _listaTiposRemesa
        End Get
        Set(value As ObservableCollection(Of tipoRemesa))
            _listaTiposRemesa = value
            'RaisePropertyChanged("listaTiposRemesa")
        End Set
    End Property

    Private Property _tipoRemesaActual As tipoRemesa
    Public Property tipoRemesaActual As tipoRemesa
        Get
            Return _tipoRemesaActual
        End Get
        Set(value As tipoRemesa)
            _tipoRemesaActual = value
            RaisePropertyChanged("tipoRemesaActual")
        End Set
    End Property

    Private Property _fechaCobro As Date
    Public Property fechaCobro As Date
        Get
            Return _fechaCobro
        End Get
        Set(value As Date)
            _fechaCobro = value
            RaisePropertyChanged("fechaCobro")
        End Set
    End Property

    Private _estaOcupado As Boolean = False
    Public Property estaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(ByVal value As Boolean)
            _estaOcupado = value
            RaisePropertyChanged("estaOcupado")
        End Set
    End Property

#End Region

#Region "Comandos"

    Private _cmdCrearFicheroRemesa As ICommand
    Public ReadOnly Property cmdCrearFicheroRemesa() As ICommand
        Get
            If _cmdCrearFicheroRemesa Is Nothing Then
                _cmdCrearFicheroRemesa = New RelayCommand(AddressOf CrearFicheroRemesa, AddressOf CanCrearFicheroRemesa)
            End If
            Return _cmdCrearFicheroRemesa
        End Get
    End Property
    Private Function CanCrearFicheroRemesa(ByVal param As Object) As Boolean
        Return Not remesaActual Is Nothing
    End Function
    Private Async Sub CrearFicheroRemesa(ByVal param As Object)
        Dim strContenido As String
        Dim listaContenido As List(Of String)
        Dim codigo As String
        codigo = tipoRemesaActual.id
        Dim nombreFichero As String = Await configuracion.leerParametro(empresaActual, Parametros.Claves.PathNorma19) + CStr(remesaActual.Número) + ".xml"
        'Dim nombreFichero As String = "c:\banco\prueba.xml"
        Try
            mensajeError = "Generando fichero..."
            DbContext.Database.CommandTimeout = 6000

            estaOcupado = True
            Await Task.Run(Sub()
                               listaContenido = crearFicheroRemesa(remesaActual.Número, codigo, fechaCobro)
                               DbContext.Database.CommandTimeout = 180
                               strContenido = ""
                               For Each linea In listaContenido
                                   strContenido = strContenido + linea
                               Next
                               contenidoFichero = XDocument.Parse(strContenido)
                               contenidoFichero.Save(nombreFichero)
                               mensajeError = "Fichero " + nombreFichero + " creado correctamente"
                           End Sub)

        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        Finally
            estaOcupado = False
        End Try
    End Sub

    Private _cmdLeerFicheroImpagado As ICommand
    Public ReadOnly Property cmdLeerFicheroImpagado() As ICommand
        Get
            If _cmdLeerFicheroImpagado Is Nothing Then
                _cmdLeerFicheroImpagado = New RelayCommand(AddressOf LeerFicheroImpagado, AddressOf CanLeerFicheroImpagado)
            End If
            Return _cmdLeerFicheroImpagado
        End Get
    End Property
    Private Function CanLeerFicheroImpagado(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Async Sub LeerFicheroImpagado(ByVal param As Object)
        Dim elegirFichero = New OpenFileDialog
        elegirFichero.Filter = "xml files (*.xml)|*.xml|All files (*.*)|*.*"
        elegirFichero.FilterIndex = 1
        elegirFichero.RestoreDirectory = True
        elegirFichero.InitialDirectory = Await configuracion.leerParametro(empresaActual, "PathDefectoImpagados")

        If elegirFichero.ShowDialog() Then
            Try
                contenidoFichero = XDocument.Load(elegirFichero.FileName)
                estaOcupado = True
                mensajeError = "Contabilizando..."
                Await Task.Run(Sub()
                                   contabilizarImpagados(contenidoFichero.ToString)
                                   mensajeError = "Impagados contabilizados correctamente"
                               End Sub)
            Catch ex As Exception
                If IsNothing(ex.InnerException) Then
                    mensajeError = ex.Message
                Else
                    mensajeError = ex.InnerException.Message
                End If
            Finally
                estaOcupado = False
            End Try
        End If

        listaImpagados = New ObservableCollection(Of impagado)(From c In DbContext.ExtractoCliente Where c.Empresa = empresaActual And c.TipoApunte = "4" Group By c.Asiento, c.Fecha Into Count() Order By Asiento Descending Take numRemesas Select New impagado With {.asiento = Asiento, .fecha = Fecha, .cuenta = Count})
        impagadoActual = listaImpagados.First

    End Sub

    Private _cmdVerTodasLasRemesas As ICommand
    Public ReadOnly Property cmdVerTodasLasRemesas() As ICommand
        Get
            If _cmdVerTodasLasRemesas Is Nothing Then
                _cmdVerTodasLasRemesas = New RelayCommand(AddressOf VerTodasLasRemesas, AddressOf CanVerTodasLasRemesas)
            End If
            Return _cmdVerTodasLasRemesas
        End Get
    End Property
    Private Function CanVerTodasLasRemesas(ByVal param As Object) As Boolean
        Return blnPuedeVerTodasLasRemesas
    End Function
    Private Sub VerTodasLasRemesas(ByVal param As Object)
        listaRemesas = New ObservableCollection(Of Remesas)(From c In DbContext.Remesas Where c.Empresa = empresaActual Order By c.Número Descending)
        blnPuedeVerTodasLasRemesas = False
    End Sub

    Private _cmdCrearTareasOutlook As ICommand
    Public ReadOnly Property cmdCrearTareasOutlook() As ICommand
        Get
            If _cmdCrearTareasOutlook Is Nothing Then
                _cmdCrearTareasOutlook = New RelayCommand(AddressOf CrearTareasOutlook, AddressOf CanCrearTareasOutlook)
            End If
            Return _cmdCrearTareasOutlook
        End Get
    End Property
    Private Function CanCrearTareasOutlook(ByVal param As Object) As Boolean
        Return Not impagadoActual Is Nothing
    End Function
    Private Sub CrearTareasOutlook(ByVal param As Object)
        Dim objOL As Outlook.Application
        objOL = New Outlook.Application
        Dim newTask As Outlook.TaskItem
        Dim ruta As New Rutas
        Dim impagados = From e In DbContext.ExtractoCliente Join c In DbContext.Clientes On e.Empresa Equals c.Empresa And e.Número Equals c.Nº_Cliente And e.Contacto Equals c.Contacto Where e.Empresa = empresaActual And e.Asiento = impagadoActual.asiento And Not e.Concepto.StartsWith("Gastos Impagado ")

        Try
            For Each impagado In impagados
                newTask = objOL.CreateItem(Outlook.OlItemType.olTaskItem)
                If Not IsNothing(newTask) Then
                    ruta = (From r In DbContext.Rutas Where r.Empresa = impagado.c.Empresa And r.Número = impagado.c.Ruta).FirstOrDefault
                    newTask.Subject = ruta.Descripción.Trim + " - Llamar al cliente " + impagado.e.Número.Trim + "/" + impagado.e.Contacto.Trim +
                        ". Vendedor: " + impagado.c.Vendedor.Trim + ". " + impagado.c.Nombre.Trim + " en " + impagado.c.Dirección.Trim
                    newTask.Body = "Ha llegado un impagado de este cliente, con fecha " + impagado.e.Fecha.ToShortDateString + " e importe de " + FormatCurrency(impagado.e.Importe) + " (más gastos)." + vbCrLf +
                        "Motivo: " + impagado.e.Concepto + vbCrLf +
                        "Ruta: " + impagado.c.Ruta + vbCrLf +
                        "Empresa: " + impagado.c.Empresas.Nombre.Trim
                    newTask.Assign()
                    'If impagado.c.Ruta.Trim = "00" Or impagado.c.Ruta.Trim = "02" Or impagado.c.Ruta.Trim = "03" Then
                    '    usuarioTareas = "laura@nuevavision.es"
                    'Else
                    '    usuarioTareas = "aidarubio@nuevavision.es"
                    'End If
                    newTask.Recipients.Add(usuarioTareas)
                    newTask.Recipients.ResolveAll()
                    newTask.Send()
                End If
            Next
            mensajeError = "Tareas del asiento " + CStr(impagadoActual.asiento) + " creadas correctamente"
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try
    End Sub

    Public Property CrearTareasPlannerCommand As DelegateCommand
    Private Function CanCrearTareasPlanner() As Boolean
        Return True
    End Function
    Private Async Sub OnCrearTareasPlanner()
        Dim p As New DialogParameters
        Dim continuar As Boolean = False
        p.Add("message", "¿Desea crear las tareas en Planner?")
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              If r.Result = ButtonResult.OK Then
                                                                  continuar = True
                                                              End If
                                                          End Sub)

        If Not continuar Then
            Return
        End If
        Dim planId = Constantes.Planner.GestionCobro.PLAN_ID
        Dim bucketId = Constantes.Planner.GestionCobro.BUCKET_PENDIENTES
        Dim scopes = {"User.Read.All", "Group.ReadWrite.All"}
        Dim graphClient As New GraphServiceClient(InteractiveBrowserCredential, scopes) 'you can pass the TokenCredential directly To the GraphServiceClient

        Dim users = Await graphClient.Users.Request().GetAsync()

        Dim usuarios As String() = usuarioTareas.Split(New Char() {";"c})
        Dim usuariosAsignar As New List(Of String)
        For Each usuario In usuarios
            Dim usuarioAsignar = users.FirstOrDefault(Function(c) c.Mail = usuario.Trim).Id
            If Not String.IsNullOrEmpty(usuarioAsignar) Then
                usuariosAsignar.Add(usuarioAsignar)
            End If
        Next

        Dim tareasBucket = Await graphClient.Planner.Buckets(bucketId).Tasks.Request().GetAsync()

        Dim impagados = From e In DbContext.ExtractoCliente Join c In DbContext.Clientes On e.Empresa Equals c.Empresa And e.Número Equals c.Nº_Cliente And e.Contacto Equals c.Contacto Where e.Empresa = empresaActual And e.Asiento = impagadoActual.asiento And Not e.Concepto.StartsWith("Gastos Impagado ")

        Dim plannerTask As PlannerTask

        Try
            For Each impagado In impagados
                Dim asignadas = New PlannerAssignments
                For Each usuario In usuariosAsignar
                    asignadas.AddAssignee(usuario)
                Next
                asignadas.ODataType = Nothing

                Dim tituloTarea = String.Format("Impagados cliente {0}", impagado.e.Número.Trim)

                Dim detallesAntiguos As PlannerTaskDetails = Nothing

                If tareasBucket.Any(Function(t) t.Title = tituloTarea) Then
                    plannerTask = tareasBucket.First(Function(t) t.Title = tituloTarea)
                    detallesAntiguos = Await graphClient.Planner.Tasks(plannerTask.Id).Details.Request().GetAsync()
                    If plannerTask.PercentComplete = 100 Then
                        Dim nuevaTask As New PlannerTask With {
                            .PercentComplete = 50 ' en curso
                        }
                        Await graphClient.Planner.Tasks(plannerTask.Id).Request().Header("Prefer", "return=representation").Header("If-Match", plannerTask.GetEtag).UpdateAsync(nuevaTask)
                    End If
                Else
                    plannerTask = New PlannerTask With
                    {
                        .PlanId = planId,
                        .BucketId = bucketId,
                        .Title = tituloTarea,
                        .Assignments = asignadas
                    }
                End If

                Dim elementoCheckList As String = String.Format("Fecha {0}, importe {1} (más gastos). {2}.",
                        impagado.e.Fecha.ToShortDateString, FormatCurrency(impagado.e.Importe), impagado.e.Concepto.Trim)
                Dim detalles As PlannerTaskDetails = New PlannerTaskDetails()
                detalles.Checklist = New PlannerChecklistItems()
                detalles.Checklist.AddChecklistItem(Left(elementoCheckList, 100))
                detalles.Checklist.ODataType = Nothing

                If IsNothing(detallesAntiguos) Then
                    Dim descripcion As String = String.Format("Llamar al cliente {0}/{1}. Vendedor: {2}. {3} en  {4}. Ruta: {5}. Empresa: {6}.",
                            impagado.e.Número.Trim(), impagado.e.Contacto.Trim, impagado.c.Vendedor.Trim, impagado.c.Nombre.Trim, impagado.c.Dirección.Trim, impagado.c.Ruta, impagado.c.Empresas.Nombre.Trim)
                    detalles.Description = descripcion
                    detalles.PreviewType = PlannerPreviewType.Checklist
                    plannerTask.Details = detalles
                End If

                If String.IsNullOrEmpty(plannerTask.Id) Then
                    plannerTask = Await graphClient.Planner.Tasks.Request().AddAsync(plannerTask)
                    tareasBucket.Add(plannerTask)
                Else
                    Await graphClient.Planner.Tasks(plannerTask.Id).Details.Request().Header("If-Match", detallesAntiguos.GetEtag).UpdateAsync(detalles)
                End If
            Next
            mensajeError = "Tareas del asiento " + CStr(impagadoActual.asiento) + " creadas correctamente"
        Catch ex As Exception
            If IsNothing(ex.InnerException) Then
                mensajeError = ex.Message
            Else
                mensajeError = ex.InnerException.Message
            End If
        End Try

    End Sub

#End Region

#Region "Funciones Auxiliares"
    Private Sub contabilizarImpagados(contenidoFichero As String)
        DbContext.Database.CommandTimeout = 6000
        DbContext.prdContabilizarImpagadosSepa(contenidoFichero)
    End Sub

    Private Function crearFicheroRemesa(remesa As Integer, codigo As String, fechaCobro As Date) As List(Of String)
        Return DbContext.CrearFicheroRemesa(remesa, codigo, fechaCobro).ToList
    End Function
#End Region

End Class

Public Class impagado
    Public Sub New()

    End Sub

    Private Property _asiento As Integer
    Public Property asiento As Integer
        Get
            Return _asiento
        End Get
        Set(value As Integer)
            _asiento = value
        End Set
    End Property

    Private Property _fecha As Date
    Public Property fecha As Date
        Get
            Return _fecha
        End Get
        Set(value As Date)
            _fecha = value
        End Set
    End Property

    Private Property _cuenta As Integer
    Public Property cuenta As Integer
        Get
            Return _cuenta
        End Get
        Set(value As Integer)
            _cuenta = value
        End Set
    End Property

End Class

