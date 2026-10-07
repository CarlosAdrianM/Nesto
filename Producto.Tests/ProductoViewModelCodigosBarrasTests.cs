using FakeItEasy;
using CommunityToolkit.Mvvm.Messaging;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// NestoAPI#605: pestaña «Códigos de barras» de la ficha. Varios códigos por producto; el principal
    /// sigue en la ficha. Caso real: los guantes de nitrilo, cuyo proveedor cambia el código por lote y
    /// manda en la caja de la talla M (32565) el código de la talla P (32564).
    /// </summary>
    [TestClass]
    public class ProductoViewModelCodigosBarrasTests
    {
        private const string GUANTES_M = "32565";

        private static CodigoBarrasProductoModel Codigo(int id, string codigo, bool principal = false, bool activo = true, int cantidad = 1)
        {
            return new CodigoBarrasProductoModel { Id = id, Codigo = codigo, Principal = principal, Activo = activo, Cantidad = cantidad, Origen = "Ficha" };
        }

        private static ProductoViewModel CrearViewModel(out IProductoService servicio, out IServicioDialogos dialogos, bool esDeCompras = true,
            List<CodigoBarrasProductoModel> codigos = null, bool apiSinEndpoint = false)
        {
            var fake = A.Fake<IProductoService>();
            servicio = fake;
            A.CallTo(() => fake.LeerProducto(GUANTES_M)).Returns(new ProductoModel
            {
                Producto = GUANTES_M,
                Nombre = "GUANTES NITRILO T/M",
                CodigoBarras = "8437017506379"
            });
            A.CallTo(() => fake.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            A.CallTo(() => fake.LeerCodigosBarras(GUANTES_M)).Returns(apiSinEndpoint ? null : codigos ?? new List<CodigoBarrasProductoModel>
            {
                Codigo(1, "8437017506379", principal: true)
            });
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)).Returns(esDeCompras);
            dialogos = A.Fake<IServicioDialogos>();
            var sut = new ProductoViewModel(A.Fake<IServicioNavegacion>(), configuracion, servicio,
                new WeakReferenceMessenger(), dialogos, A.Fake<IServicioAutenticacion>());
            sut.ReferenciaBuscar = GUANTES_M;
            return sut;
        }

        [TestMethod]
        public void AlCargar_SeVenLosCodigosActivosConElPrincipalPrimero()
        {
            var sut = CrearViewModel(out _, out _, codigos: new List<CodigoBarrasProductoModel>
            {
                Codigo(2, "8437017506362"),
                Codigo(3, "1111111111111", activo: false),
                Codigo(1, "8437017506379", principal: true)
            });

            Assert.IsTrue(sut.HayCodigosBarras);
            CollectionAssert.AreEqual(new[] { "8437017506379", "8437017506362" }, sut.CodigosBarras.Select(c => c.Codigo).ToArray());
            Assert.AreEqual("8437017506379", sut.CodigoBarrasPrincipal);
        }

        [TestMethod]
        public void AlCargar_SiLaApiNoTieneElEndpoint_LaPestannaNoSeVe()
        {
            var sut = CrearViewModel(out _, out var dialogos, apiSinEndpoint: true);

            Assert.IsFalse(sut.HayCodigosBarras);
            Assert.AreEqual(0, sut.CodigosBarras.Count);
            Assert.IsFalse(sut.AnnadirCodigoBarrasCommand.CanExecute(null));
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void SinSerDeCompras_NoSePuedeTocarNada()
        {
            var sut = CrearViewModel(out _, out _, esDeCompras: false, codigos: new List<CodigoBarrasProductoModel>
            {
                Codigo(1, "8437017506379", principal: true),
                Codigo(2, "8437017506362")
            });
            sut.NuevoCodigoBarras = "8437017506386";
            sut.CodigoBarrasSeleccionado = sut.CodigosBarras[1];

            Assert.IsFalse(sut.PuedeEditarCodigosBarras);
            Assert.IsFalse(sut.AnnadirCodigoBarrasCommand.CanExecute(null));
            Assert.IsFalse(sut.HacerPrincipalCodigoBarrasCommand.CanExecute(null));
            Assert.IsFalse(sut.DarDeBajaCodigoBarrasCommand.CanExecute(null));
        }

        [TestMethod]
        public void Annadir_Con201_LlamaSinCompartirYRecargaLaLista()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);
            A.CallTo(() => servicio.AnnadirCodigoBarras(GUANTES_M, "8437017506386", 100, "PROV1", false))
                .Returns(new RespuestaAnnadirCodigoBarras { Resultado = ResultadoAnnadirCodigoBarras.Creado });
            sut.NuevoCodigoBarras = " 8437017506386 ";
            sut.NuevaCantidadCodigoBarras = 100;
            sut.NuevoProveedorCodigoBarras = "PROV1";

            sut.AnnadirCodigoBarrasCommand.Execute(null);

            A.CallTo(() => servicio.AnnadirCodigoBarras(GUANTES_M, "8437017506386", 100, "PROV1", false)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.AnnadirCodigoBarras(A<string>._, A<string>._, A<int>._, A<string>._, true)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.LeerCodigosBarras(GUANTES_M)).MustHaveHappenedTwiceExactly();
            Assert.IsNull(sut.NuevoCodigoBarras);
            Assert.AreEqual(1, sut.NuevaCantidadCodigoBarras);
        }

        [TestMethod]
        public void Annadir_Con409YConfirmacion_RepiteCompartiendoElCodigo()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);
            A.CallTo(() => servicio.AnnadirCodigoBarras(GUANTES_M, "8437017506362", 1, null, false))
                .Returns(new RespuestaAnnadirCodigoBarras
                {
                    Resultado = ResultadoAnnadirCodigoBarras.EnOtroProducto,
                    Productos = new List<ProductoDelCodigoBarrasModel> { new() { Producto = "32564", Nombre = "GUANTES NITRILO T/P" } }
                });
            A.CallTo(() => servicio.AnnadirCodigoBarras(GUANTES_M, "8437017506362", 1, null, true))
                .Returns(new RespuestaAnnadirCodigoBarras { Resultado = ResultadoAnnadirCodigoBarras.Creado });
            string pregunta = null;
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._))
                .ReturnsLazily((string _, string mensaje) => { pregunta = mensaje; return Task.FromResult(true); });
            sut.NuevoCodigoBarras = "8437017506362";

            sut.AnnadirCodigoBarrasCommand.Execute(null);

            StringAssert.Contains(pregunta, "Ese código ya es del producto 32564 GUANTES NITRILO T/P");
            StringAssert.Contains(pregunta, "¿Añadirlo también a este?");
            A.CallTo(() => servicio.AnnadirCodigoBarras(GUANTES_M, "8437017506362", 1, null, true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Annadir_Con409SinConfirmar_NoLoComparteYNoBorraLoTecleado()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);
            A.CallTo(() => servicio.AnnadirCodigoBarras(A<string>._, A<string>._, A<int>._, A<string>._, false))
                .Returns(new RespuestaAnnadirCodigoBarras
                {
                    Resultado = ResultadoAnnadirCodigoBarras.EnOtroProducto,
                    Productos = new List<ProductoDelCodigoBarrasModel> { new() { Producto = "32564", Nombre = "GUANTES NITRILO T/P" } }
                });
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            sut.NuevoCodigoBarras = "8437017506362";

            sut.AnnadirCodigoBarrasCommand.Execute(null);

            A.CallTo(() => servicio.AnnadirCodigoBarras(A<string>._, A<string>._, A<int>._, A<string>._, true)).MustNotHaveHappened();
            Assert.AreEqual("8437017506362", sut.NuevoCodigoBarras);
        }

        [TestMethod]
        public void TextoCodigoEnOtroProducto_ConVariosProductos_LosNombraTodos()
        {
            string texto = ProductoViewModel.TextoCodigoEnOtroProducto("8437017506362", new RespuestaAnnadirCodigoBarras
            {
                Productos = new List<ProductoDelCodigoBarrasModel>
                {
                    new() { Producto = "32564 ", Nombre = "GUANTES NITRILO T/P " },
                    new() { Producto = "32566", Nombre = "GUANTES NITRILO T/G" }
                }
            });

            StringAssert.StartsWith(texto, "Ese código ya es de los productos 32564 GUANTES NITRILO T/P, 32566 GUANTES NITRILO T/G.");
        }

        [TestMethod]
        public void HacerPrincipal_LlamaALaApiConElIdYRecarga()
        {
            var sut = CrearViewModel(out var servicio, out _, codigos: new List<CodigoBarrasProductoModel>
            {
                Codigo(1, "8437017506379", principal: true),
                Codigo(2, "8437017506362")
            });

            sut.CodigoBarrasSeleccionado = sut.CodigosBarras[0];
            Assert.IsFalse(sut.HacerPrincipalCodigoBarrasCommand.CanExecute(null), "El principal ya lo es");
            sut.CodigoBarrasSeleccionado = sut.CodigosBarras[1];
            Assert.IsTrue(sut.HacerPrincipalCodigoBarrasCommand.CanExecute(null));

            A.CallTo(() => servicio.LeerCodigosBarras(GUANTES_M)).Returns(new List<CodigoBarrasProductoModel>
            {
                Codigo(1, "8437017506379"),
                Codigo(2, "8437017506362", principal: true)
            });
            sut.HacerPrincipalCodigoBarrasCommand.Execute(null);

            A.CallTo(() => servicio.HacerPrincipalCodigoBarras(GUANTES_M, 2)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("8437017506362", sut.CodigoBarrasPrincipal);
            Assert.AreEqual("8437017506362", sut.CodigosBarras[0].Codigo);
        }

        [TestMethod]
        public void DarDeBaja_ConConfirmacion_LlamaALaApiYElCodigoSaleDeLaLista()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos, codigos: new List<CodigoBarrasProductoModel>
            {
                Codigo(1, "8437017506379", principal: true),
                Codigo(2, "8437017506362")
            });
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            sut.CodigoBarrasSeleccionado = sut.CodigosBarras[1];
            A.CallTo(() => servicio.LeerCodigosBarras(GUANTES_M)).Returns(new List<CodigoBarrasProductoModel>
            {
                Codigo(1, "8437017506379", principal: true),
                Codigo(2, "8437017506362", activo: false)
            });

            sut.DarDeBajaCodigoBarrasCommand.Execute(null);

            A.CallTo(() => servicio.DarDeBajaCodigoBarras(GUANTES_M, 2)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, sut.CodigosBarras.Count);
            Assert.IsNull(sut.CodigoBarrasSeleccionado);
        }

        [TestMethod]
        public void DarDeBaja_ElPrincipalNoSePuede()
        {
            var sut = CrearViewModel(out _, out _);

            sut.CodigoBarrasSeleccionado = sut.CodigosBarras[0];

            Assert.IsFalse(sut.DarDeBajaCodigoBarrasCommand.CanExecute(null));
        }

        [TestMethod]
        public void DarDeBaja_SinConfirmar_NoLlamaALaApi()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos, codigos: new List<CodigoBarrasProductoModel>
            {
                Codigo(1, "8437017506379", principal: true),
                Codigo(2, "8437017506362")
            });
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            sut.CodigoBarrasSeleccionado = sut.CodigosBarras[1];

            sut.DarDeBajaCodigoBarrasCommand.Execute(null);

            A.CallTo(() => servicio.DarDeBajaCodigoBarras(A<string>._, A<int>._)).MustNotHaveHappened();
        }
    }

    /// <summary>NestoAPI#605: las llamadas HTTP del servicio (rutas, 404 sin endpoint, 409 con productos).</summary>
    [TestClass]
    public class ProductoServiceCodigosBarrasTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<HttpRequestMessage> Peticiones { get; } = new();
            public List<string> Cuerpos { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Peticiones.Add(request);
                Cuerpos.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
                return _responder(request);
            }
        }

        private static ProductoService CrearServicio(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ProductoService(A.Fake<IConfiguracion>(), A.Fake<IServicioAutenticacion>(), factoria);
        }

        [TestMethod]
        public async Task LeerCodigosBarras_Con404_DevuelveNull()
        {
            var sut = CrearServicio(new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

            Assert.IsNull(await sut.LeerCodigosBarras("32565"));
        }

        [TestMethod]
        public async Task LeerCodigosBarras_LlamaALaRutaDelProducto()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"Id\":7,\"Codigo\":\"8437017506362\",\"Cantidad\":1,\"Principal\":true,\"Origen\":\"Almacen\",\"Activo\":true}]", Encoding.UTF8, "application/json")
            });
            var sut = CrearServicio(handler);

            var codigos = await sut.LeerCodigosBarras("32565 ");

            Assert.AreEqual("http://api.local/api/Productos/32565/CodigosBarras?empresa=1", handler.Peticiones[0].RequestUri.ToString());
            Assert.AreEqual(7, codigos[0].Id);
            Assert.AreEqual("Almacén", codigos[0].OrigenTexto);
        }

        [TestMethod]
        public async Task AnnadirCodigoBarras_Con409_DevuelveLosProductosQueYaLoTienen()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"Message\":\"El código ya es de otro producto\",\"Productos\":[{\"Producto\":\"32564\",\"Nombre\":\"GUANTES NITRILO T/P\"}]}", Encoding.UTF8, "application/json")
            });
            var sut = CrearServicio(handler);

            var respuesta = await sut.AnnadirCodigoBarras("32565", "8437017506362", 1, "", false);

            Assert.AreEqual(ResultadoAnnadirCodigoBarras.EnOtroProducto, respuesta.Resultado);
            Assert.AreEqual("32564", respuesta.Productos.Single().Producto);
            Assert.AreEqual(HttpMethod.Post, handler.Peticiones[0].Method);
            StringAssert.Contains(handler.Cuerpos[0], "\"Origen\":\"Ficha\"");
            StringAssert.Contains(handler.Cuerpos[0], "\"PermitirCompartido\":false");
            StringAssert.Contains(handler.Cuerpos[0], "\"Proveedor\":null");
        }

        [TestMethod]
        public async Task AnnadirCodigoBarras_201EsCreadoY200EsQueYaEraSuyo()
        {
            var creado = CrearServicio(new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Created)));
            var yaEra = CrearServicio(new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.AreEqual(ResultadoAnnadirCodigoBarras.Creado, (await creado.AnnadirCodigoBarras("32565", "1", 1, null, true)).Resultado);
            Assert.AreEqual(ResultadoAnnadirCodigoBarras.YaEraDelProducto, (await yaEra.AnnadirCodigoBarras("32565", "1", 1, null, false)).Resultado);
        }

        [TestMethod]
        public async Task DarDeBajaCodigoBarras_Con400_EnsenaElMotivoDeLaApi()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"Message\":\"El código principal no se puede dar de baja\"}", Encoding.UTF8, "application/json")
            });
            var sut = CrearServicio(handler);

            var ex = await Assert.ThrowsExceptionAsync<Exception>(() => sut.DarDeBajaCodigoBarras("32565", 7));

            Assert.AreEqual("El código principal no se puede dar de baja", ex.Message);
            Assert.AreEqual(HttpMethod.Delete, handler.Peticiones[0].Method);
            Assert.AreEqual("http://api.local/api/Productos/32565/CodigosBarras/7", handler.Peticiones[0].RequestUri.ToString());
        }

        [TestMethod]
        public async Task HacerPrincipalCodigoBarras_HaceUnPutALaRutaPrincipal()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var sut = CrearServicio(handler);

            await sut.HacerPrincipalCodigoBarras("32565", 7);

            Assert.AreEqual(HttpMethod.Put, handler.Peticiones[0].Method);
            Assert.AreEqual("http://api.local/api/Productos/32565/CodigosBarras/7/Principal", handler.Peticiones[0].RequestUri.ToString());
        }
    }
}
