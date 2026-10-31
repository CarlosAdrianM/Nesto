// NestoAPI#774 — NÚCLEO DE LOS GLOBOS («consejos», coach marks). ESTE FICHERO ES EL MISMO en Nesto
// (Infrastructure/Globos/NucleoGlobos.cs) y en Ariadna (src/Ariadna.Core/Globos/NucleoGlobos.cs): solo cambia la línea del
// namespace. Sin dependencias de interfaz ni de HTTP, para poder sacarlo algún día a un paquete (y para TNV / NestoApp).
// Si se cambia en un repo, copiarlo al otro.
//
// Contrato con la API:
//   GET  api/Globos?aplicacion={Nesto|Ariadna|TNV|NestoApp}&pantalla={clave}&version={versión de la app}
//        → [{ Id, Aplicacion, Pantalla, Ancla, Titulo, Texto, Orden, NovedadId }] ya filtrados para el usuario del token
//          (activos, en fechas, de su grupo, desde su versión, no vistos) y ordenados (Orden, Id).
//   POST api/Globos/{id}/Visto → { Id, YaEstaba }, idempotente, por usuario (no por aparato).
// Claves: la pantalla es la vista sin sufijo (EnviarReposicionView → EnviarReposicion; RepartoPage → Reparto) y el ancla,
// «Pantalla.Control» (Reparto.EnLaFurgoneta), la misma que lleva el control en la propiedad adjunta Globo.Ancla.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Globos;

/// <summary>Un globo tal como lo manda GET api/Globos. La app solo decide DÓNDE pintarlo.</summary>
public sealed class DatosGlobo
{
    public int Id { get; set; }
    public string? Pantalla { get; set; }
    /// <summary>Clave completa del control («Reparto.EnLaFurgoneta»). Null = tarjeta centrada.</summary>
    public string? Ancla { get; set; }
    public string? Titulo { get; set; }
    public string? Texto { get; set; }
    public short Orden { get; set; }
    public int? NovedadId { get; set; }
}

/// <summary>Un globo que toca enseñar ahora, y si va junto a su control o como tarjeta centrada.</summary>
public sealed record GloboAEnsenar(DatosGlobo Globo, bool Centrado);

/// <summary>Las dos llamadas a la API, con la sesión del usuario activo. Cada app pone la suya.</summary>
public interface IClienteGlobos
{
    /// <summary>Puede lanzar: el servicio lo trata como «ninguno».</summary>
    Task<List<DatosGlobo>> LeerPendientesAsync(string pantalla, CancellationToken cancelacion = default);

    Task MarcarVistoAsync(int id, CancellationToken cancelacion = default);
}

/// <summary>
/// Qué globos se enseñan en cada pantalla:
/// <list type="bullet">
/// <item>Una sola llamada por USUARIO y pantalla mientras dure la app (caché en memoria); sin sondeo. Si falla, ninguno, y se
///   vuelve a pedir la próxima vez que se entre en la pantalla.</item>
/// <item>Con el cambio de usuario (Ariadna), cada uno tiene lo suyo: la caché va por usuario y el «visto» lo guarda el servidor.</item>
/// <item>Como mucho <see cref="MaximoSeguidos"/> seguidos, en el orden de la API; los demás, la próxima vez que se entre.</item>
/// <item>Una tanda a la vez (si ya hay globos en pantalla, ninguno más).</item>
/// <item>Sin ancla, o con el ancla ausente o no visible: tarjeta centrada.</item>
/// <item><see cref="EntendidoAsync"/> («Entendido», «Siguiente») → visto en el servidor. <see cref="Aplazar"/> (Esc) → no se marca y
///   no vuelve hasta que se reabra la app. <see cref="Soltar"/> (se fue de la pantalla sin contestar) → sigue pendiente.</item>
/// </list>
/// </summary>
public sealed class ServicioGlobos
{
    public const int MaximoPorDefecto = 2;

    private readonly IClienteGlobos _cliente;
    private readonly Func<string?> _usuario;
    private readonly object _cerrojo = new object();
    private readonly Dictionary<string, List<DatosGlobo>> _pendientes = new Dictionary<string, List<DatosGlobo>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<List<DatosGlobo>>> _pidiendo = new Dictionary<string, Task<List<DatosGlobo>>>(StringComparer.OrdinalIgnoreCase);
    // «usuario|id»: los ya contestados (o aplazados) por cada usuario en esta sesión
    private readonly HashSet<string> _retirados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _enPantalla = new HashSet<int>();

    /// <param name="usuarioActivo">Quién está usando la app ahora (sin él, ningún globo).</param>
    public ServicioGlobos(IClienteGlobos cliente, Func<string?> usuarioActivo, int maximoSeguidos = MaximoPorDefecto)
    {
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
        _usuario = usuarioActivo ?? throw new ArgumentNullException(nameof(usuarioActivo));
        MaximoSeguidos = Math.Max(1, maximoSeguidos);
    }

    public int MaximoSeguidos { get; }

    /// <summary>Hay una tanda en pantalla.</summary>
    public bool HayEnPantalla
    {
        get
        {
            lock (_cerrojo)
            {
                return _enPantalla.Count > 0;
            }
        }
    }

