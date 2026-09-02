Imports System.Security.Permissions

Public Class Telefono
    Dim telefonos() As String = {}
    Dim stringSeparators() As String = {"/"}

    Public Sub New(listaTelefonos As String, Optional quitarPrefijos As Boolean = False)
        ' 02/09/26: con la cadena VACÍA ("", el teléfono en blanco de una ficha) el Replace de VB
        ' devuelve Nothing, el Split no se ejecutaba y `telefonos` se quedaba a Nothing: el primer
        ' MovilUnico/FijoUnico reventaba con NullReference (ClientesViewModel.CargarDeudas, MariaJose).
        If String.IsNullOrWhiteSpace(listaTelefonos) Then
            Return
        End If
        listaTelefonos = Replace(listaTelefonos, "(", String.Empty)
        listaTelefonos = Replace(listaTelefonos, ")", String.Empty)
        listaTelefonos = Replace(listaTelefonos, " ", String.Empty)
        listaTelefonos = Replace(listaTelefonos, "-", String.Empty)
        telefonos = If(listaTelefonos?.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries), New String() {})
        If quitarPrefijos Then
            For t = 0 To telefonos.Length - 1
                If telefonos(t).StartsWith("+") OrElse telefonos(t).StartsWith("00") Then
                    telefonos(t) = Right(telefonos(t), 9)
                End If
            Next
        End If
    End Sub

    Public ReadOnly Property TodosLosTelefonos As List(Of String)
        Get
            Return New List(Of String)(telefonos)
        End Get
    End Property

    Public Function FijoUnico() As String
        For Each t As String In telefonos
            If (t.Length >= 9) AndAlso t.Substring(0, 1) = "9" Then
                Return Left(t, 9)
            End If
        Next
        Return String.Empty
    End Function

    Public Function MovilUnico() As String
        For Each t As String In telefonos
            If (t.Length >= 9) And (
                (t.Substring(0, 1) = "6") Or
                (t.Substring(0, 1) = "7") Or
                (t.Substring(0, 1) = "8")
                ) Then
                Return Left(t, 9)
            End If
        Next
        Return String.Empty
    End Function

End Class
