using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PedidoVenta;
using System;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#640 (NestoAPI#864): lo que enseña Nesto del pago a plazos de un pedido. La financiera es un dato de la API
    /// (NombreProveedor): en los tests se llama «Financiera Prueba» a propósito.
    /// </summary>
    [TestClass]
    public class ReglasPagoAPlazosTests
    {
        private const string PROVEEDOR = "Financiera Prueba";

        private static DisponibilidadPagoAPlazosModel Disponibilidad(bool disponible = true, string motivo = null, bool tieneApp = true,
            string canal = "App", string nombre = PROVEEDOR, string codigo = "Prueba") => new DisponibilidadPagoAPlazosModel
            {
                Disponible = disponible,
                Motivo = motivo,
                Proveedor = codigo,
                NombreProveedor = nombre,
                Importe = 1234.5m,
                ClienteTieneApp = tieneApp,
                Canal = canal
            };

        private static EstadoPagoAPlazosModel Estado(string estado, DateTime? limite = null) => new EstadoPagoAPlazosModel
        {
            Empresa = "1",
            Pedido = 930500,
            HaySolicitud = estado != "SinSolicitud",
            Estado = estado,
            NombreProveedor = PROVEEDOR,
            FechaLimiteCaptura = limite
        };

        #region La acción

        [TestMethod]
        public void SeOfrece_SinRespuesta_No()
        {
            Assert.IsFalse(ReglasPagoAPlazos.SeOfrece(null));
            Assert.IsFalse(ReglasPagoAPlazos.PuedePedirse(null));
            Assert.AreEqual(string.Empty, ReglasPagoAPlazos.TextoAyuda(null));
        }

        [TestMethod]
        public void SeOfrece_InterruptorApagado_VieneSinProveedor_NoSePinta()
        {
            var apagado = Disponibilidad(disponible: false, motivo: "El pago a plazos no está disponible.", nombre: null, codigo: null);

            Assert.IsFalse(ReglasPagoAPlazos.SeOfrece(apagado));
            Assert.IsFalse(ReglasPagoAPlazos.PuedePedirse(apagado));
            Assert.AreEqual(string.Empty, ReglasPagoAPlazos.TextoAyuda(apagado));
        }

        [TestMethod]
        public void SeOfrece_Disponible_SePintaYSePuedePedir()
        {
            var disponibilidad = Disponibilidad();

            Assert.IsTrue(ReglasPagoAPlazos.SeOfrece(disponibilidad));
            Assert.IsTrue(ReglasPagoAPlazos.PuedePedirse(disponibilidad));
            Assert.AreEqual("Financiar 1.234,50 € con Financiera Prueba: el cliente termina el pago en su app o con un enlace.",
                ReglasPagoAPlazos.TextoAyuda(disponibilidad));
        }

        [TestMethod]
        public void SeOfrece_NoDisponibleConProveedor_SePintaDeshabilitadaConElMotivoDeLaApi()
        {
            var disponibilidad = Disponibilidad(disponible: false, motivo: "El pedido ya está pagado.");

            Assert.IsTrue(ReglasPagoAPlazos.SeOfrece(disponibilidad));
            Assert.IsFalse(ReglasPagoAPlazos.PuedePedirse(disponibilidad));
            Assert.AreEqual("El pedido ya está pagado.", ReglasPagoAPlazos.TextoAyuda(disponibilidad));
        }

        [TestMethod]
        public void TextoAyuda_NoDisponibleSinMotivo_TextoGenerico()
        {
            Assert.AreEqual("Este pedido no se puede pagar a plazos.", ReglasPagoAPlazos.TextoAyuda(Disponibilidad(disponible: false)));
        }

        [TestMethod]
        public void CanalInicial_ConAppYSugerida_App()
        {
            Assert.AreEqual("App", ReglasPagoAPlazos.CanalInicial(Disponibilidad(tieneApp: true, canal: "App")));
        }

        [TestMethod]
        public void CanalInicial_SinApp_EnlaceAunqueLaApiDijeraApp()
        {
            Assert.AreEqual("Enlace", ReglasPagoAPlazos.CanalInicial(Disponibilidad(tieneApp: false, canal: "App")));
            Assert.AreEqual("Enlace", ReglasPagoAPlazos.CanalInicial(Disponibilidad(tieneApp: true, canal: "Enlace")));
            Assert.AreEqual("Enlace", ReglasPagoAPlazos.CanalInicial(null));
        }

        #endregion

        #region El resultado de pedirlo

        [TestMethod]
        public void TextoResultado_PorApp_DiceACuantosDispositivos()
        {
            Assert.AreEqual("Aviso enviado a la app del cliente (2 dispositivos): el cliente termina el pago ahí.",
                ReglasPagoAPlazos.TextoResultado(new SolicitudPagoAPlazosModel { Canal = "App", DispositivosAvisados = 2 }));
            Assert.AreEqual("Aviso enviado a la app del cliente (1 dispositivo): el cliente termina el pago ahí.",
                ReglasPagoAPlazos.TextoResultado(new SolicitudPagoAPlazosModel { Canal = "App", DispositivosAvisados = 1 }));
        }

        [TestMethod]
        public void TextoResultado_PorAppSinLlegarANadie_PideMandarElEnlace()
        {
            Assert.AreEqual("El aviso no ha llegado a ningún dispositivo del cliente. Copie el enlace y mándeselo usted.",
                ReglasPagoAPlazos.TextoResultado(new SolicitudPagoAPlazosModel { Canal = "App", DispositivosAvisados = 0, UrlPago = "https://pago/1" }));
            Assert.AreEqual("El aviso no ha llegado a ningún dispositivo del cliente.",
                ReglasPagoAPlazos.TextoResultado(new SolicitudPagoAPlazosModel { Canal = "App", DispositivosAvisados = 0 }));
        }

        [TestMethod]
        public void TextoResultado_PorEnlace_PideCopiarlo()
        {
            Assert.AreEqual("Solicitud creada. Copie el enlace y mándeselo al cliente: el pago no empieza hasta que lo abra y lo termine.",
                ReglasPagoAPlazos.TextoResultado(new SolicitudPagoAPlazosModel { Canal = "Enlace", UrlPago = "https://pago/1" }));
            Assert.AreEqual(string.Empty, ReglasPagoAPlazos.TextoResultado(null));
        }

        #endregion

        #region El estado

        [TestMethod]
        public void TextoEstado_SinSolicitud_Vacio()
        {
            Assert.AreEqual(string.Empty, ReglasPagoAPlazos.TextoEstado(null));
            Assert.AreEqual(string.Empty, ReglasPagoAPlazos.TextoEstado(Estado("SinSolicitud")));
            Assert.IsFalse(ReglasPagoAPlazos.HaySolicitud(Estado("SinSolicitud")));
            Assert.IsFalse(ReglasPagoAPlazos.HaySolicitud(new EstadoPagoAPlazosModel { HaySolicitud = false, Estado = "Pendiente" }));
        }

        [DataTestMethod]
        [DataRow("Pendiente", "Pago a plazos (Financiera Prueba): pendiente del cliente")]
        [DataRow("Aprobado", "Pago a plazos (Financiera Prueba): aprobado")]
        [DataRow("Capturado", "Pago a plazos (Financiera Prueba): capturado")]
        [DataRow("Rechazado", "Pago a plazos (Financiera Prueba): rechazado")]
        [DataRow("Caducado", "Pago a plazos (Financiera Prueba): caducado")]
        [DataRow("Cancelado", "Pago a plazos (Financiera Prueba): cancelado")]
        [DataRow("pendiente ", "Pago a plazos (Financiera Prueba): pendiente del cliente")]
        [DataRow("EstadoNuevo", "Pago a plazos (Financiera Prueba): estadonuevo")]
        public void TextoEstado_CadaEstado(string estado, string esperado)
        {
            Assert.AreEqual(esperado, ReglasPagoAPlazos.TextoEstado(Estado(estado)));
        }

        [TestMethod]
        public void TextoEstado_AprobadoConLimite_DiceAntesDeCuando()
        {
            Assert.AreEqual("Pago a plazos (Financiera Prueba): aprobado, a capturar antes del 20/11",
                ReglasPagoAPlazos.TextoEstado(Estado("Aprobado", new DateTime(2026, 11, 20))));
        }

        [TestMethod]
        public void TextoEstado_SinNombreDeProveedor_SoloElTitulo()
        {
            var estado = Estado("Pendiente");
            estado.NombreProveedor = null;

            Assert.AreEqual("Pago a plazos: pendiente del cliente", ReglasPagoAPlazos.TextoEstado(estado));
        }

        [DataTestMethod]
        [DataRow("Pendiente", true)]
        [DataRow("Aprobado", true)]
        [DataRow("Capturado", false)]
        [DataRow("Rechazado", false)]
        [DataRow("Caducado", false)]
        [DataRow("Cancelado", false)]
        [DataRow("SinSolicitud", false)]
        public void EsViva_SoloPendienteYAprobado(string estado, bool esperado)
        {
            Assert.AreEqual(esperado, ReglasPagoAPlazos.EsViva(Estado(estado)));
        }

        [TestMethod]
        public void EsViva_SinEstado_No()
        {
            Assert.IsFalse(ReglasPagoAPlazos.EsViva(null));
        }

        [TestMethod]
        public void TextoDetalleEstado_Aprobado_ImporteCanalFechasYPlazo()
        {
            var estado = Estado("Aprobado", new DateTime(2026, 11, 20));
            estado.Importe = 250m;
            estado.Canal = "App";
            estado.Usuario = "Lidia";
            estado.FechaSolicitud = new DateTime(2026, 11, 13, 10, 5, 0);
            estado.FechaAprobacion = new DateTime(2026, 11, 13, 11, 30, 0);
            estado.DiasParaCapturar = 7;

            string texto = ReglasPagoAPlazos.TextoDetalleEstado(estado);

            Assert.AreEqual(string.Join("\r\n",
                "Importe: 250,00 €",
                "Canal: la app del cliente",
                "Pedido el 13/11/2026 10:05 por Lidia",
                "Aprobado el 13/11/2026 11:30",
                "El pedido tiene que salir antes del 20/11/2026 (quedan 7 días)"), texto);
        }

        [TestMethod]
        public void TextoDetalleEstado_Cancelado_LlevaElMotivo()
        {
            var estado = Estado("Cancelado");
            estado.Canal = "Enlace";
            estado.FechaCierre = new DateTime(2026, 11, 13, 12, 0, 0);
            estado.Motivo = "Cancelado por Lidia: el cliente prefiere tarjeta";

            string texto = ReglasPagoAPlazos.TextoDetalleEstado(estado);

            Assert.AreEqual(string.Join("\r\n",
                "Canal: enlace",
                "Cerrado el 13/11/2026 12:00",
                "Cancelado por Lidia: el cliente prefiere tarjeta"), texto);
        }

        [TestMethod]
        public void TextoDetalleEstado_SinSolicitud_Vacio()
        {
            Assert.AreEqual(string.Empty, ReglasPagoAPlazos.TextoDetalleEstado(Estado("SinSolicitud")));
        }

        #endregion

        #region Cancelar

        [TestMethod]
        public void TextoPedirMotivoCancelacion_Pendiente_ElClienteYaNoPodraTerminar()
        {
            string texto = ReglasPagoAPlazos.TextoPedirMotivoCancelacion(Estado("Pendiente"));

            StringAssert.StartsWith(texto, "El cliente ya no podrá terminar el pago con el aviso o el enlace que tiene.");
            StringAssert.EndsWith(texto, "Indique el motivo de la cancelación:");
        }

        [TestMethod]
        public void TextoPedirMotivoCancelacion_Aprobado_AvisaDelPrepago()
        {
            string texto = ReglasPagoAPlazos.TextoPedirMotivoCancelacion(Estado("Aprobado"));

            StringAssert.StartsWith(texto, "El pago ya está aprobado: se cancelará en Financiera Prueba y se quitará el prepago del pedido.");
        }

        #endregion

        [TestMethod]
        public void Textos_NingunoNombraAUnaFinancieraConcreta()
        {
            // El nombre lo da la API (NombreProveedor): si falta, «la financiera».
            Assert.AreEqual("la financiera", ReglasPagoAPlazos.NombreProveedor(null));
            Assert.AreEqual("Financiera Prueba", ReglasPagoAPlazos.NombreProveedor(" Financiera Prueba "));
            string sinNombre = ReglasPagoAPlazos.TextoPedirMotivoCancelacion(new EstadoPagoAPlazosModel { HaySolicitud = true, Estado = "Aprobado" });
            StringAssert.Contains(sinNombre, "se cancelará en la financiera");
        }
    }
}
