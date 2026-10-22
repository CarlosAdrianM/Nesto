Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts

''' <summary>
''' Nesto#543 / NestoAPI#691: al meter un curso en la plantilla se elige el evento (si hay varios; si hay uno solo,
''' sale elegido) y siempre la modalidad, con su nombre y su importe. Aceptar solo se habilita con los dos elegidos.
''' Entra con «eventos» (List(Of EventoCursoDTO), ya solo los que tienen plazas) y «producto» (el texto del curso);
''' sale con «evento» (EventoCursoDTO) y «modalidad» (ModalidadEventoDTO).
''' </summary>
Public Class ElegirEventoCursoDialogViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "ElegirEventoCursoDialog"
    Public Const PARAMETRO_EVENTOS As String = "eventos"
    Public Const PARAMETRO_PRODUCTO As String = "producto"
    Public Const PARAMETRO_EVENTO As String = "evento"
    Public Const PARAMETRO_MODALIDAD As String = "modalidad"

    Public Sub New()
        Title = "Curso: evento y modalidad"
    End Sub

    Private _producto As String
    Public Property Producto As String
        Get
            Return _producto
        End Get
        Set(value As String)
            Dim unused = SetProperty(_producto, value)
        End Set
    End Property

    Private _eventos As List(Of EventoCursoDTO) = New List(Of EventoCursoDTO)
    Public Property Eventos As List(Of EventoCursoDTO)
        Get
            Return _eventos
        End Get
        Set(value As List(Of EventoCursoDTO))
            If SetProperty(_eventos, If(value, New List(Of EventoCursoDTO))) Then
                OnPropertyChanged(NameOf(HayVariosEventos))
                ' Con un solo evento no hay nada que elegir: sale elegido
                EventoSeleccionado = If(_eventos.Count = 1, _eventos(0), Nothing)
            End If
        End Set
    End Property

    ''' <summary>Con varios eventos se enseña la lista para elegir; con uno solo, solo se ve cuál es.</summary>
    Public ReadOnly Property HayVariosEventos As Boolean
        Get
            Return Eventos IsNot Nothing AndAlso Eventos.Count > 1
        End Get
    End Property

    Private _eventoSeleccionado As EventoCursoDTO
    Public Property EventoSeleccionado As EventoCursoDTO
        Get
            Return _eventoSeleccionado
        End Get
        Set(value As EventoCursoDTO)
            If SetProperty(_eventoSeleccionado, value) Then
                ' La modalidad se elige siempre a mano, también al cambiar de evento
                Modalidades = If(value?.Modalidades, New List(Of ModalidadEventoDTO))
                ModalidadSeleccionada = Nothing
                AceptarCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private _modalidades As List(Of ModalidadEventoDTO) = New List(Of ModalidadEventoDTO)
    Public Property Modalidades As List(Of ModalidadEventoDTO)
        Get
            Return _modalidades
        End Get
        Set(value As List(Of ModalidadEventoDTO))
            Dim unused = SetProperty(_modalidades, value)
        End Set
    End Property

    Private _modalidadSeleccionada As ModalidadEventoDTO
    Public Property ModalidadSeleccionada As ModalidadEventoDTO
        Get
            Return _modalidadSeleccionada
        End Get
        Set(value As ModalidadEventoDTO)
            If SetProperty(_modalidadSeleccionada, value) Then
                AceptarCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private _aceptarCommand As RelayCommand
    Public ReadOnly Property AceptarCommand As RelayCommand
        Get
            If _aceptarCommand Is Nothing Then
                _aceptarCommand = New RelayCommand(
                    Sub()
                        RaiseRequestClose(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {
                            {PARAMETRO_EVENTO, EventoSeleccionado},
                            {PARAMETRO_MODALIDAD, ModalidadSeleccionada}
                        }))
                    End Sub,
                    Function() EventoSeleccionado IsNot Nothing AndAlso ModalidadSeleccionada IsNot Nothing)
            End If
            Return _aceptarCommand
        End Get
    End Property

    Private _cancelarCommand As RelayCommand
    Public ReadOnly Property CancelarCommand As RelayCommand
        Get
            If _cancelarCommand Is Nothing Then
                _cancelarCommand = New RelayCommand(Sub() RaiseRequestClose(ResultadoBoton.Cancel))
            End If
            Return _cancelarCommand
        End Get
    End Property

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        If parameters.ContainsKey(PARAMETRO_PRODUCTO) Then
            Producto = parameters.GetValue(Of String)(PARAMETRO_PRODUCTO)
        End If
        If parameters.ContainsKey(PARAMETRO_EVENTOS) Then
            Eventos = parameters.GetValue(Of List(Of EventoCursoDTO))(PARAMETRO_EVENTOS)
        End If
    End Sub
End Class
