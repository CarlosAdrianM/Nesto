using System;

namespace Nesto.Infrastructure.Menu
{
    /// <summary>
    /// Nesto#655 (Carlos 26/11/26): un temporizador de UN solo disparo, cancelable. En Nesto es un DispatcherTimer (corre en el hilo de
    /// la UI); en las pruebas, uno falso que avanza a mano. Programar otra vez sustituye lo que hubiera pendiente.
    /// </summary>
    public interface ITemporizadorUnDisparo
    {
        /// <summary>Dentro de <paramref name="espera"/>, <paramref name="alVencer"/> (una vez). Cancela lo que hubiera pendiente.</summary>
        void Programar(TimeSpan espera, Action alVencer);

        /// <summary>Quita lo pendiente, si lo hay. Nunca lanza.</summary>
        void Cancelar();

        /// <summary>Si hay algo pendiente.</summary>
        bool Programado { get; }
    }
}
