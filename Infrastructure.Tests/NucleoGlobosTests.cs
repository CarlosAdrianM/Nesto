// NestoAPI#774: pruebas del núcleo de los globos. MISMO FICHERO en Nesto (Infrastructure.Tests/NucleoGlobosTests.cs) y en
// Ariadna (tests/Ariadna.Core.Tests/NucleoGlobosTests.cs): solo cambian las dos líneas de namespace.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Globos;

namespace Infrastructure.Tests;

[TestClass]
public class NucleoGlobosTests
{
    private sealed class ClienteFalso : IClienteGlobos
    {
        public readonly Dictionary<string, Queue<Func<List<DatosGlobo>>>> Respuestas = new Dictionary<string, Queue<Func<List<DatosGlobo>>>>();
        public readonly List<string> Leidas = new List<string>();
        public readonly List<(string? Usuario, int Id)> Vistos = new List<(string?, int)>();
        public Func<string?> Usuario = () => null;
        public bool FallarAlMarcar;

        public void Devuelve(string pantalla, params DatosGlobo[] globos) => Encolar(pantalla, () => globos.ToList());

        public void Encolar(string pantalla, Func<List<DatosGlobo>> respuesta)
        {
            if (!Respuestas.TryGetValue(pantalla, out Queue<Func<List<DatosGlobo>>>? cola))
            {
                cola = new Queue<Func<List<DatosGlobo>>>();
                Respuestas[pantalla] = cola;
            }
            cola.Enqueue(respuesta);
        }

        public Task<List<DatosGlobo>> LeerPendientesAsync(string pantalla, CancellationToken cancelacion = default)
        {
            Leidas.Add(Usuario() + "|" + pantalla);
            if (!Respuestas.TryGetValue(pantalla, out Queue<Func<List<DatosGlobo>>>? cola) || cola.Count == 0)
            {
                return Task.FromResult(new List<DatosGlobo>());
            }
            // La última respuesta se repite (cada usuario pide lo suyo)
            Func<List<DatosGlobo>> respuesta = cola.Count > 1 ? cola.Dequeue() : cola.Peek();
            return Task.FromResult(respuesta());
        }

        public Task MarcarVistoAsync(int id, CancellationToken cancelacion = default)
        {
            if (FallarAlMarcar)
            {
                throw new HttpRequestException("sin red");
            }
            Vistos.Add((Usuario(), id));
            return Task.CompletedTask;
        }
    }

    private ClienteFalso cliente = null!;
    private string? usuario;
    private ServicioGlobos servicio = null!;

    [TestInitialize]
    public void Preparar()
    {
        usuario = "andre";
        cliente = new ClienteFalso();
        cliente.Usuario = () => usuario;
        servicio = new ServicioGlobos(cliente, () => usuario);
    }

    private static DatosGlobo Globo(int id, string? ancla = null) => new DatosGlobo { Id = id, Pantalla = "Reparto", Ancla = ancla, Texto = "Texto " + id };

    private async Task<List<int>> Ids(string? pantalla, Func<string, bool>? visible = null)
        => (await servicio.QueEnsenarAsync(pantalla, visible ?? (_ => true))).Select(g => g.Globo.Id).ToList();

    [TestMethod]
    public async Task UnaSolaLlamadaPorPantallaYUsuario()
    {
        cliente.Devuelve("Reparto", Globo(1));

        List<int> primera = await Ids("Reparto");
        servicio.Soltar(Globo(1));
        List<int> segunda = await Ids("Reparto");

        CollectionAssert.AreEqual(new[] { 1 }, primera);
        CollectionAssert.AreEqual(new[] { 1 }, segunda);
        Assert.AreEqual(1, cliente.Leidas.Count);
    }

    [TestMethod]
    public async Task CadaPantallaTieneSuLlamada()
    {
        cliente.Devuelve("A", Globo(1));
        cliente.Devuelve("B", Globo(2));

        CollectionAssert.AreEqual(new[] { 1 }, await Ids("A"));
        servicio.Aplazar(Globo(1));
        CollectionAssert.AreEqual(new[] { 2 }, await Ids("B"));
    }

