using ControlesUsuario.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.ObjectModel;
using System.Threading;

namespace ControlesUsuario.Tests
{
    [TestClass]
    public class SelectorPlazosPagoTests
    {
        // Regresion Issue #254 (familia "el pedido ha cambiado al cargar"): el pedido
        // 917768 tenia plazosPago='CONTADO' en BD, pero el cliente tiene cartera
        // vencida y /api/PlazosPago/ConInfoDeuda solo devuelve [CR, PRE]. El selector
        // no encontraba 'CONTADO' en la lista y auto-seleccionaba 'CR' (Contado
        // Riguroso, tambien numeroPlazos=1/dias=0/meses=0), lo que via TwoWay
        // pisaba pedido.Model.plazosPago y disparaba el falso "el pedido ha
        // cambiado" al facturar.
        //
        // Caso analogo al fix de SelectorCCC (commit 92a2c06): el selector NO debe
        // mutar el modelo durante la carga si el valor ya no es valido; debe limitarse
        // a avisar al usuario con MensajePlazoNoPermitido.
        [TestMethod]
        [TestCategory("SelectorPlazosPago")]
        [TestCategory("Issue254")]
        public void SelectorPlazosPago_PlazoNoEstaEnLista_NoDebeMutarSeleccionada()
        {
            string seleccionadaFinal = "valor-no-inicializado";
            string mensajeAviso = null;
            bool plazoNoPermitido = false;

            Thread thread = new Thread(() =>
            {
                var sut = new SelectorPlazosPago
                {
                    Seleccionada = "CONTADO",
                    listaPlazosPago = new ObservableCollection<PlazosPago>
                    {
                        new PlazosPago { plazoPago = "CR",  descripcion = "CONTADO RIGUROSO", numeroPlazos = 1, diasPrimerPlazo = 0, mesesPrimerPlazo = 0 },
                        new PlazosPago { plazoPago = "PRE", descripcion = "Prepago",          numeroPlazos = 1, diasPrimerPlazo = 0, mesesPrimerPlazo = 0 }
                    }
                };

                sut.ValidarYAjustarPlazoSeleccionado();

                seleccionadaFinal = sut.Seleccionada;
                mensajeAviso = sut.MensajePlazoNoPermitido;
                plazoNoPermitido = sut.PlazoNoPermitido;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.AreEqual("CONTADO", seleccionadaFinal,
                "El selector NO debe mutar Seleccionada cuando el plazo del pedido no esta " +
                "en la lista devuelta por la API. Mutar dispara un falso 'el pedido ha cambiado' " +
                "porque el snapshot ya se tomo con 'CONTADO' (Issue #254).");
            Assert.IsFalse(string.IsNullOrEmpty(mensajeAviso),
                "Debe avisar al usuario con MensajePlazoNoPermitido cuando el plazo " +
                "guardado ya no esta permitido, en lugar de cambiarlo silenciosamente.");
            Assert.IsTrue(plazoNoPermitido,
                "PlazoNoPermitido debe ser True para que el ViewModel pueda resaltar la pestana Pago " +
                "y pedir confirmacion al facturar.");
        }

        // Caso "feliz": cuando el plazo SI esta en la lista, el selector lo selecciona
        // (sin mutar el string, claro). Esto protege contra una sobre-correccion del
        // fix anterior que dejara la deteccion de plazos validos rota.
        [TestMethod]
        [TestCategory("SelectorPlazosPago")]
        [TestCategory("Issue254")]
        public void SelectorPlazosPago_PlazoExisteEnLista_LoSeleccionaSinAviso()
        {
            string seleccionadaFinal = null;
            string mensajeAviso = "valor-no-inicializado";
            PlazosPago plazoCompleto = null;
            bool plazoNoPermitido = true;

            Thread thread = new Thread(() =>
            {
                var sut = new SelectorPlazosPago
                {
                    Seleccionada = "CONTADO",
                    listaPlazosPago = new ObservableCollection<PlazosPago>
                    {
                        new PlazosPago { plazoPago = "CONTADO", descripcion = "Contado",          numeroPlazos = 1, diasPrimerPlazo = 0, mesesPrimerPlazo = 0 },
                        new PlazosPago { plazoPago = "CR",      descripcion = "CONTADO RIGUROSO", numeroPlazos = 1, diasPrimerPlazo = 0, mesesPrimerPlazo = 0 }
                    }
                };

                sut.ValidarYAjustarPlazoSeleccionado();

                seleccionadaFinal = sut.Seleccionada;
                mensajeAviso = sut.MensajePlazoNoPermitido;
                plazoCompleto = sut.PlazoPagoCompleto;
                plazoNoPermitido = sut.PlazoNoPermitido;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.AreEqual("CONTADO", seleccionadaFinal, "Si el plazo existe en la lista, Seleccionada se mantiene.");
            Assert.IsTrue(string.IsNullOrEmpty(mensajeAviso), "Si el plazo existe en la lista, no hay aviso de plazo no permitido.");
            Assert.IsNotNull(plazoCompleto, "Si el plazo existe en la lista, PlazoPagoCompleto debe quedar establecido.");
            Assert.AreEqual("CONTADO", plazoCompleto.plazoPago);
            Assert.IsFalse(plazoNoPermitido, "Si el plazo existe en la lista, PlazoNoPermitido debe ser False.");
        }
    }
}
