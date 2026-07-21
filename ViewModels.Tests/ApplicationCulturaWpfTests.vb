Imports System.Globalization
Imports System.Windows
Imports System.Windows.Documents
Imports System.Windows.Markup

''' <summary>
''' Regresión del CRASH DE ARRANQUE de la 1.10.14.1: Application.OnStartup añadió un
''' OverrideMetadata de LanguageProperty sobre GetType(FrameworkContentElement) para que los
''' Run hereden la cultura (total de Cartera viva en euros), pero WPF ya registra esa
''' propiedad con metadata propia en FrameworkContentElement y OverrideMetadata LANZA
''' InvalidOperationException ("PropertyMetadata ya está registrado") ANTES de que se
''' enganchen los manejadores de error: la app se cerraba nada más abrir, sin rastro.
''' La forma permitida es sobre el tipo DERIVADO TextElement (padre de Run/Span/Bold).
'''
''' Este test replica EXACTAMENTE la secuencia de Application.OnStartup (duplicada a
''' propósito: el exe de Views no es referenciable desde este proyecto de tests). Si alguien
''' vuelve a tocar esa secuencia y rompe el arranque, esto se pone rojo en local en vez de
''' tirar Nesto en producción.
''' </summary>
<TestClass()>
Public Class ApplicationCulturaWpfTests

    <TestMethod()>
    Public Sub SecuenciaDeCulturaDeOnStartup_NoLanzaYLosRunHeredanLaCultura()
        ' Misma secuencia, mismo orden y mismos tipos que Application.OnStartup
        FrameworkElement.LanguageProperty.OverrideMetadata(GetType(FrameworkElement),
            New FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)))
        FrameworkContentElement.LanguageProperty.OverrideMetadata(GetType(TextElement),
            New FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)))

        ' Y el objetivo del override: un Run SIN Language explícito (el del total de Cartera
        ' viva) debe heredar la cultura del sistema, no el en-US por defecto de WPF (dólares).
        Dim run As New Run
        Assert.AreEqual(CultureInfo.CurrentCulture.IetfLanguageTag.ToLowerInvariant(),
                        run.Language.IetfLanguageTag.ToLowerInvariant(),
                        "El Run debe heredar la cultura del sistema vía el override de TextElement")
    End Sub

End Class
