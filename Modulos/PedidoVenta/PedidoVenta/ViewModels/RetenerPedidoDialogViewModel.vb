Imports System.Globalization
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts

''' <summary>
''' NestoAPI#698: pide el motivo (obligatorio) y, si se sabe, hasta cuándo se retiene el pedido. Sin fecha = hasta nuevo
''' aviso. «Retener» devuelve OK con <see cref="PARAMETRO_MOTIVO"/> y <see cref="PARAMETRO_HASTA_FECHA"/> (Date? o Nothing);
''' «Cancelar» (o cerrar) devuelve Cancel y no se retiene nada.
''' </summary>
Public Class RetenerPedidoDialogViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "RetenerPedidoDialog"
    Public Const PARAMETRO_NUMERO As String = "numero"
    Public Const PARAMETRO_MOTIVO As String = "motivo"
    Public Const PARAMETRO_HASTA_FECHA As String = "hastaFecha"

    Public Sub New()
        Title = RetencionPedido.TITULO_RETENER
        Mensaje = "Mientras esté retenido, el pedido no entrará en el picking. Indique el motivo y, si lo sabe, hasta qué día. " &
            "Si deja la fecha en blanco, quedará retenido hasta nuevo aviso."
    End Sub

    Private _mensaje As String
    Public Property Mensaje As String
        Get
            Return _mensaje
        End Get
        Set(value As String)
            Dim unused = SetProperty(_mensaje, value)
        End Set
    End Property

    Private _motivo As String
    Public Property Motivo As String
        Get
            Return _motivo
        End Get
        Set(value As String)
            If SetProperty(_motivo, value) Then
                RetenerCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private _hastaFecha As Date?
    ''' <summary>Nothing = hasta nuevo aviso.</summary>
    Public Property HastaFecha As Date?
        Get
            Return _hastaFecha
        End Get
        Set(value As Date?)
            If SetProperty(_hastaFecha, value) Then
                OnPropertyChanged(NameOf(TextoHasta))
            End If
        End Set
    End Property

    Public ReadOnly Property TextoHasta As String
        Get
            Return If(HastaFecha.HasValue,
                      $"Retenido hasta el {HastaFecha.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}.",
                      "Retenido hasta nuevo aviso.")
        End Get
    End Property

    ''' <summary>No se puede retener hasta un día que ya ha pasado.</summary>
    Public ReadOnly Property FechaMinima As Date
        Get
            Return Date.Today
        End Get
    End Property

    Private _retenerCommand As RelayCommand
    Public ReadOnly Property RetenerCommand As RelayCommand
        Get
            If _retenerCommand Is Nothing Then
                _retenerCommand = New RelayCommand(
                    Sub()
                        RaiseRequestClose(New ResultadoDialogo(ResultadoBoton.OK, New ParametrosDialogo From {
                            {PARAMETRO_MOTIVO, Motivo?.Trim()},
                            {PARAMETRO_HASTA_FECHA, HastaFecha}
                        }))
                    End Sub,
                    Function() Not String.IsNullOrWhiteSpace(Motivo))
            End If
            Return _retenerCommand
        End Get
    End Property

    Private _quitarFechaCommand As RelayCommand
    Public ReadOnly Property QuitarFechaCommand As RelayCommand
        Get
            If _quitarFechaCommand Is Nothing Then
                _quitarFechaCommand = New RelayCommand(Sub() HastaFecha = Nothing)
            End If
            Return _quitarFechaCommand
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
        If parameters IsNot Nothing AndAlso parameters.ContainsKey(PARAMETRO_NUMERO) Then
            Title = $"{RetencionPedido.TITULO_RETENER} {parameters.GetValue(Of Integer)(PARAMETRO_NUMERO)}"
        End If
    End Sub
End Class