    [TestMethod]
    public async Task ComoMuchoDosSeguidos_YLosDemasLaProximaVez()
    {
        cliente.Devuelve("Reparto", Globo(1), Globo(2), Globo(3));

        List<int> primera = await Ids("Reparto");
        await servicio.EntendidoAsync(Globo(1));
        await servicio.EntendidoAsync(Globo(2));
        List<int> segunda = await Ids("Reparto");

        CollectionAssert.AreEqual(new[] { 1, 2 }, primera);
        CollectionAssert.AreEqual(new[] { 3 }, segunda);
        Assert.AreEqual(2, servicio.MaximoSeguidos);
        Assert.AreEqual(ServicioGlobos.MaximoPorDefecto, servicio.MaximoSeguidos);
    }

    [TestMethod]
    public async Task AnclaAusenteNoVisibleOSinAncla_Centrado()
    {
        cliente.Devuelve("P", Globo(1, "P.Visible"), Globo(2, "P.Oculta"));
        cliente.Devuelve("Q", Globo(3, null), Globo(4, "Q.QueRevienta"));

        IReadOnlyList<GloboAEnsenar> p = await servicio.QueEnsenarAsync("P", a => a == "P.Visible");
        servicio.Soltar(p[0].Globo);
        servicio.Soltar(p[1].Globo);
        IReadOnlyList<GloboAEnsenar> q = await servicio.QueEnsenarAsync("Q", _ => throw new InvalidOperationException());

        Assert.IsFalse(p[0].Centrado);
        Assert.IsTrue(p[1].Centrado);
        Assert.AreEqual(2, q.Count);
        Assert.IsTrue(q.All(g => g.Centrado));
    }

    [TestMethod]
    public async Task Entendido_MarcaVistoEnElServidor_YNoVuelve()
    {
        cliente.Devuelve("P", Globo(1));

        _ = await Ids("P");
        await servicio.EntendidoAsync(Globo(1));

        Assert.AreEqual(1, cliente.Vistos.Count);
        Assert.AreEqual(1, cliente.Vistos[0].Id);
        Assert.AreEqual(0, (await Ids("P")).Count);
    }

    [TestMethod]
    public async Task Entendido_SiFallaElServidor_NoRompeYNoVuelveEnLaSesion()
    {
        cliente.Devuelve("P", Globo(1));
        cliente.FallarAlMarcar = true;

        _ = await Ids("P");
        await servicio.EntendidoAsync(Globo(1));

        Assert.AreEqual(0, (await Ids("P")).Count);
    }

    [TestMethod]
    public async Task Aplazar_NoMarcaVisto_YNoVuelveEnLaSesion()
    {
        cliente.Devuelve("P", Globo(1));

        _ = await Ids("P");
        servicio.Aplazar(Globo(1));

        Assert.AreEqual(0, cliente.Vistos.Count);
        Assert.AreEqual(0, (await Ids("P")).Count);
    }

    [TestMethod]
    public async Task UnaTandaALaVez_HastaQueSeContestaOSeSuelta()
    {
        cliente.Devuelve("A", Globo(1));
        cliente.Devuelve("B", Globo(2));

        _ = await Ids("A");
        bool hay = servicio.HayEnPantalla;
        List<int> mientras = await Ids("B");
        servicio.Soltar(Globo(1));
        List<int> despues = await Ids("B");

        Assert.IsTrue(hay);
        Assert.AreEqual(0, mientras.Count);
        CollectionAssert.AreEqual(new[] { 2 }, despues);
    }

    [TestMethod]
    public async Task SiFallaLaLectura_Ninguno_YSeVuelveAPedirLaProximaVez()
    {
        cliente.Encolar("P", () => throw new HttpRequestException("sin red"));
        cliente.Encolar("P", () => new List<DatosGlobo> { Globo(1) });

        List<int> primera = await Ids("P");
        List<int> segunda = await Ids("P");

        Assert.AreEqual(0, primera.Count);
        CollectionAssert.AreEqual(new[] { 1 }, segunda);
    }

