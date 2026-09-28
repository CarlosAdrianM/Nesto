using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;
using System.Collections.Generic;

namespace CajasTests
{
    /// <summary>
    /// Carlos 28/09/26: «Apunte contabilizado correctamente en asiento -1» sin que se contabilizara nada
    /// ni quedara rastro en ELMAH. La regla se elige con todo lo seleccionado pero contabiliza solo lo que
    /// no está completamente punteado; si eso queda vacío, ahora se explica y se registra.
    /// </summary>
    [TestClass]
    public class ContabilizarApunteSinLineasTests
    {
        [TestMethod]
        public void Diagnostico_ContabilidadYaPunteada_LoDiceConLosIds()
        {
            var banco = new List<ApunteBancarioDTO> { new ApunteBancarioDTO { Id = 501 } };
            var conta = new List<ContabilidadDTO> { new ContabilidadDTO { Id = 9001 } };

            string texto = BancosViewModel.DiagnosticoContabilizacionSinLineas("Pago proveedor",
                banco, banco, conta, new List<ContabilidadDTO>());

            StringAssert.StartsWith(texto, "Los apuntes de contabilidad seleccionados ya están punteados del todo.");
            StringAssert.Contains(texto, "Regla «Pago proveedor»");
            StringAssert.Contains(texto, "Banco: 1 seleccionados, 1 sin puntear del todo (Id 501)");
            StringAssert.Contains(texto, "Contabilidad: 1 seleccionados, 0 sin puntear del todo (Id 9001)");
        }

        [TestMethod]
        public void Diagnostico_BancoYaPunteado_LoDice()
        {
            var banco = new List<ApunteBancarioDTO> { new ApunteBancarioDTO { Id = 501 } };

            string texto = BancosViewModel.DiagnosticoContabilizacionSinLineas("Comisiones banco",
                banco, new List<ApunteBancarioDTO>(), null, null);

            StringAssert.StartsWith(texto, "Los movimientos del banco seleccionados ya están punteados del todo.");
        }

        [TestMethod]
        public void Diagnostico_ConTodoLibre_EsLaReglaLaQueNoGenera()
        {
            var banco = new List<ApunteBancarioDTO> { new ApunteBancarioDTO { Id = 501 } };
            var conta = new List<ContabilidadDTO> { new ContabilidadDTO { Id = 9001 } };

            string texto = BancosViewModel.DiagnosticoContabilizacionSinLineas("Aplázame", banco, banco, conta, conta);

            StringAssert.StartsWith(texto, "La regla no ha generado ningún apunte con lo seleccionado.");
        }
    }
}
