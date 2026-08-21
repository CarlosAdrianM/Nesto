Imports Nesto.Models.Nesto.Models

Public Class CorreoCliente
    Const CARGO_AGENCIA = 26
    Const CARGO_FACTURA_ELECTRONICA = 22

    Dim listaPersonas As List(Of PersonaContactoCorreo)

    Public Sub New(listaPersonas As ICollection(Of PersonasContactoCliente))
        Me.listaPersonas = listaPersonas.
            Select(Function(p) New PersonaContactoCorreo(p.Cargo, p.CorreoElectrónico)).ToList()
    End Sub

    ''' <summary>
    ''' Nesto#340 (slice A3): mismo criterio de elección para quien ya no trae entidades de
    ''' Entity Framework. De una persona de contacto aquí solo importan el cargo y el correo,
    ''' así que el llamante normaliza a PersonaContactoCorreo y el criterio de elección sigue
    ''' viviendo en un único sitio: este.
    ''' </summary>
    Public Sub New(listaPersonas As IEnumerable(Of PersonaContactoCorreo))
        Me.listaPersonas = If(listaPersonas Is Nothing, New List(Of PersonaContactoCorreo), listaPersonas.ToList())
    End Sub

    Public Function CorreoAgencia() As String
        Dim correo As String
        Dim personaAgencia As PersonaContactoCorreo

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
        Dim personaAgencia As PersonaContactoCorreo

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

''' <summary>
''' Nesto#340 (slice A3): lo único que hace falta de una persona de contacto para elegir un
''' correo. Permite que CorreoCliente sirva tanto a las entidades de EF como a los modelos sin
''' EF, sin duplicar el criterio de elección.
''' </summary>
Public Class PersonaContactoCorreo
    Public Sub New(cargo As Short, correoElectronico As String)
        Me.Cargo = cargo
        Me.CorreoElectrónico = correoElectronico
    End Sub
    Public ReadOnly Property Cargo As Short
    Public ReadOnly Property CorreoElectrónico As String
End Class
