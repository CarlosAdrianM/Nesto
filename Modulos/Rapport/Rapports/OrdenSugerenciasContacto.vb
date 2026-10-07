''' <summary>
''' NestoAPI#603: orden de la lista de clientes para contactar. Las pendientes van primero, por el
''' Orden que manda la API; las atendidas van al final (sin desaparecer, para que el vendedor vea
''' lo que ya ha hecho), también por su Orden. A igualdad de Orden (endpoint antiguo, todas con 0)
''' se respeta el orden en que llegaron.
''' </summary>
Public Module OrdenSugerenciasContacto
    Public Function Ordenar(sugerencias As IEnumerable(Of ClienteProbabilidadVenta)) As List(Of ClienteProbabilidadVenta)
        If sugerencias Is Nothing Then
            Return New List(Of ClienteProbabilidadVenta)
        End If
        ' OrderBy de LINQ es estable: los empates conservan el orden de llegada.
        Return sugerencias.Where(Function(s) s IsNot Nothing).
            OrderBy(Function(s) s.Atendida).
            ThenBy(Function(s) If(s.Orden > 0, s.Orden, Integer.MaxValue)).
            ToList()
    End Function
End Module
