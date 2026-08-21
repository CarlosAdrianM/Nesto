Imports System.Threading
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.ViewModels
Imports Prism.Services.Dialogs
Imports Unity
Imports Unity.Resolution

''' <summary>
''' ELMAH 21/08/26 (MariaJose): "A Task's exception(s) were not observed... (Object reference not
''' set to an instance of an object)" con stack en ComisionesViewModel.CalcularComisionAsync.
'''
''' Dos fallos encadenados: sin vendedor seleccionado se desreferenciaba vendedorActual, y los dos
''' checkbox llamaban al metodo async SIN Await, asi que la excepcion no la observaba nadie: el
''' usuario no veia nada y a ELMAH llegaba un mensaje sin pista de donde salia.
''' </summary>
<TestClass()>
Public Class ComisionesViewModelTests

    ''' <summary>
    ''' El ViewModel crea un DependencyObject en el constructor (DesignerProperties), y WPF exige
    ''' hilo STA. Mismo patron que los tests de ControlesUsuario.
    ''' </summary>
    Private Shared Sub EnHiloSta(prueba As Action)
        Dim fallo As Exception = Nothing
        Dim hilo As New Thread(Sub()
                                   Try
                                       prueba()
                                   Catch ex As Exception
                                       fallo = ex
                                   End Try
                               End Sub)
        hilo.SetApartmentState(ApartmentState.STA)
        hilo.Start()
        hilo.Join()
        If fallo IsNot Nothing Then
            Throw New Exception("La prueba fallo en el hilo STA: " & fallo.Message, fallo)
        End If
    End Sub

    Private Shared Function CrearViewModel() As ComisionesViewModel
        Dim container = A.Fake(Of IUnityContainer)()
        Dim configuracion = A.Fake(Of IConfiguracion)()
        Dim dialogService = A.Fake(Of IDialogService)()

        A.CallTo(Function() container.Resolve(GetType(IServicioAutenticacion), A(Of String).Ignored, A(Of ResolverOverride()).Ignored)).
            Returns(A.Fake(Of IServicioAutenticacion)())
        A.CallTo(Function() container.Resolve(GetType(IClienteApiFactory), A(Of String).Ignored, A(Of ResolverOverride()).Ignored)).
            Returns(A.Fake(Of IClienteApiFactory)())

        Return New ComisionesViewModel(container, configuracion, dialogService)
    End Function

    ''' <summary>
    ''' Sin vendedor (listaVendedores puede venir vacia, y vendedorActual sale de su
    ''' FirstOrDefault) marcar el checkbox reventaba con NullReferenceException.
    ''' </summary>
    <TestMethod()>
    Public Sub ComisionesViewModel_SinVendedorSeleccionado_MarcarIncluirPickingNoRevienta()
        EnHiloSta(Sub()
                      Dim vm = CrearViewModel()
                      Assert.IsNull(vm.vendedorActual, "El caso del error: nadie ha seleccionado vendedor")

                      vm.IncluirPicking = True

                      Assert.IsNull(vm.ComisionAnualResumenActual, "Sin vendedor no hay comision que calcular")
                      ' Sin la guarda, la NullReferenceException la recoge el Try de RecalcularComisionAsync
                      ' y acaba en un dialogo de error delante de la usuaria. No debe salir nada.
                      A.CallTo(Sub() vm.DialogService.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).MustNotHaveHappened()
                  End Sub)
    End Sub

    <TestMethod()>
    Public Sub ComisionesViewModel_SinVendedorSeleccionado_DesmarcarIncluirAlbaranesNoRevienta()
        EnHiloSta(Sub()
                      Dim vm = CrearViewModel()

                      vm.IncluirAlbaranes = False

                      Assert.IsNull(vm.ComisionAnualResumenActual)
                  End Sub)
    End Sub

End Class
