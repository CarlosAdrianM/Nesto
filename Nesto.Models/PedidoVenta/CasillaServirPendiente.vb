Imports CommunityToolkit.Mvvm.ComponentModel

''' <summary>
''' NestoAPI#688: la casilla «Servir lo pendiente en cuanto haya stock», común a la plantilla y al detalle del pedido.
''' La API decide si se enseña (solo con «Todo ahora») y qué valor propone (marcada al elegir «Todo ahora»); aquí solo
''' se recuerda si el usuario la ha tocado, para que la respuesta siguiente no le pise lo que eligió, y qué viaja al
''' guardar: el valor solo si la tocó o cambió el modo de facturación; si no, Nothing (no se manda, como hasta ahora).
''' </summary>
Public Class CasillaServirPendiente
    Inherits ObservableObject

    Public Const TEXTO As String = "Servir lo pendiente en cuanto haya stock"
    Public Const AYUDA_POR_DEFECTO As String = "Si está marcada, lo que quede pendiente sale de una vez, sin portes, en cuanto haya stock de todo. Si no, se entrega cuando lo pida el cliente."

    Private _visible As Boolean
    ''' <summary>Si se enseña (lo dice la API; con la API anterior, nunca).</summary>
    Public Property Visible As Boolean
        Get
            Return _visible
        End Get
        Private Set(value As Boolean)
            SetProperty(_visible, value)
        End Set
    End Property

    Private _valor As Boolean = True
    ''' <summary>Lo que enseña la casilla. Cambiarlo desde la pantalla cuenta como «tocada».</summary>
    Public Property Valor As Boolean
        Get
            Return _valor
        End Get
        Set(value As Boolean)
            If SetProperty(_valor, value) Then
                Tocada = True
                RaiseEvent CambiadaPorElUsuario(Me, value)
            End If
        End Set
    End Property

    Private _motivo As String
    ''' <summary>El texto de la API para el tooltip (o la ayuda por defecto).</summary>
    Public Property Ayuda As String
        Get
            Return If(String.IsNullOrWhiteSpace(_motivo), AYUDA_POR_DEFECTO, _motivo)
        End Get
        Private Set(value As String)
            SetProperty(_motivo, value, NameOf(Ayuda))
        End Set
    End Property

    ''' <summary>NestoAPI#688: el usuario la ha cambiado en pantalla (no salta con lo que propone la API). En la nota de
    ''' entrega el cambio no espera a guardar: va a su propio endpoint.</summary>
    Public Event CambiadaPorElUsuario(sender As Object, valor As Boolean)

    ''' <summary>Deja el valor anterior sin contar como tocada (p. ej. si la API rechaza el cambio en la nota).</summary>
    Public Sub Revertir(valorAnterior As Boolean)
        PonerSinTocar(valorAnterior)
        Tocada = False
    End Sub

    ''' <summary>True si el usuario la ha cambiado desde que se eligió el modo de facturación actual.</summary>
    Public Property Tocada As Boolean

    ''' <summary>Refleja la respuesta de la API. Nothing (sin respuesta o API anterior) = oculta.</summary>
    Public Sub Aplicar(opcion As OpcionServirPendienteDTO)
        If opcion Is Nothing OrElse Not opcion.Visible Then
            Visible = False
            Ayuda = Nothing
            Tocada = False ' si vuelve a aparecer, manda otra vez lo que proponga la API (decisión 12)
            Return
        End If
        Ayuda = opcion.Motivo
        If Not Tocada Then
            PonerSinTocar(If(opcion.Valor, True))
        End If
        Visible = True
    End Sub

    ''' <summary>Al cargar un pedido o empezar otro: oculta, sin tocar y con el valor guardado (Nothing = marcada).</summary>
    Public Sub Reiniciar(Optional valorGuardado As Boolean? = Nothing)
        Visible = False
        Ayuda = Nothing
        PonerSinTocar(If(valorGuardado, True))
        Tocada = False
    End Sub

    ''' <summary>Al cambiar el modo de facturación: lo que se tocó con el modo anterior ya no cuenta.</summary>
    Public Sub ModoFacturacionCambiado()
        Tocada = False
    End Sub

    ''' <summary>
    ''' Lo que viaja en servirPendienteAlHaberStock al guardar: el valor si se enseña y el usuario la tocó o cambió el
    ''' modo de facturación (en un pedido nuevo, siempre que se enseña); si no, Nothing y el servidor no toca nada.
    ''' </summary>
    Public Function ValorParaGuardar(modoFacturacionCambiado As Boolean) As Boolean?
        If Not Visible Then
            Return Nothing
        End If
        If Tocada OrElse modoFacturacionCambiado Then
            Return Valor
        End If
        Return Nothing
    End Function

    ''' <summary>Cambia el valor sin marcarla como tocada: no lo ha elegido el usuario.</summary>
    Private Sub PonerSinTocar(nuevo As Boolean)
        SetProperty(_valor, nuevo, NameOf(Valor))
    End Sub
End Class
