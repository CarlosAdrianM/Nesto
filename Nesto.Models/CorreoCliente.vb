Imports Nesto.Models.Nesto.Models

Public Class CorreoCliente
    Const CARGO_AGENCIA = 26
    Const CARGO_FACTURA_ELECTRONICA = 22

    Dim listaPersonas As List(Of PersonasContactoCliente)
    Public Sub New(listaPersonas As ICollection(Of PersonasContactoCliente))
        Me.listaPersonas = listaPersonas.ToList()
    End Sub

    Public Function CorreoAgencia() As String
        Dim correo As String
        Dim personaAgencia As PersonasContactoCliente

        If Not listaPersonas.Any Then
            Return String.Empty
        End If
        personaAgencia = (From c In listaPersonas Where c.Cargo = CARGO_AGENCIA AndAlso Not String.IsNullOrWhiteSpace(c.CorreoElectrónico)).FirstOrDefault
        If Not IsNothing(personaAgencia) AndAlso Not IsNothing(personaAgencia.CorreoElectrónico) Then
            correo = personaAgencia.CorreoElectrónico.Trim
            If Not String.IsNullOrWhiteSpace(correo) Then
                Return correo
            End If
        End If

        personaAgencia = (From c In listaPersonas Where Not String.IsNullOrWhiteSpace(c.CorreoElectrónico)).FirstOrDefault
        If Not IsNothing(personaAgencia) AndAlso Not IsNothing(personaAgencia.CorreoElectrónico) Then
            correo = personaAgencia.CorreoElectrónico.Trim
            If Not String.IsNullOrWhiteSpace(correo) Then
                Return correo
            End If
        End If

        If IsNothing(listaPersonas.FirstOrDefault.CorreoElectrónico) Then
            Return String.Empty
        Else
            Return listaPersonas.FirstOrDefault.CorreoElectrónico.Trim
        End If
    End Function

    Public Function CorreoUnicoFacturaElectronica() As String
        Dim correo As String
        Dim personaAgencia As PersonasContactoCliente

        If Not listaPersonas.Any Then
            Return String.Empty
        End If
        personaAgencia = (From c In listaPersonas Where c.Cargo = CARGO_FACTURA_ELECTRONICA AndAlso Not String.IsNullOrWhiteSpace(c.CorreoElectrónico)).FirstOrDefault
        If Not IsNothing(personaAgencia) AndAlso Not IsNothing(personaAgencia.CorreoElectrónico) Then
            correo = personaAgencia.CorreoElectrónico.Trim
            If Not String.IsNullOrWhiteSpace(correo) Then
                Return correo
            End If
        End If
    End Function
End Class
