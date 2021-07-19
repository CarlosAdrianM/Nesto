Imports Prism.Commands
Imports Prism.Regions
Imports Nesto.Contratos
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs

Public Class RapportViewModel
    Inherits BindableBase
    Implements INavigationAware

    Public Property configuracion As IConfiguracion
    Private Const empresaPorDefecto As String = "1"
    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly servicio As IRapportService
    Private ReadOnly dialogService As IDialogService

    Public Sub New(configuracion As IConfiguracion, servicio As IRapportService, regionManager As IRegionManager, dialogService As IDialogService)
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.regionManager = regionManager
        Me.dialogService = dialogService

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

        cmdCrearCita = New DelegateCommand(AddressOf OnCrearCita, AddressOf CanCrearCita)
        cmdGuardarCambios = New DelegateCommand(Of Object)(AddressOf OnGuardarCambios, AddressOf CanGuardarCambios)

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
        End Set
    End Property

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

    Private _rapport As SeguimientoClienteDTO
    Public Property rapport As SeguimientoClienteDTO
        Get
            Return _rapport
        End Get
        Set(value As SeguimientoClienteDTO)
            SetProperty(_rapport, value)
            cmdCrearCita.RaiseCanExecuteChanged()
            cmdGuardarCambios.RaiseCanExecuteChanged()
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
    Private _cmdCrearCita As DelegateCommand
    Public Property cmdCrearCita As DelegateCommand
        Get
            Return _cmdCrearCita
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdCrearCita, value)
        End Set
    End Property
    Private Function CanCrearCita() As Boolean
        Return Not IsNothing(rapport)
    End Function
    Private Async Sub OnCrearCita()
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


    Private _cmdGuardarCambios As DelegateCommand(Of Object)
    Public Property cmdGuardarCambios As DelegateCommand(Of Object)
        Get
            Return _cmdGuardarCambios
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdGuardarCambios, value)
        End Set
    End Property
    Private Function CanGuardarCambios(arg As Object) As Boolean
        Return Not IsNothing(rapport) AndAlso (rapport.Usuario.ToLower = configuracion.usuario.ToLower OrElse configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION))
    End Function
    Private Async Sub OnGuardarCambios(arg As Object)
        If Not EstaVisibleTipoCentro Then
            rapport.TipoCentro = TiposCentro.NoSeSabe
        End If
        Dim texto As String
        Try
            texto = Await servicio.crearRapport(rapport)
            dialogService.ShowNotification("Rapport", texto)
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
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
