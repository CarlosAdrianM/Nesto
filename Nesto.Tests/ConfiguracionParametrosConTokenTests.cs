using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Views;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Tests
{
    /// <summary>
    /// Nesto#639 (para poder cerrar PUT api/ParametrosUsuario): Configuracion lee y guarda los parámetros del usuario con el HttpClient
    /// de la fábrica común (la que pone el token), también en las versiones síncronas (esperando en otro hilo: no se bloquea el de la
    /// interfaz).
    /// </summary>
    [TestClass]
    public class ConfiguracionParametrosConTokenTests
    {
        private sealed class Transporte : HttpMessageHandler
        {
            public List<HttpRequestMessage> Peticiones { get; } = new();
            public List<string> Cuerpos { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Peticiones.Add(request);
                Cuerpos.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\" 1 \"", Encoding.UTF8, "application/json") };
            }
        }

        private Transporte _transporte = null!;
        private IDisposable _contenedor = null!;
        private IClienteApiFactory _fabrica = null!;

        [TestInitialize]
        public void Preparar()
        {
            _transporte = new Transporte();
            _fabrica = A.Fake<IClienteApiFactory>();
            A.CallTo(() => _fabrica.Crear()).ReturnsLazily(() => new HttpClient(_transporte, false)
            {
                BaseAddress = new Uri("http://api.prueba/api/"),
                DefaultRequestHeaders = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "TOKEN") }
            });
            IResolutorNesto resolutor = A.Fake<IResolutorNesto>();
            A.CallTo(() => resolutor.Resolver<IClienteApiFactory>()).Returns(_fabrica);
            _contenedor = Contenedor.Sustituir(resolutor);
        }

        [TestCleanup]
        public void Limpiar() => _contenedor.Dispose();

        [TestMethod]
        public async Task GuardarParametro_VaPorLaFabricaConToken()
        {
            await new Configuracion().GuardarParametro("1", "Clave", "1");

            HttpRequestMessage p = _transporte.Peticiones[0];
            Assert.AreEqual(HttpMethod.Put, p.Method);
            Assert.AreEqual("http://api.prueba/api/ParametrosUsuario", p.RequestUri!.ToString());
            Assert.AreEqual("Bearer TOKEN", p.Headers.Authorization?.ToString());
            StringAssert.Contains(_transporte.Cuerpos[0], "\"Clave\":\"Clave\"");
        }

        [TestMethod]
        public void GuardarParametroSync_VaPorLaFabricaConToken()
        {
            new Configuracion().GuardarParametroSync("1", "Clave", "0");

            Assert.AreEqual(HttpMethod.Put, _transporte.Peticiones[0].Method);
            Assert.AreEqual("Bearer TOKEN", _transporte.Peticiones[0].Headers.Authorization?.ToString());
        }

        [TestMethod]
        public async Task LeerParametro_YSuVersionSincrona_VanPorLaFabrica()
        {
            var configuracion = new Configuracion();

            Assert.AreEqual("1", await configuracion.leerParametro("1", "Clave"));
            Assert.AreEqual("1", configuracion.LeerParametroSync("1", "Clave"));

            Assert.AreEqual(2, _transporte.Peticiones.Count);
            Assert.IsTrue(_transporte.Peticiones.TrueForAll(p => p.Method == HttpMethod.Get && p.Headers.Authorization != null));
            StringAssert.StartsWith(_transporte.Peticiones[1].RequestUri!.PathAndQuery, "/api/ParametrosUsuario?empresa=1&usuario=");
            A.CallTo(() => _fabrica.Crear()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public void LaVersionSincrona_NoSeBloqueaConUnContextoDeSincronizacion()
        {
            // Como el hilo de la interfaz: un contexto que nunca ejecuta las continuaciones que se le mandan
            SynchronizationContext anterior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new ContextoQueNoEjecuta());
            try
            {
                Assert.AreEqual("1", new Configuracion().LeerParametroSync("1", "Clave"));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(anterior);
            }
        }

        private sealed class ContextoQueNoEjecuta : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object? state) { }
        }
    }
}
