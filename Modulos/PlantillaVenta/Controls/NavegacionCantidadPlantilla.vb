Imports System.Windows.Input

''' <summary>
''' Nesto#633 (sugerencia 1060): Intro en la casilla de cantidad confirma lo escrito y devuelve el cursor al
''' buscador con el texto seleccionado. Tab sigue como hoy (pasa a la siguiente casilla).
'''
''' Se marca al PULSAR Intro (y se da por manejado, para que no haga nada más) y se vuelve al buscador al
''' SOLTARLO: si el foco cambiara al pulsar, el KeyUp de ese mismo Intro llegaría al buscador y fijaría su
''' texto como filtro. Si el ShortUpDown no ha podido confirmar la cantidad (marca Intro como manejado),
''' el cursor se queda en la cantidad.
''' </summary>
Public Class NavegacionCantidadPlantilla

    Public ReadOnly Property VolverAlBuscadorPendiente As Boolean

    ''' <summary>Devuelve True si hay que marcar la pulsación como manejada.</summary>
    Public Function AlPulsar(tecla As Key, modificadores As ModifierKeys, yaManejada As Boolean) As Boolean
        If tecla <> Key.Enter OrElse modificadores <> ModifierKeys.None OrElse yaManejada Then
            Return False
        End If
        _VolverAlBuscadorPendiente = True
        Return True
    End Function

    ''' <summary>Devuelve True si hay que llevar el cursor al buscador.</summary>
    Public Function AlSoltar(tecla As Key) As Boolean
        If tecla <> Key.Enter OrElse Not VolverAlBuscadorPendiente Then
            Return False
        End If
        _VolverAlBuscadorPendiente = False
        Return True
    End Function

    Public Sub Cancelar()
        _VolverAlBuscadorPendiente = False
    End Sub

End Class
