using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Xceed.Wpf.Toolkit;

namespace PlantillaVentaTests
{
    /// <summary>
    /// ELMAH 24/09/26 (Nesto 1.10.30.0): en la plantilla, teclear en «Fecha de entrega» una fecha anterior a
    /// fechaMinimaEntrega tumbaba Nesto: Xceed DateTimeUpDown.ConvertTextToValue → ValidateDefaultMinMax lanza
    /// ArgumentOutOfRangeException desde OnPreviewKeyDown (fuera de su try/catch). Con ClipValueToMinMax="True"
    /// (PlantillaVentaView.xaml) ConvertTextToValue recorta a la mínima en vez de lanzar. Estos tests fijan ese
    /// contrato de Xceed por si se actualiza el paquete.
    /// </summary>
    [TestClass]
    public class FechaEntregaMinimaTests
    {
        private static readonly DateTime MINIMA = new DateTime(2026, 9, 25);

        private static object ConvertirTexto(bool recortar, string texto)
        {
            object resultado = null;
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    var selector = new DateTimePicker
                    {
                        Format = DateTimeFormat.ShortDate,
                        CultureInfo = new CultureInfo("es-ES"),
                        Minimum = MINIMA,
                        ClipValueToMinMax = recortar
                    };
                    MethodInfo convertir = typeof(DateTimeUpDown).GetMethod("ConvertTextToValue", BindingFlags.Instance | BindingFlags.NonPublic);
                    resultado = convertir.Invoke(selector, new object[] { texto });
                }
                catch (TargetInvocationException ex)
                {
                    error = ex.InnerException;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw error;
            }
            return resultado;
        }

        [TestMethod]
        public void SinClipValueToMinMax_UnaFechaAnteriorALaMinimaLanza_ElFalloDeELMAH()
        {
            _ = Assert.ThrowsException<ArgumentOutOfRangeException>(() => ConvertirTexto(false, "20/09/2026"));
        }

        [TestMethod]
        public void ConClipValueToMinMax_UnaFechaAnteriorALaMinimaSeAjustaALaMinima()
        {
            Assert.AreEqual(MINIMA, ConvertirTexto(true, "20/09/2026"));
        }

        [TestMethod]
        public void ConClipValueToMinMax_UnaFechaValidaSeRespeta()
        {
            // Xceed completa la hora con la actual (Format=ShortDate no la muestra): solo cuenta el día.
            Assert.AreEqual(new DateTime(2026, 9, 30), ((DateTime)ConvertirTexto(true, "30/09/2026")).Date);
        }
    }
}
