Imports CommunityToolkit.Mvvm.Input
Imports Prism.Regions
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Prism.Events
Imports Nesto.Infrastructure.Events

Public Class RapportViewModel
    Inherits BindableBase
    Implements INavigationAware

    Public Property configuracion As IConfiguracion
    Private Const empresaPorDefecto As String = "1"
    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly servicio As IRapportService
    Private ReadOnly dialogService As IDialogService
    Private ReadOnly _eventAggregator As IEventAggregator

    Public Sub New(configuracion As IConfiguracion, servicio As IRapportService, regionManager As IRegionManager, dialogService As IDialogService, eventAggregator As IEventAggregator)
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.regionManager = regionManager
        Me.dialogService = dialogService
        _eventAggregator = eventAggregator

        listaTiposRapports = servicio.CargarListaTipos()

        listaTiposCentros = New List(Of idDescripcionTipoCentro)
        listaTiposCentros.Add(New idDescripcionTipoCentro With {
                              .id = TiposCentro.NoSeSabe,
                              .descripcion = "No se sabe"})
        listaTiposCentros.Add(New idDescripcionTipoCentro With {
                              .id = TiposCentro.SoloEstetica,
                              .descripcion = "Sólo Estética"})
        listaTiposCentros.Add(New idDescripcionTipoCentro With {
                              .id = TiposCentro.SoloPeluqueria,
                              .descripcion = "Sólo Peluquería"})
        listaTiposCentros.Add(New idDescripcionTipoCentro With {
                              .id = TiposCentro.EsteticaYPeluqueria,
                              .descripcion = "Estética y Peluquería"})

        listaEstadosRapport = servicio.CargarListaEstados()

        ' Nesto#469: las opciones de la combo de empleados (el valor es el que guarda la API).
        listaEmpleados = New List(Of idByteDescripcion) From {
            New idByteDescripcion(0, "Sin empleados"),
            New idByteDescripcion(1, "1"),
            New idByteDescripcion(2, "2"),
            New idByteDescripcion(3, "3"),
            New idByteDescripcion(4, "4"),
            New idByteDescripcion(5, "5 o más")
        }

        cmdCrearCita = New RelayCommand(AddressOf OnCrearCita, AddressOf CanCrearCita)
        cmdGuardarCambios = New RelayCommand(Of Object)(AddressOf OnGuardarCambios, AddressOf CanGuardarCambios)

    End Sub

