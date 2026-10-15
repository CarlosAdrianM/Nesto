using Nesto.Infrastructure.Contracts;
using System;

namespace Nesto.Infrastructure.Composicion
{
    /// <summary>
    /// Nesto#490 (4C.5): las sobrecargas genéricas de <see cref="IRegistroNesto"/>, iguales para todos los contenedores:
    /// cada una llama a la que recibe los tipos.
    /// </summary>
    public abstract class RegistroNestoBase : IRegistroNesto
    {
        public abstract void Transitorio(Type servicio, Type implementacion);
        public abstract void Singleton(Type servicio, Type implementacion);
        public abstract void Singleton(Type servicio, Func<IResolutorNesto, object> fabrica);
        public abstract void Instancia(Type servicio, object instancia);
        public abstract void Vista(string nombre, Type tipoVista);
        public abstract void Dialogo(string nombre, DialogoRegistrado dialogo);

        public void Transitorio<TServicio, TImplementacion>() where TImplementacion : TServicio
            => Transitorio(typeof(TServicio), typeof(TImplementacion));

        public void Transitorio<T>() where T : class
            => Transitorio(typeof(T), typeof(T));

        public void Singleton<TServicio, TImplementacion>() where TImplementacion : TServicio
            => Singleton(typeof(TServicio), typeof(TImplementacion));

        public void Singleton<TServicio>(Func<IResolutorNesto, TServicio> fabrica)
        {
            if (fabrica == null)
            {
                throw new ArgumentNullException(nameof(fabrica));
            }
            Singleton(typeof(TServicio), resolutor => fabrica(resolutor));
        }

        public void Instancia<TServicio>(TServicio instancia)
            => Instancia(typeof(TServicio), instancia);

        public void Vista<TVista>(string nombre) where TVista : class
            => Vista(nombre, typeof(TVista));

        protected static void ComprobarNombre(string nombre, string que)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                throw new ArgumentException($"{que} necesita un nombre", nameof(nombre));
            }
        }
    }
}