    /// <summary>
    /// Los que tocan ahora en la pantalla para el usuario activo (como mucho <see cref="MaximoSeguidos"/>). Quedan reservados hasta
    /// <see cref="EntendidoAsync"/>, <see cref="Aplazar"/> o <see cref="Soltar"/>.
    /// </summary>
    /// <param name="anclaVisible">Si el control de esa clave está a la vista. Se llama en el hilo de quien pregunta.</param>
    public async Task<IReadOnlyList<GloboAEnsenar>> QueEnsenarAsync(string? pantalla, Func<string, bool>? anclaVisible)
    {
        string? usuario = Normalizar(_usuario());
        string? clavePantalla = pantalla?.Trim();
        if (usuario == null || string.IsNullOrEmpty(clavePantalla))
        {
            return Array.Empty<GloboAEnsenar>();
        }
        // Sin ConfigureAwait(false): anclaVisible mira controles y tiene que correr en el hilo de la pantalla
        List<DatosGlobo> pendientes = await PendientesAsync(usuario, clavePantalla!);
        lock (_cerrojo)
        {
            // Si mientras se pedía ha cambiado el usuario, nada: lo suyo saldrá cuando vuelva a entrar en la pantalla
            if (_enPantalla.Count > 0 || !string.Equals(Normalizar(_usuario()), usuario, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<GloboAEnsenar>();
            }
            List<GloboAEnsenar> tanda = pendientes
                .Where(g => !_retirados.Contains(Clave(usuario, g.Id)))
                .Take(MaximoSeguidos)
                .Select(g => new GloboAEnsenar(g, EsCentrado(g, anclaVisible)))
                .ToList();
            foreach (GloboAEnsenar g in tanda)
            {
                _ = _enPantalla.Add(g.Globo.Id);
            }
            return tanda;
        }
    }

    /// <summary>Lo ha leído: visto en el servidor y no vuelve para ese usuario. Nunca lanza.</summary>
    public async Task EntendidoAsync(DatosGlobo globo)
    {
        if (globo == null)
        {
            return;
        }
        Retirar(globo);
        try
        {
            await _cliente.MarcarVistoAsync(globo.Id).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Un globo nunca molesta: si no se pudo marcar, como mucho vuelve a salir al reabrir la app
        }
    }

    /// <summary>Esc: no se marca visto; no vuelve hasta que se reabra la app.</summary>
    public void Aplazar(DatosGlobo globo)
    {
        if (globo != null)
        {
            Retirar(globo);
        }
    }

    /// <summary>No llegó a contestarse (se fue de la pantalla antes): vuelve a estar pendiente.</summary>
    public void Soltar(DatosGlobo globo)
    {
        if (globo == null)
        {
            return;
        }
        lock (_cerrojo)
        {
            _ = _enPantalla.Remove(globo.Id);
        }
    }

    private void Retirar(DatosGlobo globo)
    {
        string? usuario = Normalizar(_usuario());
        lock (_cerrojo)
        {
            _ = _enPantalla.Remove(globo.Id);
            if (usuario != null)
            {
                _ = _retirados.Add(Clave(usuario, globo.Id));
            }
        }
    }

    private static bool EsCentrado(DatosGlobo globo, Func<string, bool>? anclaVisible)
    {
        if (string.IsNullOrWhiteSpace(globo.Ancla) || anclaVisible == null)
        {
            return true;
        }
        try
        {
            return !anclaVisible(globo.Ancla!.Trim());
        }
        catch (Exception)
        {
            return true;
        }
    }

    private Task<List<DatosGlobo>> PendientesAsync(string usuario, string pantalla)
    {
        string clave = usuario + "|" + pantalla;
        lock (_cerrojo)
        {
            if (_pendientes.TryGetValue(clave, out List<DatosGlobo>? ya))
            {
                return Task.FromResult(ya.ToList());
            }
            if (!_pidiendo.TryGetValue(clave, out Task<List<DatosGlobo>>? enCurso))
            {
                enCurso = PedirAsync(clave, pantalla);
                // Si ya ha terminado (falló o respondió sin esperar), ya ha dejado su resultado: no se guarda la tarea
                if (!enCurso.IsCompleted)
                {
                    _pidiendo[clave] = enCurso;
                }
            }
            return enCurso;
        }
    }

    private async Task<List<DatosGlobo>> PedirAsync(string clave, string pantalla)
    {
        List<DatosGlobo>? leidos = null;
        try
        {
            leidos = await _cliente.LeerPendientesAsync(pantalla).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Sin globos: la pantalla sigue como siempre
        }
        lock (_cerrojo)
        {
            _ = _pidiendo.Remove(clave);
            if (leidos == null)
            {
                return new List<DatosGlobo>();
            }
            List<DatosGlobo> validos = leidos.Where(g => g != null && !string.IsNullOrWhiteSpace(g.Texto)).ToList();
            _pendientes[clave] = validos;
            return validos.ToList();
        }
    }

    private static string Clave(string usuario, int id) => usuario + "|" + id;

    private static string? Normalizar(string? usuario)
    {
        string? limpio = usuario?.Trim();
        return string.IsNullOrEmpty(limpio) ? null : limpio;
    }
}

/// <summary>
/// La clave de pantalla de una vista según su tipo: sin el sufijo («EnviarReposicionView» → «EnviarReposicion»; «RepartoPage» →
/// «Reparto»). La que se ponga a mano (Globo.Pantalla) manda. Null si el tipo no acaba en ninguno de los sufijos.
/// </summary>
public static class ClavesPantallaGlobos
{
    public static string? DeTipo(string? nombreTipo, string? puesta, params string[] sufijos)
    {
        if (!string.IsNullOrWhiteSpace(puesta))
        {
            return puesta!.Trim();
        }
        if (string.IsNullOrWhiteSpace(nombreTipo))
        {
            return null;
        }
        foreach (string sufijo in sufijos ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(sufijo) && nombreTipo!.Length > sufijo.Length && nombreTipo.EndsWith(sufijo, StringComparison.Ordinal))
            {
                return nombreTipo.Substring(0, nombreTipo.Length - sufijo.Length);
            }
        }
        return null;
    }
}
