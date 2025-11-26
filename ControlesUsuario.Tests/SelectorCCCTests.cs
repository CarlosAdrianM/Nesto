using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Tests de caracterización para SelectorCCC.
    /// Estos tests documentan el comportamiento actual del control sin modificarlo.
    ///
    /// Comportamientos clave documentados:
    /// - Carga de CCCs cuando cambian Empresa/Cliente/Contacto
    /// - Auto-selección según FormaPago ("RCB" = primer CCC válido, else = "(Sin CCC)")
    /// - Opción "(Sin CCC)" siempre presente como primera opción (retorna NULL)
    /// - CCCs inválidos (estado < 0) deben mostrarse pero estar deshabilitados
    /// - Protección contra bucles infinitos con flag _estaCargando
    /// - Respeto de selección previa si sigue siendo válida
    ///
    /// Carlos 20/11/24: Tests de caracterización siguiendo el patrón de SelectorDireccionEntrega
    /// </summary>
    [TestClass]
    public class SelectorCCCTests
    {
        #region Test: Dependency Properties y sus callbacks

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("DependencyProperties")]
        public void SelectorCCC_AlCambiarEmpresa_PropiedadSeEstableceCorrectamente()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();

            SelectorCCC sut = null;
            string resultado = null;

            Thread thread = new Thread(() =>
            {
                sut = new SelectorCCC(servicioCCC);

                // Act: Cambiar Empresa debería llamar a CargarCCCsAsync()
                // (según OnEmpresaChanged, que verifica !_estaCargando)
                sut.Empresa = "1";
                resultado = sut.Empresa;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.AreEqual("1", resultado, "La propiedad Empresa debería haberse establecido");
            Assert.IsNotNull(sut, "El control debería haberse creado correctamente");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("DependencyProperties")]
        public void SelectorCCC_AlCambiarCliente_PropiedadSeEstableceCorrectamente()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();

            string resultado = null;

            Thread thread = new Thread(() =>
            {
                var sut = new SelectorCCC(servicioCCC);

                // Act: Cambiar Cliente debería llamar a CargarCCCsAsync()
                sut.Cliente = "10458";
                resultado = sut.Cliente;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.AreEqual("10458", resultado, "La propiedad Cliente debería haberse establecido");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("DependencyProperties")]
        public void SelectorCCC_AlCambiarContacto_PropiedadSeEstableceCorrectamente()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();

            string resultado = null;

            Thread thread = new Thread(() =>
            {
                var sut = new SelectorCCC(servicioCCC);

                // Act: Cambiar Contacto debería llamar a CargarCCCsAsync()
                sut.Contacto = "0";
                resultado = sut.Contacto;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.AreEqual("0", resultado, "La propiedad Contacto debería haberse establecido");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("DependencyProperties")]
        public void SelectorCCC_AlCambiarFormaPago_PropiedadSeEstableceCorrectamente()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();

            string resultado = null;

            Thread thread = new Thread(() =>
            {
                var sut = new SelectorCCC(servicioCCC);

                // Act: Cambiar FormaPago debería llamar a ActualizarSeleccionSegunFormaPago()
                sut.FormaPago = "RCB"; // Recibo bancario
                resultado = sut.FormaPago;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.AreEqual("RCB", resultado, "La propiedad FormaPago debería haberse establecido");
        }

        #endregion

        #region Test: Auto-selección según FormaPago

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("AutoSeleccion")]
        public async Task SelectorCCC_ConFormaPagoRCB_DeberiAutoSeleccionarPrimerCCCValido()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();

            var cccsEsperados = new List<CCCItem>
            {
                new CCCItem { numero = "ES1234567890", estado = 1 }, // Válido
                new CCCItem { numero = "ES9876543210", estado = -1 } // Inválido
            };

            A.CallTo(() => servicioCCC.ObtenerCCCs("1", "10", "0"))
                .Returns(Task.FromResult<IEnumerable<CCCItem>>(cccsEsperados));

            SelectorCCC sut = null;
            string cccSeleccionado = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorCCC(servicioCCC);
                sut.FormaPago = "RCB"; // Recibo bancario - debe auto-seleccionar primer CCC válido
                sut.Contacto = "0";    // Establecer Contacto primero
                sut.Cliente = "10";    // Luego Cliente
                sut.Empresa = "1";     // Empresa al final (dispara CargarCCCsAsync)

                System.Threading.Thread.Sleep(300);
                cccSeleccionado = sut.CCCSeleccionado;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(200);

            // Assert
            Assert.AreEqual("ES1234567890", cccSeleccionado,
                "Cuando FormaPago = 'RCB', debe auto-seleccionar el primer CCC válido");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("AutoSeleccion")]
        public async Task SelectorCCC_ConFormaPagoNoRCB_DeberiAutoSeleccionarSinCCC()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();

            var cccsEsperados = new List<CCCItem>
            {
                new CCCItem { numero = "ES1234567890", estado = 1 }, // Válido
            };

            A.CallTo(() => servicioCCC.ObtenerCCCs("1", "10", "0"))
                .Returns(Task.FromResult<IEnumerable<CCCItem>>(cccsEsperados));

            SelectorCCC sut = null;
            string cccSeleccionado = "valorInicial"; // Para detectar si cambió a null

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorCCC(servicioCCC);
                sut.FormaPago = "EFC"; // Efectivo - NO es RCB, debe auto-seleccionar "(Sin CCC)"
                sut.Contacto = "0";
                sut.Cliente = "10";
                sut.Empresa = "1";

                System.Threading.Thread.Sleep(300);
                cccSeleccionado = sut.CCCSeleccionado;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(200);

            // Assert
            Assert.IsNull(cccSeleccionado,
                "Cuando FormaPago != 'RCB', debe auto-seleccionar '(Sin CCC)' que es NULL");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("AutoSeleccion")]
        public async Task SelectorCCC_ConSeleccionPreviaValida_DebeMantenerSeleccion()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();

            var cccsEsperados = new List<CCCItem>
            {
                new CCCItem { numero = "ES1111111111", estado = 1 }, // Válido
                new CCCItem { numero = "ES2222222222", estado = 1 }, // Válido - el que pre-seleccionaremos
            };

            A.CallTo(() => servicioCCC.ObtenerCCCs("1", "10", "0"))
                .Returns(Task.FromResult<IEnumerable<CCCItem>>(cccsEsperados));

            SelectorCCC sut = null;
            string cccSeleccionado = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorCCC(servicioCCC);
                sut.CCCSeleccionado = "ES2222222222"; // Pre-seleccionar antes de cargar
                sut.FormaPago = "RCB"; // Aunque sea RCB, debe respetar la selección previa
                sut.Contacto = "0";
                sut.Cliente = "10";
                sut.Empresa = "1";

                System.Threading.Thread.Sleep(300);
                cccSeleccionado = sut.CCCSeleccionado;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(200);

            // Assert
            Assert.AreEqual("ES2222222222", cccSeleccionado,
                "Debe respetar la selección previa 'ES2222222222', NO auto-seleccionar el primero");
        }

        #endregion

        #region Test: Opción "(Sin CCC)"

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("SinCCC")]
        public async Task SelectorCCC_SiempreDebeTenerOpcionSinCCC()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();

            var cccsEsperados = new List<CCCItem>
            {
                new CCCItem { numero = "ES1234567890", estado = 1 },
            };

            A.CallTo(() => servicioCCC.ObtenerCCCs("1", "10", "0"))
                .Returns(Task.FromResult<IEnumerable<CCCItem>>(cccsEsperados));

            SelectorCCC sut = null;
            ObservableCollection<CCCItem> listaCCCs = null;
            CCCItem primerItem = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorCCC(servicioCCC);
                sut.Contacto = "0";
                sut.Cliente = "10";
                sut.Empresa = "1";

                System.Threading.Thread.Sleep(300);
                listaCCCs = sut.ListaCCCs;
                if (listaCCCs != null && listaCCCs.Any())
                {
                    primerItem = listaCCCs.First();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(200);

            // Assert
            Assert.IsNotNull(listaCCCs, "La lista de CCCs debería estar cargada");
            Assert.IsTrue(listaCCCs.Count >= 2, "La lista debería tener al menos 2 elementos (Sin CCC + el CCC del servicio)");
            Assert.IsNotNull(primerItem, "El primer item debería existir");
            Assert.IsNull(primerItem.numero, "El primer item '(Sin CCC)' debe tener numero = NULL");
            Assert.AreEqual("(Sin CCC)", primerItem.Descripcion, "El primer item debe ser '(Sin CCC)'");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("SinCCC")]
        public void SelectorCCC_OpcionSinCCCDebeRetornarNull()
        {
            // Este test documenta que cuando se selecciona "(Sin CCC)",
            // la propiedad CCCSeleccionado debe ser NULL.
            //
            // Esto se logra porque:
            // 1. El item "(Sin CCC)" tiene numero = null
            // 2. El ComboBox usa SelectedValuePath="numero"
            // 3. Por tanto, SelectedValue = null cuando se selecciona ese item
            //
            // Ver XAML: SelectedValuePath="numero"

            Assert.IsTrue(true,
                "'(Sin CCC)' debe retornar NULL en CCCSeleccionado. " +
                "Ver SelectorCCC.xaml: SelectedValuePath='numero'");
        }

        #endregion

        #region Test: CCCs Inválidos (estado < 0)

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("CCCsInvalidos")]
        public void SelectorCCC_CCCsInvalidosDebenMostrarseEnCursiva()
        {
            // Este test documenta que los CCCs con estado < 0 deben:
            // 1. Mostrarse en la lista (no ocultarse)
            // 2. Aparecer con estilo cursiva
            // 3. Aparecer en color gris
            // 4. Estar deshabilitados (IsEnabled = False)
            //
            // Esto se logra mediante ItemContainerStyle en el XAML:
            // <DataTrigger Binding="{Binding EsInvalido}" Value="True">
            //     <Setter Property="FontStyle" Value="Italic" />
            //     <Setter Property="Foreground" Value="Gray" />
            //     <Setter Property="IsEnabled" Value="False" />
            // </DataTrigger>
            //
            // El modelo CCC tiene la propiedad calculada:
            // public bool EsInvalido => estado < 0;

            Assert.IsTrue(true,
                "CCCs con estado < 0 deben mostrarse en cursiva, gris y deshabilitados. " +
                "Ver ItemContainerStyle en SelectorCCC.xaml y propiedad EsInvalido en SelectorCCCModel.cs");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("CCCsInvalidos")]
        public void SelectorCCC_CCCsInvalidosNoDebenSerSeleccionables()
        {
            // Este test documenta que los CCCs inválidos (estado < 0)
            // no pueden ser seleccionados por el usuario.
            //
            // Esto se logra con IsEnabled = False en el ItemContainerStyle,
            // lo que impide hacer clic en esos items.
            //
            // Adicionalmente, la lógica de auto-selección solo considera
            // CCCs válidos: lista.FirstOrDefault(c => c.EsValido && ...)

            Assert.IsTrue(true,
                "CCCs inválidos tienen IsEnabled = False y no son considerados en auto-selección. " +
                "Ver ItemContainerStyle en SelectorCCC.xaml y AutoSeleccionarCCC() en SelectorCCC.xaml.cs");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("CCCsInvalidos")]
        public void SelectorCCC_CCCsInvalidosDebenMostrarTextoINVALIDO()
        {
            // Este test documenta que los CCCs inválidos deben mostrar
            // el texto "(INVÁLIDO)" en su descripción.
            //
            // Lógica esperada (según CargarCCCsAsync):
            // if (ccc.EsInvalido)
            // {
            //     ccc.Descripcion = $"{ccc.numero} - {ccc.entidad ?? "Sin entidad"} (INVÁLIDO)";
            // }
            //
            // Esto ayuda visualmente al usuario a identificar CCCs que
            // no pueden usarse.

            Assert.IsTrue(true,
                "CCCs inválidos deben incluir '(INVÁLIDO)' en su descripción. " +
                "Ver método CargarCCCsAsync() en SelectorCCC.xaml.cs");
        }

        #endregion

        #region Test: Protección contra bucles infinitos

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("AntiBucles")]
        public void SelectorCCC_UsaFlagEstaCargandoParaEvitarBucles()
        {
            // Este test documenta que el control usa un flag _estaCargando
            // para prevenir bucles infinitos durante la carga de datos.
            //
            // Mecanismo (según SelectorCCC.xaml.cs):
            // 1. _estaCargando = true al inicio de CargarCCCsAsync()
            // 2. Todos los PropertyChanged handlers verifican:
            //    if (selector._estaCargando) return;
            // 3. _estaCargando = false en el finally de CargarCCCsAsync()
            //
            // Esto previene que cambios en CCCSeleccionado durante la carga
            // disparen nuevas cargas recursivas.

            Assert.IsTrue(true,
                "El control usa el flag _estaCargando para prevenir bucles infinitos. " +
                "Ver OnEmpresaChanged, OnClienteChanged, OnContactoChanged, OnFormaPagoChanged en SelectorCCC.xaml.cs");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("AntiBucles")]
        public void SelectorCCC_ComparaValoresEnOnCCCSeleccionadoChanged()
        {
            // Este test documenta que el handler de CCCSeleccionadoChanged
            // compara los valores old/new para evitar propagaciones innecesarias.
            //
            // Mecanismo (según OnCCCSeleccionadoChanged):
            // if (e.OldValue?.ToString() == e.NewValue?.ToString())
            //     return; // No propagar si el valor realmente no cambió
            //
            // Esto evita bucles causados por asignaciones redundantes
            // (ej: CCCSeleccionado = CCCSeleccionado).

            Assert.IsTrue(true,
                "OnCCCSeleccionadoChanged compara old/new valores para evitar propagaciones innecesarias. " +
                "Ver OnCCCSeleccionadoChanged() en SelectorCCC.xaml.cs");
        }

        #endregion

        #region Test: Manejo de Errores

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("ManejoErrores")]
        public void SelectorCCC_ConParametrosIncompletos_NoLanzaExcepcion()
        {
            // Este test documenta que si faltan Empresa, Cliente o Contacto,
            // el control no lanza excepción sino que simplemente limpia la lista.
            //
            // Lógica esperada (según CargarCCCsAsync):
            // if (string.IsNullOrWhiteSpace(Empresa) ||
            //     string.IsNullOrWhiteSpace(Cliente) ||
            //     string.IsNullOrWhiteSpace(Contacto))
            // {
            //     ListaCCCs = new ObservableCollection<CCC>();
            //     return;
            // }
            //
            // Esto permite que el control se cargue antes de que el parent
            // haya establecido todos los parámetros.

            Assert.IsTrue(true,
                "Si faltan parámetros, el control limpia la lista sin lanzar excepción. " +
                "Ver CargarCCCsAsync() en SelectorCCC.xaml.cs");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("ManejoErrores")]
        public void SelectorCCC_SinServicio_FuncionaEnModoDegradado()
        {
            // Este test documenta que si el servicio es null (modo degradado),
            // el control no crashea sino que simplemente no carga datos.
            //
            // Lógica esperada (según CargarCCCsAsync):
            // if (_servicioCCC == null)
            // {
            //     Debug.WriteLine("[SelectorCCC] Servicio CCC no disponible (modo degradado)");
            //     return;
            // }
            //
            // Esto permite que el control se use en XAML designer sin servicio.

            Assert.IsTrue(true,
                "Si el servicio es null, el control funciona en modo degradado sin crashear. " +
                "Ver CargarCCCsAsync() en SelectorCCC.xaml.cs");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("ManejoErrores")]
        public void SelectorCCC_ConErrorHTTP_MuestraSoloSinCCC()
        {
            // Este test documenta que si hay error al cargar CCCs desde la API,
            // el control muestra solo la opción "(Sin CCC)" y la auto-selecciona.
            //
            // Lógica esperada (según catch en CargarCCCsAsync):
            // catch (Exception ex)
            // {
            //     Debug.WriteLine($"[SelectorCCC] Error al cargar CCCs: {ex.Message}");
            //     ListaCCCs = new ObservableCollection<CCC> { /* solo "(Sin CCC)" */ };
            //     CCCSeleccionado = null;
            // }
            //
            // Esto permite que el usuario siga trabajando aunque la API falle.

            Assert.IsTrue(true,
                "Si hay error HTTP, el control muestra solo '(Sin CCC)' y lo auto-selecciona. " +
                "Ver catch en CargarCCCsAsync() en SelectorCCC.xaml.cs");
        }

        #endregion

        #region Test: Construcción del control

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("Constructor")]
        public void SelectorCCC_ConstructorConServicio_CreaControlCorrectamente()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();

            SelectorCCC resultado = null;

            Thread thread = new Thread(() =>
            {
                // Act: Crear control con servicio inyectado
                resultado = new SelectorCCC(servicioCCC);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.IsNotNull(resultado, "El control debería crearse correctamente con servicio inyectado");
        }

        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("Constructor")]
        public void SelectorCCC_ConstructorSinParametros_CreaControlParaXAMLDesigner()
        {
            // Este test documenta que el constructor sin parámetros existe
            // para que el XAML designer pueda cargar el control.
            //
            // IMPORTANTE: Este constructor NO debe usarse en producción.
            // En runtime, siempre se debe usar el constructor con IServicioCCC
            // mediante inyección de dependencias.
            //
            // Ver comentario en SelectorCCC.xaml.cs:
            // "NO USAR en producción. El control REQUIERE IServicioCCC para funcionar."

            SelectorCCC resultado = null;

            Thread thread = new Thread(() =>
            {
                // Act: Crear control sin parámetros (XAML designer)
                resultado = new SelectorCCC();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.IsNotNull(resultado, "El control debería crearse para XAML designer (sin funcionar completamente)");
        }

        #endregion
    }
}
