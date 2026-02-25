Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Services.Dialogs

Public Class CopiarSeguimientosViewModel
    Inherits BindableBase
    Implements IDialogAware

    Private ReadOnly _servicio As IRapportService
    Private ReadOnly _configuracion As IConfiguracion

#Region "Propiedades"

    Private _titulo As String = "Copiar seguimientos a otro cliente"
    Public ReadOnly Property Title As String Implements IDialogAware.Title
        Get
            Return _titulo
        End Get
    End Property

    Private _empresa As String
    Public Property Empresa As String
        Get
            Return _empresa
        End Get
        Set(value As String)
            SetProperty(_empresa, value)
        End Set
    End Property

    Private _clienteOrigen As String
    Public Property ClienteOrigen As String
        Get
            Return _clienteOrigen
        End Get
        Set(value As String)
            If SetProperty(_clienteOrigen, value) Then
                EjecutarCommand.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _clienteCompletoOrigen As ControlesUsuario.Models.ClienteDTO
    Public Property ClienteCompletoOrigen As ControlesUsuario.Models.ClienteDTO
        Get
            Return _clienteCompletoOrigen
        End Get
        Set(value As ControlesUsuario.Models.ClienteDTO)
            SetProperty(_clienteCompletoOrigen, value)
        End Set
    End Property

    Private _contactoOrigen As String = "0"
    Public Property ContactoOrigen As String
        Get
            Return _contactoOrigen
        End Get
        Set(value As String)
            If SetProperty(_contactoOrigen, value) Then
                EjecutarCommand.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _clienteDestino As String
    Public Property ClienteDestino As String
        Get
            Return _clienteDestino
        End Get
        Set(value As String)
            If SetProperty(_clienteDestino, value) Then
                EjecutarCommand.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _clienteCompletoDestino As ControlesUsuario.Models.ClienteDTO
    Public Property ClienteCompletoDestino As ControlesUsuario.Models.ClienteDTO
        Get
            Return _clienteCompletoDestino
        End Get
        Set(value As ControlesUsuario.Models.ClienteDTO)
            SetProperty(_clienteCompletoDestino, value)
        End Set
    End Property

    Private _contactoDestino As String = "0"
    Public Property ContactoDestino As String
        Get
            Return _contactoDestino
        End Get
        Set(value As String)
            If SetProperty(_contactoDestino, value) Then
                EjecutarCommand.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _estaProcesando As Boolean = False
    Public Property EstaProcesando As Boolean
        Get
            Return _estaProcesando
        End Get
        Set(value As Boolean)
            SetProperty(_estaProcesando, value)
            EjecutarCommand.RaiseCanExecuteChanged()
            CerrarCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _mensaje As String
    Public Property Mensaje As String
        Get
            Return _mensaje
        End Get
        Set(value As String)
            SetProperty(_mensaje, value)
        End Set
    End Property

    Private _registrosCopiados As Integer?
    Public Property RegistrosCopiados As Integer?
        Get
            Return _registrosCopiados
        End Get
        Set(value As Integer?)
            If SetProperty(_registrosCopiados, value) Then
                RaisePropertyChanged(NameOf(MostrarResultado))
            End If
        End Set
    End Property

    Private _eliminarOrigen As Boolean = False
    Public Property EliminarOrigen As Boolean
        Get
            Return _eliminarOrigen
        End Get
        Set(value As Boolean)
            SetProperty(_eliminarOrigen, value)
        End Set
    End Property

    Public ReadOnly Property MostrarResultado As Boolean
        Get
            Return RegistrosCopiados.HasValue
        End Get
    End Property

#End Region

#Region "Commands"

    Private _ejecutarCommand As DelegateCommand
    Public Property EjecutarCommand As DelegateCommand
        Get
            If _ejecutarCommand Is Nothing Then
                _ejecutarCommand = New DelegateCommand(AddressOf OnEjecutar, AddressOf CanEjecutar)
            End If
            Return _ejecutarCommand
        End Get
        Set(value As DelegateCommand)
            _ejecutarCommand = value
        End Set
    End Property

    Private _cerrarCommand As DelegateCommand
    Public Property CerrarCommand As DelegateCommand
        Get
            If _cerrarCommand Is Nothing Then
                _cerrarCommand = New DelegateCommand(AddressOf OnCerrar, AddressOf CanCerrar)
            End If
            Return _cerrarCommand
        End Get
        Set(value As DelegateCommand)
            _cerrarCommand = value
        End Set
    End Property

#End Region

#Region "Constructor"

    Public Sub New(servicio As IRapportService, configuracion As IConfiguracion)
        _servicio = servicio
        _configuracion = configuracion
        _empresa = Constantes.Empresas.EMPRESA_DEFECTO
    End Sub

#End Region

#Region "IDialogAware"

    Public Event RequestClose As Action(Of IDialogResult) Implements IDialogAware.RequestClose

    Public Sub OnDialogOpened(parameters As IDialogParameters) Implements IDialogAware.OnDialogOpened
        If parameters IsNot Nothing Then
            If parameters.ContainsKey("empresa") Then
                Empresa = parameters.GetValue(Of String)("empresa")
            End If
            If parameters.ContainsKey("cliente") Then
                ClienteOrigen = parameters.GetValue(Of String)("cliente")
            End If
            If parameters.ContainsKey("contacto") Then
                ContactoOrigen = parameters.GetValue(Of String)("contacto")
            End If
        End If
    End Sub

    Public Sub OnDialogClosed() Implements IDialogAware.OnDialogClosed
    End Sub

    Public Function CanCloseDialog() As Boolean Implements IDialogAware.CanCloseDialog
        Return Not EstaProcesando
    End Function

#End Region

#Region "Command Handlers"

    Private Function CanEjecutar() As Boolean
        If EstaProcesando OrElse
           String.IsNullOrWhiteSpace(ClienteOrigen) OrElse
           String.IsNullOrWhiteSpace(ClienteDestino) Then
            Return False
        End If

        Dim mismoCliente = ClienteOrigen.Trim() = ClienteDestino.Trim()
        If mismoCliente Then
            Return ContactoOrigen?.Trim() <> ContactoDestino?.Trim()
        End If

        Return True
    End Function

    Private Async Sub OnEjecutar()
        EstaProcesando = True
        Dim accion = If(EliminarOrigen, "Moviendo", "Copiando")
        Mensaje = $"{accion} seguimientos..."

        Try
            Dim resultado = Await _servicio.CopiarSeguimientos(Empresa, ClienteOrigen, ContactoOrigen, ClienteDestino, ContactoDestino, EliminarOrigen)
            RegistrosCopiados = resultado
            Dim accionPasada = If(EliminarOrigen, "movido", "copiado")
            Mensaje = $"Se han {accionPasada} {resultado} seguimientos correctamente."
        Catch ex As Exception
            Mensaje = $"Error: {ex.Message}"
        Finally
            EstaProcesando = False
        End Try
    End Sub

    Private Function CanCerrar() As Boolean
        Return Not EstaProcesando
    End Function

    Private Sub OnCerrar()
        RaiseEvent RequestClose(New DialogResult(ButtonResult.OK))
    End Sub

#End Region

End Class
