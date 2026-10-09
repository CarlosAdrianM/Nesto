''' <summary>
''' Nesto#521: un vendedor del desplegable de «clientes para contactar» (GET api/Vendedores/VisiblesEnSugerencias).
''' </summary>
Public Class VendedorSugerencias
    Public Property Vendedor As String
    Public Property Nombre As String

    Public ReadOnly Property Descripcion As String
        Get
            If String.IsNullOrWhiteSpace(Nombre) OrElse String.Equals(Nombre?.Trim(), Vendedor?.Trim(), StringComparison.OrdinalIgnoreCase) Then
                Return Vendedor?.Trim()
            End If
            Return $"{Nombre.Trim()} ({Vendedor?.Trim()})"
        End Get
    End Property
End Class
