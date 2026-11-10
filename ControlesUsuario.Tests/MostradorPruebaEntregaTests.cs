using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Prueba de entrega por agencia (10/11/26): «Ver prueba de entrega», el mismo paso en Agencias › Tramitados y en el detalle del
    /// pedido, contra GET api/EnviosAgencias/{id}/PruebaEntrega (la API va con fakes).
    /// </summary>
    [TestClass]
    public class MostradorPruebaEntregaTests
    {
        [TestMethod]
        public void Contrato_RutaYMismosNombresQueElServidor()
        {
            Assert.AreEqual("EnviosAgencias/251500/PruebaEntrega", ServicioPruebaEntregaEnvios.Ruta(251500));
            CollectionAssert.AreEqual(new[] { "Agencia", "CobroReembolso", "Disponible", "DniReceptor", "Documento", "FechaHora", "ImporteCobrado",
                    "Mensaje", "NombreAgencia", "Numero", "Pedido", "Receptor", "TipoDocumento", "UrlDocumento", "Usuario" },
                typeof(PruebaEntregaEnvio).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        [TestMethod]
        public void DeRespuesta_LeeElDocumentoEnBase64()
        {
            PruebaEntregaEnvio prueba = ServicioPruebaEntregaEnvios.DeRespuesta(
                "{\"Numero\":251500,\"Agencia\":13,\"NombreAgencia\":\"CTT\",\"Disponible\":true,\"FechaHora\":\"2026-11-10T09:27:35\"," +
                "\"Receptor\":\"Receptor\",\"Documento\":\"JVBERg==\",\"TipoDocumento\":\"application/pdf\"}");

            Assert.IsTrue(prueba.Disponible);
            Assert.AreEqual(13, prueba.Agencia);
            CollectionAssert.AreEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 }, prueba.Documento);
            Assert.AreEqual(new DateTime(2026, 11, 10, 9, 27, 35), prueba.FechaHora);
            Assert.IsNull(ServicioPruebaEntregaEnvios.DeRespuesta(" "));
        }

        [TestMethod]
        public async Task NoDisponible_EnsenaElMotivoDeLaApi()
        {
            var dialogos = A.Fake<IServicioDialogos>();
            var mostrador = new MostradorPruebaEntrega(n => Task.FromResult(new PruebaEntregaEnvio { Numero = n, Disponible = false, Mensaje = "Esta agencia no da prueba de entrega." }), dialogos);

            await mostrador.Mostrar(251500, "930001");

            A.CallTo(() => dialogos.ShowNotification(MostradorPruebaEntrega.TITULO, "Esta agencia no da prueba de entrega.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Disponible_AbreElDialogoConElDocumento()
        {
            var dialogos = A.Fake<IServicioDialogos>();
            ParametrosDialogo parametros = null;
            A.CallTo(() => dialogos.ShowDialog(PruebaEntregaDialogViewModel.NOMBRE, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string n, ParametrosDialogo p, Action<ResultadoDialogo> cb) => parametros = p);
            var pdf = new byte[] { 0x25, 0x50 };
            var ocupado = new List<bool>();
            var mostrador = new MostradorPruebaEntrega(n => Task.FromResult(new PruebaEntregaEnvio
            {
                Numero = n, Agencia = 13, NombreAgencia = "CTT", Pedido = 930001, Disponible = true, Receptor = "Receptor",
                FechaHora = new DateTime(2026, 11, 10, 9, 27, 0), Documento = pdf, TipoDocumento = "application/pdf"
            }), dialogos, ocupado: ocupado.Add);

            await mostrador.Mostrar(251501, null);

            Assert.IsNotNull(parametros);
            Assert.AreEqual(251501, parametros.GetValue<int>(PruebaEntregaDialogViewModel.PARAMETRO_ENVIO));
            Assert.AreEqual("930001", parametros.GetValue<string>(PruebaEntregaDialogViewModel.PARAMETRO_PEDIDO), "Sin pedido, el del envío");
            Assert.AreEqual("CTT", parametros.GetValue<string>(PruebaEntregaDialogViewModel.PARAMETRO_AGENCIA));
            Assert.AreSame(pdf, parametros.GetValue<byte[]>(PruebaEntregaDialogViewModel.PARAMETRO_DOCUMENTO));
            Assert.IsNull(parametros.GetValue<byte[]>(PruebaEntregaDialogViewModel.PARAMETRO_FIRMA), "Un PDF no se pinta como imagen");
            CollectionAssert.AreEqual(new[] { true, false }, ocupado, "Ocupado solo mientras se pide a la API");
        }

        [TestMethod]
        public async Task FallaLaApi_ErrorDeUstedYElmah()
        {
            var dialogos = A.Fake<IServicioDialogos>();
            var errores = new List<Exception>();
            var ocupado = new List<bool>();
            var mostrador = new MostradorPruebaEntrega(n => throw new InvalidOperationException("Su usuario no tiene permiso"), dialogos, errores.Add, ocupado.Add);

            await mostrador.Mostrar(251500, "930001");

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("Su usuario no tiene permiso"))).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, errores.Count);
            CollectionAssert.AreEqual(new[] { true, false }, ocupado);
        }
    }
}
