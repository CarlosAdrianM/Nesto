using System.Globalization;
using System.Text.RegularExpressions;

namespace Nesto.Modulos.Cajas.Models.ReglasContabilizacion
{
    internal class FuncionesAuxiliaresReglas
    {
        internal static string FormatearConcepto(string concepto)
        {
            TextInfo ti = CultureInfo.CurrentCulture.TextInfo;
            concepto = ti.ToTitleCase(concepto.ToLower());
            concepto = concepto.Trim();
            concepto = Regex.Replace(concepto, @"\s+", " ");
            if (concepto.Length > 50)
            {
                concepto = concepto.Substring(0, 50);
            }
            return concepto;
        }

        internal static string UltimosDiezCaracteres(string referenciaCompleta)
        {
            referenciaCompleta = referenciaCompleta.Trim();
            int longitud = referenciaCompleta.Length;
            int caracteresDeseados = 10;
            string ultimos10Caracteres;
            if (longitud >= caracteresDeseados)
            {
                ultimos10Caracteres = referenciaCompleta.Substring(longitud - caracteresDeseados);
            }
            else
            {
                // Manejar el caso donde la cadena es menor a 10 caracteres si es necesario
                ultimos10Caracteres = referenciaCompleta;
            }

            return ultimos10Caracteres;
        }
    }
}