#Region "Propiedades de Nesto"
    Private _clienteCompleto As Object
    Public Property ClienteCompleto As Object
        Get
            Return _clienteCompleto
        End Get
        Set(value As Object)
            SetProperty(_clienteCompleto, value)
            VendedorEstetica = _clienteCompleto?.vendedor?.Trim()
            If _clienteCompleto?.VendedoresGrupoProducto IsNot Nothing AndAlso _clienteCompleto?.VendedoresGrupoProducto?.Count > 0 Then
                VendedorPeluqueria = _clienteCompleto?.VendedoresGrupoProducto(0)?.Vendedor?.Trim()
            Else
                VendedorPeluqueria = String.Empty
            End If
            RaisePropertyChanged(NameOf(EstaVisibleTipoCentro))
            PrerrellenarEmpleados()
            RaisePropertyChanged(NameOf(EstaVisibleEmpleados))
        End Set
    End Property

    ''' <summary>
    ''' Nesto#469: la combo de empleados solo se enseña cuando la API lo pide (hoy, clientes de la
    ''' Comunidad de Madrid). La regla no está aquí: está en NestoAPI (PoliticaEmpleadosCliente).
    ''' </summary>
    Public ReadOnly Property EstaVisibleEmpleados As Boolean
        Get
            Try
                Return _clienteCompleto IsNot Nothing AndAlso CBool(_clienteCompleto.preguntarEmpleados)
            Catch ex As Exception
                ' Un cliente que venga de un modelo sin la propiedad (código viejo): no se pregunta.
                Return False
            End Try
        End Get
    End Property

    Private _listaEmpleados As List(Of idByteDescripcion)
    Public Property listaEmpleados As List(Of idByteDescripcion)
        Get
            Return _listaEmpleados
        End Get
        Set(value As List(Of idByteDescripcion))
            SetProperty(_listaEmpleados, value)
        End Set
    End Property

    ''' <summary>
    ''' Si la ficha ya tiene el dato, la combo sale rellena y el vendedor solo la cambia si algo ha
    ''' cambiado. Solo se rellena si el rapport no trae ya un valor (un rapport que se está editando
    ''' conserva lo que puso el vendedor).
    ''' </summary>
    Private Sub PrerrellenarEmpleados()
        If rapport Is Nothing OrElse rapport.Empleados.HasValue OrElse _clienteCompleto Is Nothing Then
            Return
        End If
        Try
            Dim deLaFicha As Object = _clienteCompleto.empleados
            If deLaFicha IsNot Nothing Then
                rapport.Empleados = CByte(deLaFicha)
            End If
        Catch ex As Exception
            ' Modelo sin la propiedad: se deja vacía.
        End Try
    End Sub

    Public ReadOnly Property EstaVisibleTipoCentro As Boolean
        Get
            Return rapport IsNot Nothing AndAlso rapport.Id = 0 AndAlso VendedorEstetica = VendedorPeluqueria AndAlso VendedorEstetica = VendedorUsuario
        End Get
    End Property

    Private _fechaAviso As DateTime = DateTime.Now.AddDays(1)
    Public Property fechaAviso As DateTime
        Get
            Return _fechaAviso
        End Get
        Set(value As DateTime)
            SetProperty(_fechaAviso, value)
        End Set
    End Property

    Private _listaEstadosRapport As List(Of idShortDescripcion)
    Public Property listaEstadosRapport As List(Of idShortDescripcion)
        Get
            Return _listaEstadosRapport
        End Get
        Set(value As List(Of idShortDescripcion))
            SetProperty(_listaEstadosRapport, value)
        End Set
    End Property

    Private _listaTiposCentros As List(Of idDescripcionTipoCentro)
    Public Property listaTiposCentros As List(Of idDescripcionTipoCentro)
        Get
            Return _listaTiposCentros
        End Get
        Set(value As List(Of idDescripcionTipoCentro))
            SetProperty(_listaTiposCentros, value)
        End Set
    End Property

    Private _listaTiposRapports As List(Of idDescripcion)
    Public Property listaTiposRapports As List(Of idDescripcion)
        Get
            Return _listaTiposRapports
        End Get
        Set(value As List(Of idDescripcion))
            SetProperty(_listaTiposRapports, value)
        End Set
    End Property

    Private _quitarDeMiListado As Boolean
    Public Property QuitarDeMiListado As Boolean
        Get
            Return _quitarDeMiListado
        End Get
        Set(value As Boolean)
            SetProperty(_quitarDeMiListado, value)
        End Set
    End Property

    Private _rapport As SeguimientoClienteDTO
    Public Property rapport As SeguimientoClienteDTO
        Get
            Return _rapport
        End Get
        Set(value As SeguimientoClienteDTO)
            SetProperty(_rapport, value)
            cmdCrearCita.NotifyCanExecuteChanged()
            cmdGuardarCambios.NotifyCanExecuteChanged()
            PrerrellenarEmpleados()
            RaisePropertyChanged(NameOf(EstaVisibleEmpleados))
        End Set
    End Property

    Private _sePuedeCrearRapport As Boolean = True
    Public Property SePuedeCrearRapport As Boolean
        Get
            Return _sePuedeCrearRapport
        End Get
        Set(value As Boolean)
            SetProperty(_sePuedeCrearRapport, value)
        End Set
    End Property

    Private _vendedorEstetica As String
    Public Property VendedorEstetica As String
        Get
            Return _vendedorEstetica
        End Get
        Set(value As String)
            SetProperty(_vendedorEstetica, value)
        End Set
    End Property

    Private _vendedorPeluqueria As String
    Public Property VendedorPeluqueria As String
        Get
            Return _vendedorPeluqueria
        End Get
        Set(value As String)
            SetProperty(_vendedorPeluqueria, value)
        End Set
    End Property


    Private _vendedorUsuario As String
    Public Property VendedorUsuario As String
        Get
            Return _vendedorUsuario
        End Get
        Set(value As String)
            SetProperty(_vendedorUsuario, value)
        End Set
    End Property

#End Region


#Region "Comandos"
    Private _cmdCrearCita As RelayCommand
    Public Property cmdCrearCita As RelayCommand
        Get
            Return _cmdCrearCita
        End Get
        Private Set(value As RelayCommand)
            SetProperty(_cmdCrearCita, value)
        End Set
    End Property
    Private Function CanCrearCita() As Boolean
        Return Not IsNothing(rapport)
    End Function
    Private Async Sub OnCrearCita()
        Dim p As New DialogParameters
        Dim continuar As Boolean = False
        p.Add("message", "Se va a crear la tarea. ¿Desea continuar?")
        dialogService.ShowDialog("ConfirmationDialog", p, Sub(r)
                                                              If r.Result = ButtonResult.OK Then
                                                                  continuar = True
                                                              End If
                                                          End Sub)
        If Not continuar Then
            Return
        End If

        SePuedeCrearRapport = False
        Try
            Await servicio.CrearCita(rapport, fechaAviso)
            SePuedeCrearRapport = True
            dialogService.ShowNotification("Cita", "Se ha creado la cita correctamente")
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            SePuedeCrearRapport = True
        End Try
    End Sub


    Private _cmdGuardarCambios As RelayCommand(Of Object)
    Public Property cmdGuardarCambios As RelayCommand(Of Object)
        Get
            Return _cmdGuardarCambios
        End Get
        Private Set(value As RelayCommand(Of Object))
            SetProperty(_cmdGuardarCambios, value)
        End Set
    End Property
    ' NestoAPI#294: guard de reentrada. Con el guardado en vuelo (servidor lento) el botón seguía
    ' activo y cada clic extra disparaba otro POST: dos peticiones concurrentes pasaban ambas el
    ' check del servidor y se creaban rapports duplicados. Mismo patrón que SePuedeCrearRapport.
    Private _guardandoRapport As Boolean = False
    Private Function CanGuardarCambios(arg As Object) As Boolean
        Return Not _guardandoRapport AndAlso Not IsNothing(rapport) AndAlso (rapport.Usuario.ToLower = configuracion.usuario.ToLower OrElse configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION))
    End Function
    Private Async Sub OnGuardarCambios(arg As Object)
        If _guardandoRapport Then
            Return
        End If
        If Not EstaVisibleTipoCentro Then
            rapport.TipoCentro = TiposCentro.NoSeSabe
        End If
        _guardandoRapport = True
        cmdGuardarCambios.NotifyCanExecuteChanged()
        Dim texto As String
        Try
            texto = Await servicio.crearRapport(rapport)
            If rapport.Estado = Constantes.Rapports.Estados.GESTION_ADMINISTRATIVA Then
                texto += vbCrLf + Await servicio.CrearTareaPlanner(rapport)
            End If
            If QuitarDeMiListado Then
                Await servicio.QuitarDeMiListado(rapport, VendedorEstetica, VendedorPeluqueria)
                QuitarDeMiListado = False
            End If
            _eventAggregator.GetEvent(Of RapportGuardadoEvent).Publish(0)
            dialogService.ShowNotification("Rapport", texto)
        Catch ex As Exception
            ' Nesto#206: la lista ya lo tenía como fila; que sepa que no se ha guardado.
            _eventAggregator.GetEvent(Of RapportNoGuardadoEvent).Publish(rapport)
            dialogService.ShowError(ex.Message)
        Finally
            _guardandoRapport = False
            cmdGuardarCambios.NotifyCanExecuteChanged()
        End Try
    End Sub
#End Region


    Public Overloads Async Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo
        rapport = navigationContext.Parameters("rapportParameter")
        If VendedorUsuario Is Nothing Then
            VendedorUsuario = Await configuracion.leerParametro("1", "Vendedor")
        End If
    End Sub

    Public Function IsNavigationTarget(navigationContext As NavigationContext) As Boolean Implements INavigationAware.IsNavigationTarget
        Return False
    End Function

    Public Sub OnNavigatedFrom(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedFrom

    End Sub

    Public Structure idByteDescripcion
        Public Sub New(_id As Byte, _descripcion As String)
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As Byte
        Property descripcion As String
    End Structure

    Public Structure idDescripcionTipoCentro
        Public Sub New(
       ByVal _id As TiposCentro,
       ByVal _descripcion As String
       )
            id = _id
            descripcion = _descripcion
        End Sub
        Property id As TiposCentro
        Property descripcion As String
    End Structure

End Class
