using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Nesto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.5, tramo 4): la guarda de que Prism y Unity no vuelven. Tres miradas, porque cada una ve una cosa:
    /// <list type="bullet">
    /// <item>los ensamblados de Nesto (el exe y todo lo suyo que carga) no referencian Prism.* ni Unity* (lo que se usa
    /// en el código);</item>
    /// <item>las dependencias de la compilación (deps.json y la carpeta de salida) no traen ningún paquete de Prism ni de
    /// Unity, tampoco de rebote por otro proyecto;</item>
    /// <item>ningún proyecto de la solución tiene un PackageReference de Prism o Unity (aunque nadie lo use, el compilador
    /// quitaría la referencia del ensamblado y no se vería).</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class SinPrismNiUnityTests
    {
        private static bool EsDePrismOUnity(string nombre)
            => nombre != null && (nombre.StartsWith("Prism", StringComparison.OrdinalIgnoreCase)
                                  || nombre.StartsWith("Unity", StringComparison.OrdinalIgnoreCase));

        [TestMethod]
        public void NingunEnsambladoDeNesto_ReferenciaPrismNiUnity()
        {
            var ensamblados = new Dictionary<string, Assembly>();
            var pendientes = new Queue<Assembly>(new[] { typeof(MenuBarView).Assembly });
            var conPrism = new List<string>();
            while (pendientes.Count > 0)
            {
                Assembly actual = pendientes.Dequeue();
                if (!ensamblados.TryAdd(actual.GetName().Name, actual))
                {
                    continue;
                }
                foreach (AssemblyName referencia in actual.GetReferencedAssemblies())
                {
                    if (EsDePrismOUnity(referencia.Name))
                    {
                        conPrism.Add($"{actual.GetName().Name} → {referencia.Name}");
                        continue;
                    }
                    if (ensamblados.ContainsKey(referencia.Name))
                    {
                        continue;
                    }
                    Assembly cargado;
                    try { cargado = Assembly.Load(referencia); } catch { continue; }
                    bool esDeNesto = cargado.GetName().Name == "Infrastructure"
                        || cargado.GetReferencedAssemblies().Any(r => r.Name == "Infrastructure");
                    if (esDeNesto)
                    {
                        pendientes.Enqueue(cargado);
                    }
                }
            }

            Assert.IsTrue(ensamblados.ContainsKey("Infrastructure") && ensamblados.Count > 10,
                "Se tienen que mirar todos los ensamblados de Nesto: " + string.Join(", ", ensamblados.Keys));
            Assert.AreEqual(0, conPrism.Count, "Referencian Prism o Unity: " + string.Join("; ", conPrism));
        }

        [TestMethod]
        public void LaCompilacion_NoTraePaquetesDePrismNiDeUnity()
        {
            string carpeta = Path.GetDirectoryName(typeof(SinPrismNiUnityTests).Assembly.Location);

            List<string> dlls = Directory.GetFiles(carpeta, "*.dll")
                .Select(Path.GetFileName)
                .Where(EsDePrismOUnity)
                .ToList();
            string depsJson = File.ReadAllText(Path.Combine(carpeta, "Nesto.Tests.deps.json"));
            List<string> paquetes = Regex.Matches(depsJson, "\"((?:Prism|Unity)[^\"/]*)/[^\"]*\"\\s*:")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            Assert.AreEqual(0, dlls.Count, "En la carpeta de salida: " + string.Join(", ", dlls));
            Assert.AreEqual(0, paquetes.Count, "En las dependencias (deps.json): " + string.Join(", ", paquetes));
        }

        [TestMethod]
        public void NingunProyecto_TieneUnPackageReferenceDePrismNiDeUnity()
        {
            DirectoryInfo raiz = new DirectoryInfo(Path.GetDirectoryName(typeof(SinPrismNiUnityTests).Assembly.Location));
            while (raiz != null && !File.Exists(Path.Combine(raiz.FullName, "Nesto.sln")))
            {
                raiz = raiz.Parent;
            }
            Assert.IsNotNull(raiz, "No se encuentra Nesto.sln por encima de la carpeta de las pruebas");

            // Los proyectos de la solución (no las copias viejas de MigrationBackup ni de los informes)
            string solucion = File.ReadAllText(Path.Combine(raiz.FullName, "Nesto.sln"));
            List<string> proyectos = Regex.Matches(solucion, "\"([^\"]+\\.(?:csproj|vbproj))\"")
                .Select(m => Path.Combine(raiz.FullName, m.Groups[1].Value))
                .Where(File.Exists)
                .ToList();
            List<string> conPrism = proyectos
                .Where(p => Regex.IsMatch(File.ReadAllText(p), "<PackageReference\\s+Include=\"(Prism|Unity)", RegexOptions.IgnoreCase))
                .Select(p => Path.GetRelativePath(raiz.FullName, p))
                .ToList();

            Assert.IsTrue(proyectos.Count > 20, $"Solo se han encontrado {proyectos.Count} proyectos en Nesto.sln");
            Assert.AreEqual(0, conPrism.Count, "Con PackageReference de Prism o Unity: " + string.Join(", ", conPrism));
        }
    }
}
