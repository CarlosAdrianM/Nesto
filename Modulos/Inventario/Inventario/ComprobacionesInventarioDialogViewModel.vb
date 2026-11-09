Imports System.Collections.ObjectModel
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Nesto#597 (gemela de NestoAPI#790): lo que se comprueba antes de contabilizar el inventario. Los bloqueantes (en rojo) no
''' dejan seguir; con solo avisos (en ámbar) se puede «Continuar de todos modos». Vuelve OK para seguir y Cancel si no.
''' NestoAPI#784: los bloqueantes que se confirman (SeConfirma, hoy RecontadaSinContar) dejan seguir solo si el usuario marca
''' «He contado y de verdad hay 0»; entonces el OK lleva <see cref="PARAMETRO_CONFIRMAR_CONTADO_CERO"/> = True. Los demás
''' bloqueantes (FilasDuplicadas…) no se saltan nunca.
''' </summary>
Public Class ComprobacionesInventarioDialogViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "ComprobacionesInventarioDialog"
    Public Const PARAMETRO_COMPROBACIONES As String = "comprobaciones"
    ''' <summary>NestoAPI#784: en el resultado OK, True si el usuario ha confirmado el contado 0 (ConfirmarContadoCero).</summary>
    Public Const PARAMETRO_CONFIRMAR_CONTADO_CERO As String = "confirmarContadoCero"
    Public Const TEXTO_CONFIRMAR_CONTADO_CERO As String = "He contado y de verdad hay 0"

    Public Sub New()
        Title = "Comprobaciones del inventario"
        ContinuarCommand = New RelayCommand(AddressOf OnContinuar, Function() PuedeContinuar)
        CancelarCommand = New RelayCommand(Sub() RaiseRequestClose(ResultadoBoton.Cancel))
    End Sub

    Public Property ContinuarCommand As RelayCommand
    Public Property CancelarCommand As RelayCommand

    Public ReadOnly Property Bloqueantes As New ObservableCollection(Of ComprobacionInventarioDTO)
    Public ReadOnly Property Avisos As New ObservableCollection(Of ComprobacionInventarioDTO)
    Public ReadOnly Property Recuento As New ObservableCollection(Of RecuentoUsuarioInventarioDTO)

    Private _cabecera As String
    Public Property Cabecera As String
        Get
            Return _cabecera
        End Get
        Private Set(value As String)
            SetProperty(_cabecera, value)
        End Set
    End Property

    Private _confirmadoContadoCero As Boolean
    ''' <summary>NestoAPI#784: la casilla «He contado y de verdad hay 0».</summary>
    Public Property ConfirmadoContadoCero As Boolean
        Get
            Return _confirmadoContadoCero
        End Get
        Set(value As Boolean)
            If SetProperty(_confirmadoContadoCero, value) Then
                OnPropertyChanged(NameOf(PuedeContinuar))
                ContinuarCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Public ReadOnly Property HayBloqueantes As Boolean
        Get
            Return Bloqueantes.Count > 0
        End Get
    End Property

    ''' <summary>NestoAPI#784: bloqueantes que no se pueden confirmar (FilasDuplicadas, CantidadDisparatada…).</summary>
    Public ReadOnly Property HayBloqueantesSinConfirmacion As Boolean
        Get
            Return Bloqueantes.Any(Function(b) Not b.SeConfirma)
        End Get
    End Property

    ''' <summary>NestoAPI#784: hay bloqueantes que se confirman y ninguno que no: se enseña la casilla.</summary>
    Public ReadOnly Property SePuedeConfirmarContadoCero As Boolean
        Get
            Return HayBloqueantes AndAlso Not HayBloqueantesSinConfirmacion
        End Get
    End Property

    Public ReadOnly Property HayAvisos As Boolean
        Get
            Return Avisos.Count > 0
        End Get
    End Property

    ''' <summary>El botón se enseña si no hay bloqueantes que no se confirman (con la casilla sin marcar sale desactivado).</summary>
    Public ReadOnly Property MostrarContinuar As Boolean
        Get
            Return Not HayBloqueantesSinConfirmacion
        End Get
    End Property

    Public ReadOnly Property PuedeContinuar As Boolean
        Get
            If Not HayBloqueantes Then
                Return True
            End If
            Return SePuedeConfirmarContadoCero AndAlso ConfirmadoContadoCero
        End Get
    End Property

    Public ReadOnly Property TextoContinuar As String
        Get
            Return If(HayAvisos OrElse HayBloqueantes, "Continuar de todos modos", "Continuar")
        End Get
    End Property

    Public ReadOnly Property Explicacion As String
        Get
            If HayBloqueantesSinConfirmacion Then
                Return "Hay problemas que impiden contabilizar el inventario. Corríjalos y vuelva a intentarlo."
            End If
            If HayBloqueantes Then
                Return "Hay productos que ya se contabilizaron ese día, se han vuelto a abrir y siguen con contado 0: si los " +
                    "contabiliza, su stock se quedará a cero. Cuéntelos o, si de verdad no hay ninguno, marque «" +
                    TEXTO_CONFIRMAR_CONTADO_CERO + "»."
            End If
            If HayAvisos Then
                Return "Revise los avisos: no impiden contabilizar, pero conviene comprobarlos antes."
            End If
            Return "No se ha encontrado ningún problema."
        End Get
    End Property

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        Cargar(parameters?.GetValue(Of ComprobacionesInventarioDTO)(PARAMETRO_COMPROBACIONES))
    End Sub

    Public Sub Cargar(comprobaciones As ComprobacionesInventarioDTO)
        Bloqueantes.Clear()
        Avisos.Clear()
        Recuento.Clear()
        _confirmadoContadoCero = False
        If comprobaciones IsNot Nothing Then
            For Each c In If(comprobaciones.Bloqueantes, New List(Of ComprobacionInventarioDTO))
                Bloqueantes.Add(c)
            Next
            For Each c In If(comprobaciones.Avisos, New List(Of ComprobacionInventarioDTO))
                Avisos.Add(c)
            Next
            For Each r In If(comprobaciones.Recuento, New List(Of RecuentoUsuarioInventarioDTO))
                Recuento.Add(r)
            Next
            Cabecera = $"Inventario de {comprobaciones.Almacen?.Trim()} del {comprobaciones.Fecha:dd/MM/yyyy}"
        Else
            Cabecera = String.Empty
        End If
        OnPropertyChanged(NameOf(ConfirmadoContadoCero))
        OnPropertyChanged(NameOf(HayBloqueantes))
        OnPropertyChanged(NameOf(HayBloqueantesSinConfirmacion))
        OnPropertyChanged(NameOf(SePuedeConfirmarContadoCero))
        OnPropertyChanged(NameOf(HayAvisos))
        OnPropertyChanged(NameOf(MostrarContinuar))
        OnPropertyChanged(NameOf(PuedeContinuar))
        OnPropertyChanged(NameOf(TextoContinuar))
        OnPropertyChanged(NameOf(Explicacion))
        ContinuarCommand.NotifyCanExecuteChanged()
    End Sub

    Private Sub OnContinuar()
        If Not PuedeContinuar Then
            Return
        End If
        Dim parametros As New ParametrosDialogo From {
            {PARAMETRO_CONFIRMAR_CONTADO_CERO, SePuedeConfirmarContadoCero AndAlso ConfirmadoContadoCero}
        }
        RaiseRequestClose(New ResultadoDialogo(ResultadoBoton.OK, parametros))
    End Sub
End Class
