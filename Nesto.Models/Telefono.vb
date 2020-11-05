Imports System.Security.Permissions

Public Class Telefono
    Dim telefonos() As String = {}
    Dim stringSeparators() As String = {"/"}

    Public Sub New(listaTelefonos As String)
        If IsNothing(listaTelefonos) Then
            Return
        End If
        listaTelefonos = Replace(listaTelefonos, "(", String.Empty)
        listaTelefonos = Replace(listaTelefonos, ")", String.Empty)
        listaTelefonos = Replace(listaTelefonos, " ", String.Empty)
        listaTelefonos = Replace(listaTelefonos, "-", String.Empty)
        telefonos = listaTelefonos?.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries)
    End Sub

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
