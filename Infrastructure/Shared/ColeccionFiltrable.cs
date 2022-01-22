using Nesto.Infrastructure.Contracts;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nesto.Infrastructure.Shared
{
    /*
    public class ColeccionFiltrable<T> : BindableBase where T : IFiltrableItem
    {
        #region Constructores
        public ColeccionFiltrable() : base()
        {
            FijarFiltroCommand = new DelegateCommand<string>(OnFijarFiltro);
            QuitarFiltroCommand = new DelegateCommand<string>(OnQuitarFiltro);
        }

        public ColeccionFiltrable(IEnumerable<T> enumerable) : this()
        {
            ListaOriginal = new ObservableCollection<T>(enumerable);
        }

        public ColeccionFiltrable(params T[] collection) : this(collection as IEnumerable<T>) { }
        #endregion

        #region Propiedades
        
        private T _elementoSeleccionado;
        public T ElementoSeleccionado
        {
            get => _elementoSeleccionado;
            set
            {
                T oldElementoSeleccionado = _elementoSeleccionado;
                _elementoSeleccionado = value;
                ElementoSeleccionadoChanged?.Invoke(oldElementoSeleccionado, _elementoSeleccionado);
                SetProperty(ref _elementoSeleccionado, value);
            }
        }

        private string _filtro;
        public string Filtro
        {
            get => _filtro;
            set
            {
                SetProperty(ref _filtro, value);
                Lista = AplicarFiltro(value);
            }
        }

        private ObservableCollection<string> _filtrosPuestos;
        public ObservableCollection<string> FiltrosPuestos {
            get => _filtrosPuestos;
            set => SetProperty(ref _filtrosPuestos, value);
        }
        private ObservableCollection<T> _lista;
        public ObservableCollection<T> Lista
        {
            get => _lista;
            set => SetProperty(ref _lista, value);
        }
        private ObservableCollection<T> _listaFijada;
        public ObservableCollection<T> ListaFijada {
            get => _listaFijada;
            set
            {
                SetProperty(ref _listaFijada, value);
                Lista = value;
            }
        }
        private ObservableCollection<T> _listaOriginal;
        public ObservableCollection<T> ListaOriginal {
            get => _listaOriginal;
            set
            {
                SetProperty(ref _listaOriginal, value);
                ListaFijada = value;
                //if (value == null || !value.Any())
                //{
                //    FiltrosPuestos.Clear();
                //}
                //FiltrosPuestos.Clear();
            }
        }
        public bool TieneDatosIniciales { get; set; }
        #endregion

        #region Funciones
        internal ObservableCollection<T> AplicarFiltro(string filtro)
        {
            if (ListaFijada == null)
            {
                return new ObservableCollection<T>();
            }
            if (string.IsNullOrEmpty(filtro))
            {
                return ListaFijada;
            }
            return new ObservableCollection<T>(ListaFijada.Where(l => l.Contains(filtro)));
        }

        public void RefrescarFiltro()
        {
            Lista = AplicarFiltro(Filtro);
        }
        #endregion

        #region Comandos
        public DelegateCommand<string> FijarFiltroCommand { get; private set; }
        private void OnFijarFiltro(string filtro)
        {
            if (FiltrosPuestos == null)
            {
                FiltrosPuestos = new();
            }
            if (!string.IsNullOrEmpty(filtro))
            {
                if (Lista == null || !Lista.Any())
                {
                    if (!TieneDatosIniciales)
                    {
                        FiltrosPuestos.Clear();
                        HayQueCargarDatos(); //emite el evento
                    }
                }
                else
                {
                    Lista = AplicarFiltro(filtro);
                    ListaFijada = Lista;
                }
                FiltrosPuestos.Add(filtro);
            }
            else
            {
                FiltrosPuestos.Clear();
                ListaFijada = ListaOriginal;
            }
        }

        public DelegateCommand<string> QuitarFiltroCommand { get; private set; }
        private void OnQuitarFiltro(string filtro)
        {
            filtro = filtro.ToLower();
            if (!FiltrosPuestos.Any() || FiltrosPuestos.Count == 1)
            {
                FiltrosPuestos.Clear();
                if (TieneDatosIniciales)
                {
                    ListaFijada = ListaOriginal;
                }
                else
                {
                    ListaOriginal.Clear();
                }
            }
            else
            {
                FiltrosPuestos.Remove(filtro);
                ListaFijada = ListaOriginal;
                foreach (string filtroPuesto in FiltrosPuestos)
                {
                    ListaFijada = AplicarFiltro(filtroPuesto);
                }
            }
        }
        #endregion

        #region Eventos
        public delegate void ElementoSeleccionadoChange(T oldItem, T newItem);
        public virtual ElementoSeleccionadoChange ElementoSeleccionadoChanged { get; set; }
        public event Action HayQueCargarDatos;
        #endregion
    }
    */
    public class ColeccionFiltrable : BindableBase
    {
        #region Constructores
        public ColeccionFiltrable() : base()
        {
            FijarFiltroCommand = new DelegateCommand<string>(OnFijarFiltro);
            QuitarFiltroCommand = new DelegateCommand<string>(OnQuitarFiltro);
        }

        public ColeccionFiltrable(IEnumerable<IFiltrableItem> enumerable) : this()
        {
            ListaOriginal = new ObservableCollection<IFiltrableItem>(enumerable);
        }

        public ColeccionFiltrable(params IFiltrableItem[] collection) : this(collection as IEnumerable<IFiltrableItem>) { }
        #endregion

        #region Propiedades

        private IFiltrableItem _elementoSeleccionado;
        public IFiltrableItem ElementoSeleccionado
        {
            get => _elementoSeleccionado;
            set
            {
                IFiltrableItem oldElementoSeleccionado = _elementoSeleccionado;
                _elementoSeleccionado = value;
                ElementoSeleccionadoChangedEventArgs args = new();
                args.OldValue = oldElementoSeleccionado;
                args.NewValue = value;
                OnElementoSeleccionadoChanged(args);
                SetProperty(ref _elementoSeleccionado, value);
            }
        }

        private string _filtro;
        public string Filtro
        {
            get => _filtro;
            set
            {
                string oldValue = _filtro;
                SetProperty(ref _filtro, value);
                Lista = AplicarFiltro(value);
                FiltroChangedEventArgs args = new();
                args.OldValue = oldValue;
                args.NewValue = value;
                OnFiltroChanged(args);
            }
        }

        private ObservableCollection<string> _filtrosPuestos;
        public ObservableCollection<string> FiltrosPuestos
        {
            get => _filtrosPuestos;
            set => SetProperty(ref _filtrosPuestos, value);
        }
        private ObservableCollection<IFiltrableItem> _lista;
        public ObservableCollection<IFiltrableItem> Lista
        {
            get => _lista;
            set => SetProperty(ref _lista, value);
        }
        private ObservableCollection<IFiltrableItem> _listaFijada;
        public ObservableCollection<IFiltrableItem> ListaFijada
        {
            get => _listaFijada;
            set
            {
                SetProperty(ref _listaFijada, value);
                Lista = value;
                if (TieneDatosIniciales && value != null)
                {
                    ElementoSeleccionado = value.FirstOrDefault();
                }
            }
        }
        private ObservableCollection<IFiltrableItem> _listaOriginal;
        public ObservableCollection<IFiltrableItem> ListaOriginal
        {
            get => _listaOriginal;
            set
            {
                SetProperty(ref _listaOriginal, value);
                ListaFijada = value;
                if (value != null && value.Count == 1)
                {
                    ElementoSeleccionado = value.First();
                }
            }
        }
        public bool TieneDatosIniciales { get; set; }
        #endregion

        #region Funciones
        internal ObservableCollection<IFiltrableItem> AplicarFiltro(string filtro)
        {
            if (ListaFijada == null)
            {
                return new ObservableCollection<IFiltrableItem>();
            }
            if (string.IsNullOrEmpty(filtro))
            {
                return ListaFijada;
            }
            else
            {
                filtro = filtro.ToLower();
            }
            return new ObservableCollection<IFiltrableItem>(ListaFijada.Where(l => l.Contains(filtro)));
        }

        public void RefrescarFiltro()
        {
            Lista = AplicarFiltro(Filtro);
        }
        #endregion

        #region Comandos
        public DelegateCommand<string> FijarFiltroCommand { get; private set; }
        private void OnFijarFiltro(string filtro)
        {
            if (FiltrosPuestos == null)
            {
                FiltrosPuestos = new();
            }
            if (!string.IsNullOrEmpty(filtro))
            {
                if (Lista == null || !Lista.Any())
                {
                    if (!TieneDatosIniciales)
                    {
                        FiltrosPuestos.Clear();
                    }
                    HayQueCargarDatos(); //emite el evento
                }
                else
                {
                    Lista = AplicarFiltro(filtro);
                    ListaFijada = Lista;
                }
                FiltrosPuestos.Add(filtro);
            }
            else
            {
                FiltrosPuestos.Clear();
                ListaFijada = ListaOriginal;
            }
        }

        public DelegateCommand<string> QuitarFiltroCommand { get; private set; }
        private void OnQuitarFiltro(string filtro)
        {
            filtro = filtro.ToLower();
            if (Filtro.ToLower() == filtro)
            {
                Filtro = string.Empty;
            }
            if (!FiltrosPuestos.Any() || FiltrosPuestos.Count == 1)
            {
                FiltrosPuestos.Clear();              
                if (TieneDatosIniciales)
                {
                    ListaFijada = ListaOriginal;
                }
                else
                {
                    ListaOriginal = new(); // si hacemos Clear() no actualiza ListaFijada ni Lista
                }
            }
            else
            {
                FiltrosPuestos.Remove(filtro);
                ListaFijada = ListaOriginal;
                foreach (string filtroPuesto in FiltrosPuestos)
                {
                    if (TieneDatosIniciales || FiltrosPuestos.IndexOf(filtroPuesto) != 0)
                    {
                        ListaFijada = AplicarFiltro(filtroPuesto);
                    }
                }
            }
        }
        #endregion

        #region Eventos
        public event Action HayQueCargarDatos;
        
        public event EventHandler<ElementoSeleccionadoChangedEventArgs> ElementoSeleccionadoChanged;
        protected virtual void OnElementoSeleccionadoChanged(ElementoSeleccionadoChangedEventArgs e)
        {
            EventHandler<ElementoSeleccionadoChangedEventArgs> handler = ElementoSeleccionadoChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        


        public event EventHandler<FiltroChangedEventArgs> FiltroChanged;

        protected virtual void OnFiltroChanged(FiltroChangedEventArgs e)
        {
            EventHandler<FiltroChangedEventArgs> handler = FiltroChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        #endregion
    }
}
