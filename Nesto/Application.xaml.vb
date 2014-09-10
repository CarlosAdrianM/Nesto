Imports System.Windows.Markup
Imports System.Globalization

Class Application
    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.
    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        FrameworkElement.LanguageProperty.OverrideMetadata(GetType(FrameworkElement), New FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)))
        MyBase.OnStartup(e)
    End Sub

End Class
