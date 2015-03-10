Imports System.Windows
Imports Microsoft.Practices.Prism.Modularity
Imports Microsoft.Practices.Unity
Imports Microsoft.Practices.Prism.UnityExtensions

Public Class Bootstrapper
    Inherits UnityBootstrapper

    Protected Overrides Function CreateShell() As DependencyObject
        Return Me.Container.Resolve(Of MainWindow)()
    End Function

    Protected Overrides Sub InitializeShell()
        MyBase.InitializeShell()
        Application.Current.MainWindow = CType(Me.Shell, Window)
        Application.Current.MainWindow.Show()
    End Sub

    Protected Overrides Sub ConfigureModuleCatalog()
        MyBase.ConfigureModuleCatalog()
        Dim moduleCatalog As ModuleCatalog = CType(Me.ModuleCatalog, ModuleCatalog)
        ' Carlos: aquí se añadirían los módulos
        'moduleCatalog.AddModule(GetType(HelloWorldModule.HelloWorldModule))
    End Sub

End Class
