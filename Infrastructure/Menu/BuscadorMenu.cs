using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Nesto.Infrastructure.Menu
{
    /// <summary>Cómo ha acabado una búsqueda: abriendo una pantalla o cancelada (Esc, o el foco se fue a otro sitio).</summary>
    public sealed class BusquedaTerminadaEventArgs : EventArgs
    {
        public BusquedaTerminadaEventArgs(ResultadoBusquedaMenu ejecutado)
        {
            Ejecutado = ejecutado;
        }

        /// <summary>Lo que se ha abierto (null si se ha cancelado).</summary>
        public ResultadoBusquedaMenu Ejecutado { get; }
    }

    /// <summary>
    /// Nesto#490 (panel lateral, tramo 2): el ViewModel de «Buscar pantalla…» (C# puro, sin WPF-UI). El panel del
    /// ejecutable pinta la caja y la lista y le pasa las teclas; aquí está lo que hacen:
    /// <list type="bullet">
    /// <item><see cref="ActivarCommand"/> (Ctrl+K en la ventana principal): pide al panel que ponga el foco en la caja
    /// (<see cref="ActivacionPedida"/>); con el panel compacto, el panel se abre solo mientras se busca.</item>
    /// <item>Flechas: <see cref="Mover"/>. Intro: <see cref="Ejecutar"/> el seleccionado (o el primero). Esc:
    /// <see cref="Cancelar"/>. Las dos terminan la búsqueda (<see cref="Terminada"/>): el panel vuelve a compacto si lo
    /// abrió para buscar y devuelve el foco a la pestaña activa.</item>
    /// <item>Un error al abrir la pantalla va a ELMAH (contexto «NavegacionLateral: …») y se avisa; nunca tumba Nesto.</item>
    /// </list>
    /// </summary>
    public sealed class BuscadorMenu : ObservableObject
    {
        public const string CONTEXTO_ERRORES = "NavegacionLateral";

        private readonly Func<IEnumerable<(ElementoMenu Elemento, string Seccion)>> _candidatos;
        private readonly Action<Exception, string> _registrarError;
        private string _texto = string.Empty;
        private ResultadoBusquedaMenu _seleccionado;

        /// <param name="catalogo">El catálogo del menú lateral (todas sus secciones y el pie).</param>
        /// <param name="registrarError">Error y contexto (en Nesto, a ELMAH). Puede ser null.</param>
        public BuscadorMenu(CatalogoMenu catalogo, Action<Exception, string> registrarError = null)
            : this(() => FiltroCatalogoMenu.Candidatos(catalogo), registrarError)
        {
        }

        public BuscadorMenu(Func<IEnumerable<(ElementoMenu Elemento, string Seccion)>> candidatos, Action<Exception, string> registrarError = null)
        {
            _candidatos = candidatos ?? throw new ArgumentNullException(nameof(candidatos));
            _registrarError = registrarError;
            ActivarCommand = new RelayCommand(Activar);
        }

        /// <summary>Ctrl+K: poner el foco en la búsqueda.</summary>
        public IRelayCommand ActivarCommand { get; }

        /// <summary>El panel pone el foco en la caja (y se abre si está compacto).</summary>
        public event EventHandler ActivacionPedida;

        /// <summary>Se ha abierto una pantalla o se ha cancelado: el panel vuelve a como estaba y devuelve el foco.</summary>
        public event EventHandler<BusquedaTerminadaEventArgs> Terminada;

        /// <summary>No se ha podido abrir la pantalla (ya registrado): el panel avisa al usuario con este texto.</summary>
        public event EventHandler<string> ErrorAlEjecutar;

        /// <summary>Lo escrito en la caja. Cada cambio vuelve a filtrar y selecciona el primero.</summary>
        public string Texto
        {
            get => _texto;
            set
            {
                if (SetProperty(ref _texto, value ?? string.Empty))
                {
                    Filtrar();
                }
            }
        }

        public ObservableCollection<ResultadoBusquedaMenu> Resultados { get; } = new ObservableCollection<ResultadoBusquedaMenu>();

        public ResultadoBusquedaMenu Seleccionado
        {
            get => _seleccionado;
            set => SetProperty(ref _seleccionado, value);
        }

        /// <summary>Hay algo escrito (la lista de resultados se ve).</summary>
        public bool HayBusqueda => !string.IsNullOrWhiteSpace(_texto);

        /// <summary>Hay algo escrito y no sale nada.</summary>
        public bool SinResultados => HayBusqueda && Resultados.Count == 0;

        /// <summary>«No hay ninguna pantalla con «…»»: lo que se enseña cuando no sale nada.</summary>
        public string TextoSinResultados => $"No hay ninguna pantalla con «{_texto?.Trim()}»";

        private void Activar() => ActivacionPedida?.Invoke(this, EventArgs.Empty);

        /// <summary>Vuelve a filtrar (también si ha cambiado lo que el usuario puede usar).</summary>
        public void Filtrar()
        {
            IReadOnlyList<ResultadoBusquedaMenu> nuevos;
            try
            {
                nuevos = FiltroCatalogoMenu.Filtrar(_candidatos(), _texto);
            }
            catch (Exception ex)
            {
                Registrar(ex, "filtrar la búsqueda");
                nuevos = Array.Empty<ResultadoBusquedaMenu>();
            }
            Resultados.Clear();
            foreach (ResultadoBusquedaMenu resultado in nuevos)
            {
                Resultados.Add(resultado);
            }
            Seleccionado = Resultados.FirstOrDefault();
            OnPropertyChanged(nameof(HayBusqueda));
            OnPropertyChanged(nameof(SinResultados));
            OnPropertyChanged(nameof(TextoSinResultados));
        }

        /// <summary>Flecha abajo (+1) o arriba (-1). Da la vuelta al llegar al final o al principio.</summary>
        public void Mover(int desplazamiento)
        {
            if (Resultados.Count == 0)
            {
                return;
            }
            int actual = Seleccionado == null ? -1 : Resultados.IndexOf(Seleccionado);
            if (actual < 0)
            {
                Seleccionado = desplazamiento >= 0 ? Resultados[0] : Resultados[Resultados.Count - 1];
                return;
            }
            int nuevo = ((actual + desplazamiento) % Resultados.Count + Resultados.Count) % Resultados.Count;
            Seleccionado = Resultados[nuevo];
        }

        /// <summary>
        /// Intro (o clic en un resultado): abre <paramref name="resultado"/> o, si es null, el seleccionado (o el primero).
        /// Primero termina la búsqueda (el panel vuelve a compacto) y después ejecuta el MISMO comando que la cinta.
        /// Devuelve si lo ha ejecutado.
        /// </summary>
        public bool Ejecutar(ResultadoBusquedaMenu resultado = null)
        {
            resultado ??= Seleccionado ?? Resultados.FirstOrDefault();
            if (resultado == null)
            {
                return false;
            }
            ElementoMenu elemento = resultado.Elemento;
            ICommand comando = elemento.Comando;
            object parametro = elemento.ParametroComando;
            bool sePuede;
            try
            {
                sePuede = comando?.CanExecute(parametro) == true;
            }
            catch (Exception ex)
            {
                Registrar(ex, $"comprobar si se puede abrir «{resultado.Titulo}» desde la búsqueda");
                sePuede = false;
            }
            if (!sePuede)
            {
                Filtrar(); // ya no se puede usar: que deje de salir
                return false;
            }

            Limpiar();
            Terminar(resultado);
            try
            {
                comando.Execute(parametro);
                return true;
            }
            catch (Exception ex)
            {
                Registrar(ex, $"abrir «{resultado.Titulo}» desde la búsqueda");
                try
                {
                    ErrorAlEjecutar?.Invoke(this, $"No se ha podido abrir «{resultado.Titulo}». Se ha registrado el error para revisarlo.\n\n{ex.Message}");
                }
                catch
                {
                    // El aviso nunca rompe nada
                }
                return false;
            }
        }

        /// <summary>Esc: borra lo escrito y termina la búsqueda.</summary>
        public void Cancelar()
        {
            Limpiar();
            Terminar(null);
        }

        private void Limpiar()
        {
            Texto = string.Empty;
        }

        private void Terminar(ResultadoBusquedaMenu ejecutado)
        {
            try
            {
                Terminada?.Invoke(this, new BusquedaTerminadaEventArgs(ejecutado));
            }
            catch (Exception ex)
            {
                Registrar(ex, "terminar la búsqueda");
            }
        }

        private void Registrar(Exception error, string queHacia)
        {
            try
            {
                _registrarError?.Invoke(error, CONTEXTO_ERRORES + ": " + queHacia);
            }
            catch
            {
                // El registro de errores nunca rompe el panel
            }
        }
    }
}
