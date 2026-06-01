Imports Nesto.Infrastructure.Models.Alquileres

Public Interface IProductosAlquilerService
    Function LeerProductosAlquiler() As Task(Of List(Of ProductoAlquilerModel))
End Interface