    [TestMethod]
    public async Task SinPantallaSinUsuarioOSinTexto_Ninguno()
    {
        cliente.Encolar("P", () => new List<DatosGlobo> { new DatosGlobo { Id = 1, Texto = " " }, null! });

        Assert.AreEqual(0, (await Ids(null)).Count);
        Assert.AreEqual(0, (await Ids("P")).Count);
        usuario = " ";
        Assert.AreEqual(0, (await Ids("Q")).Count);
        Assert.IsFalse(cliente.Leidas.Any(l => l.EndsWith("|Q")));
    }

    [TestMethod]
    public async Task CambioDeUsuario_CadaUnoVeLoSuyo()
    {
        cliente.Devuelve("Reparto", Globo(1), Globo(2));

        // Andre ve los dos y los contesta
        CollectionAssert.AreEqual(new[] { 1, 2 }, await Ids("Reparto"));
        await servicio.EntendidoAsync(Globo(1));
        await servicio.EntendidoAsync(Globo(2));
        Assert.AreEqual(0, (await Ids("Reparto")).Count);

        // Pedro coge la PDA: su propia llamada (con su sesión) y le salen a él
        usuario = "pedro";
        CollectionAssert.AreEqual(new[] { 1, 2 }, await Ids("Reparto"));
        servicio.Aplazar(Globo(1));
        servicio.Aplazar(Globo(2));

        // Vuelve Andre: sigue sin ver los que ya contestó, y no se ha vuelto a pedir lo suyo
        usuario = "andre";
        Assert.AreEqual(0, (await Ids("Reparto")).Count);
        CollectionAssert.AreEqual(new[] { "andre|Reparto", "pedro|Reparto" }, cliente.Leidas);
        CollectionAssert.AreEqual(new[] { ("andre", 1), ("andre", 2) }, cliente.Vistos.Select(v => (v.Usuario, v.Id)).ToList());
    }

    [TestMethod]
    public async Task SiCambiaElUsuarioMientrasSePide_NoSeEnsenaAlNuevo()
    {
        var espera = new TaskCompletionSource<List<DatosGlobo>>();
        var lento = new ClienteLento(espera.Task);
        var conLento = new ServicioGlobos(lento, () => usuario);

        Task<IReadOnlyList<GloboAEnsenar>> pidiendo = conLento.QueEnsenarAsync("Reparto", _ => true);
        usuario = "pedro";
        espera.SetResult(new List<DatosGlobo> { Globo(1) });

        Assert.AreEqual(0, (await pidiendo).Count);
        Assert.IsFalse(conLento.HayEnPantalla);
    }

    private sealed class ClienteLento : IClienteGlobos
    {
        private readonly Task<List<DatosGlobo>> _respuesta;
        public ClienteLento(Task<List<DatosGlobo>> respuesta) { _respuesta = respuesta; }
        public Task<List<DatosGlobo>> LeerPendientesAsync(string pantalla, CancellationToken cancelacion = default) => _respuesta;
        public Task MarcarVistoAsync(int id, CancellationToken cancelacion = default) => Task.CompletedTask;
    }

    [DataTestMethod]
    [DataRow("RepartoPage", null, "Reparto")]
    [DataRow("EnviarReposicionView", null, "EnviarReposicion")]
    [DataRow("PanelBultos", null, null)]
    [DataRow("Page", null, null)]
    [DataRow("PanelBultos", "Reparto", "Reparto")]
    [DataRow(null, null, null)]
    public void ClaveDePantalla(string? tipo, string? puesta, string? esperada)
    {
        Assert.AreEqual(esperada, ClavesPantallaGlobos.DeTipo(tipo, puesta, "Page", "View"));
    }
}
