using ControlesUsuario;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#488: el RelayCommand de CommunityToolkit (a diferencia del DelegateCommand de Prism) lanza
    /// CanExecuteChanged en el hilo que llama. Ejecutar un comando o avisar de su CanExecute desde dentro
    /// de un <c>Task.Run</c> toca los botones desde un hilo del pool. Caso real:
    /// <c>Await Task.Run(Sub() cmdCargarListaRapports.Execute(Nothing))</c> en ListaRapportsViewModel.
    ///
    /// Este «analizador» recorre el código fuente de Nesto y falla si vuelve a aparecer ese patrón.
    /// Para ejecutar una carga en segundo plano: una Function/Task que se espera en el hilo de la UI, y
    /// el trabajo pesado (solo el trabajo, sin tocar el ViewModel) dentro del Task.Run.
    /// </summary>
    [TestClass]
    public class ComandosFueraDeHiloUiTests
    {
        private static readonly Regex TaskRun = new Regex(@"\bTask\.Run\s*\(", RegexOptions.Compiled);

        // Dentro del cuerpo del Task.Run: ejecutar un comando (xxxCommand.Execute / cmdXxx.Execute) o avisar de
        // su CanExecute (NotifyCanExecuteChanged / RaiseCanExecuteChanged; NotifyCanExecuteChangedEnUi sí vale).
        private static readonly Regex Prohibido = new Regex(
            @"(\b\w*Command\b|\bcmd\w*)\s*(\?\.|\.)\s*Execute(Async)?\s*\(|\b(NotifyCanExecuteChanged|RaiseCanExecuteChanged)\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex Cadenas = new Regex("\"[^\"\n]*\"", RegexOptions.Compiled);

        /// <summary>Los cuerpos de Task.Run(...) que contienen algo prohibido.</summary>
        internal static IEnumerable<string> TaskRunConComandos(string codigo)
        {
            foreach (Match m in TaskRun.Matches(codigo))
            {
                string cuerpo = HastaParentesisDeCierre(codigo, m.Index + m.Length);
                // Lo que va entre comillas es texto, no código.
                if (Prohibido.IsMatch(Cadenas.Replace(cuerpo, "\"\"")))
                {
                    yield return cuerpo.Trim();
                }
            }
        }

        /// <summary>Desde justo después del "(" de Task.Run hasta su ")" (sin contar los de dentro de cadenas).</summary>
        private static string HastaParentesisDeCierre(string codigo, int inicio)
        {
            int nivel = 1;
            bool enCadena = false;
            for (int i = inicio; i < codigo.Length; i++)
            {
                char c = codigo[i];
                if (c == '"')
                {
                    enCadena = !enCadena;
                    continue;
                }
                if (enCadena)
                {
                    continue;
                }
                if (c == '(')
                {
                    nivel++;
                }
                else if (c == ')' && --nivel == 0)
                {
                    return codigo.Substring(inicio, i - inicio);
                }
            }
            return codigo.Substring(inicio);
        }

        // ---- El detector, probado ----

        [DataTestMethod]
        [DataRow("Await Task.Run(Sub() cmdCargarListaRapports.Execute(Nothing))")]
        [DataRow("await Task.Run(() => CargarCommand.Execute(null));")]
        [DataRow("_ = Task.Run(async () => { await Algo(); GuardarCommand.NotifyCanExecuteChanged(); });")]
        [DataRow("Task.Run(Sub()\n    Dim x = 1\n    cmdGuardar.Execute(Nothing)\nEnd Sub)")]
        [DataRow("await Task.Run(() => BuscarCommand?.Execute(texto));")]
        [DataRow("await Task.Run(async () => await CargarCommand.ExecuteAsync(null));")]
        public void Detector_EncuentraComandosDentroDeTaskRun(string codigo)
        {
            Assert.AreEqual(1, TaskRunConComandos(codigo).Count(), codigo);
        }

        [DataTestMethod]
        [DataRow("var paginas = await Task.Run(() => conexion.Financial.ListFinancialEvents(parametros));")]
        [DataRow("await Task.Run(() => regla.ApuntesContabilizar(a, b, c));\nGuardarCommand.NotifyCanExecuteChanged();")]
        [DataRow("_ = Task.Run(async () => { var v = await s.Cargar(1); Application.Current.Dispatcher.Invoke(() => { Video = v; }); });")]
        [DataRow("await Task.Run(() => Log(\"no toques GuardarCommand.Execute( aquí\"));")]
        [DataRow("await Task.Run(() => Trabajo());\nGenerarResumenCommand.NotifyCanExecuteChangedEnUi();")]
        public void Detector_NoSaltaConTrabajoSinComandos(string codigo)
        {
            Assert.AreEqual(0, TaskRunConComandos(codigo).Count(), codigo);
        }

        // ---- El código de Nesto ----

        [TestMethod]
        public void NingunTaskRunEjecutaComandosNiAvisaDeSuCanExecute()
        {
            string raiz = RaizDelRepositorio();
            var infracciones = new List<string>();
            foreach (string fichero in FicherosDeCodigo(raiz))
            {
                string codigo = File.ReadAllText(fichero);
                foreach (string cuerpo in TaskRunConComandos(codigo))
                {
                    infracciones.Add($"{Path.GetRelativePath(raiz, fichero)}: Task.Run({cuerpo.Split('\n')[0].Trim()}...)");
                }
            }

            Assert.AreEqual(0, infracciones.Count,
                "Nesto#488: no ejecutes comandos ni avises de su CanExecute dentro de Task.Run (el RelayCommand de " +
                "CommunityToolkit no vuelve al hilo de la UI). Convierte la carga en un método que devuelva Task y espéralo:\n" +
                string.Join("\n", infracciones));
        }

        [TestMethod]
        public void ElAnalizadorVeLosModulos()
        {
            // Si cambia la estructura del repo y deja de encontrar el código, el test de arriba pasaría sin mirar nada.
            List<string> ficheros = FicherosDeCodigo(RaizDelRepositorio()).ToList();
            Assert.IsTrue(ficheros.Any(f => f.EndsWith("ListaRapportsViewModel.vb", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(ficheros.Any(f => f.EndsWith("BancosViewModel.cs", StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(ficheros.Any(f => f.EndsWith(nameof(ComandosFueraDeHiloUiTests) + ".cs", StringComparison.OrdinalIgnoreCase)),
                "Los proyectos de tests no se analizan (este mismo lleva los ejemplos prohibidos)");
        }

        // ---- El helper ----

        [TestMethod]
        public void NotifyCanExecuteChangedEnUi_SinAplicacionWpf_AvisaDirectamente()
        {
            var comando = new RelayCommand(() => { });
            int avisos = 0;
            comando.CanExecuteChanged += (s, e) => avisos++;

            comando.NotifyCanExecuteChangedEnUi();

            Assert.AreEqual(1, avisos);
        }

        [TestMethod]
        public void NotifyCanExecuteChangedEnUi_ConComandoNulo_NoRevienta()
        {
            ((IRelayCommand)null).NotifyCanExecuteChangedEnUi();
        }

        private static string RaizDelRepositorio()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Nesto.sln")))
            {
                dir = dir.Parent;
            }
            Assert.IsNotNull(dir, "No se encuentra Nesto.sln subiendo desde " + AppContext.BaseDirectory);
            return dir.FullName;
        }

        private static IEnumerable<string> FicherosDeCodigo(string raiz)
        {
            return Directory.EnumerateFiles(raiz, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".vb", StringComparison.OrdinalIgnoreCase))
                .Where(f => !Path.GetRelativePath(raiz, f)
                    .Split(Path.DirectorySeparatorChar)
                    .Any(parte => parte.Equals("bin", StringComparison.OrdinalIgnoreCase)
                        || parte.Equals("obj", StringComparison.OrdinalIgnoreCase)
                        || parte.StartsWith(".", StringComparison.Ordinal)
                        || parte.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
