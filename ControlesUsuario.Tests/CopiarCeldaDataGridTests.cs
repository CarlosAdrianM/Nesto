using ControlesUsuario.Behaviors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#609 (sugerencia 970): en las rejillas de Agencias Ctrl+C copia solo la celda en la que se ha pinchado
    /// (el nº de envío), no la fila entera.
    /// </summary>
    [TestClass]
    public class CopiarCeldaDataGridTests
    {
        [TestMethod]
        public void DejarSoloLaColumna_ConLaColumnaDelNumeroDeEnvio_SoloQuedaEseValor()
        {
            EjecutarEnSTA(() =>
            {
                var nombre = new DataGridTextColumn { Header = "Nombre", Binding = new Binding("Nombre") };
                var numeroEnvio = new DataGridTextColumn { Header = "Nº Envío", Binding = new Binding("CodigoBarras") };
                var fila = new List<DataGridClipboardCellContent>
                {
                    new DataGridClipboardCellContent(null, nombre, "CLIENTE DE PRUEBA"),
                    new DataGridClipboardCellContent(null, numeroEnvio, "61197140249481")
                };

                bool filtrado = CopiarCeldaDataGrid.DejarSoloLaColumna(fila, numeroEnvio);

                Assert.IsTrue(filtrado);
                Assert.AreEqual(1, fila.Count);
                Assert.AreEqual("61197140249481", fila[0].Content);
            });
        }

        [TestMethod]
        public void DejarSoloLaColumna_SinCeldaActual_CopiaLaFilaEntera()
        {
            EjecutarEnSTA(() =>
            {
                var nombre = new DataGridTextColumn { Header = "Nombre", Binding = new Binding("Nombre") };
                var fila = new List<DataGridClipboardCellContent> { new DataGridClipboardCellContent(null, nombre, "CLIENTE") };

                Assert.IsFalse(CopiarCeldaDataGrid.DejarSoloLaColumna(fila, null));
                Assert.AreEqual(1, fila.Count);
            });
        }

        [TestMethod]
        public void DejarSoloLaColumna_ColumnaDeBotones_CopiaLaFilaEntera()
        {
            EjecutarEnSTA(() =>
            {
                var nombre = new DataGridTextColumn { Header = "Nombre", Binding = new Binding("Nombre") };
                var botones = new DataGridTemplateColumn();
                var fila = new List<DataGridClipboardCellContent>
                {
                    new DataGridClipboardCellContent(null, nombre, "CLIENTE"),
                    new DataGridClipboardCellContent(null, botones, null)
                };

                Assert.IsFalse(CopiarCeldaDataGrid.DejarSoloLaColumna(fila, botones), "Una columna sin valor no deja el portapapeles vacío");
                Assert.AreEqual(2, fila.Count);
            });
        }

        [TestMethod]
        public void Activo_EnUnaRejilla_SeQuedaPuesto()
        {
            EjecutarEnSTA(() =>
            {
                var rejilla = new DataGrid();
                CopiarCeldaDataGrid.SetActivo(rejilla, true);
                Assert.IsTrue(CopiarCeldaDataGrid.GetActivo(rejilla));
            });
        }

        private static void EjecutarEnSTA(Action action)
        {
            Exception capturada = null;
            var hilo = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { capturada = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (capturada != null)
            {
                throw new AssertFailedException(capturada.Message, capturada);
            }
        }
    }
}
