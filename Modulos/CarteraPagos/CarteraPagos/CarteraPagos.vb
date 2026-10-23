Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts

Public Class CarteraPagos
    Implements IModuloNesto, ICarteraPagos

    Public Sub Registrar(registro As IRegistroNesto) Implements IModuloNesto.Registrar
        registro.Vista(Of CarteraPagosView)("CarteraPagosView")

        ' Nesto#490 (4C.5): lo que Unity construía sin registrar
        registro.Transitorio(Of CarteraPagosMenuBar)()
        registro.Transitorio(Of CarteraPagosViewModel)()
    End Sub

    Public Sub Inicializar(resolutor As IResolutorNesto) Implements IModuloNesto.Inicializar
        Dim view = resolutor.Resolver(Of CarteraPagosMenuBar)()
        If Not IsNothing(view) Then
            resolutor.Resolver(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
