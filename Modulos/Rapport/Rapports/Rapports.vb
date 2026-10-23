Imports ControlesUsuario.Dialogs
Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts

Public Class Rapports
    Implements IModuloNesto, IRapports

    Public Sub Registrar(registro As IRegistroNesto) Implements IModuloNesto.Registrar
        registro.Vista(Of ListaRapportsView)("ListaRapportsView")
        registro.Vista(Of RapportView)("RapportView")
        registro.RegistrarDialogo(Of CopiarSeguimientosView, CopiarSeguimientosViewModel)

        ' Nesto#490 (4C.5): lo que Unity construía sin registrar
        registro.Transitorio(Of RapportsMenuBar)()
        registro.Transitorio(Of ListaRapportsViewModel)()
        ' Los ViewModels de sus vistas
        registro.Transitorio(Of RapportViewModel)()
    End Sub

    Public Sub Inicializar(resolutor As IResolutorNesto) Implements IModuloNesto.Inicializar
        Dim view = resolutor.Resolver(Of RapportsMenuBar)()
        If Not IsNothing(view) Then
            resolutor.Resolver(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
