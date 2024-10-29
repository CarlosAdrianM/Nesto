using Nesto.Infrastructure.Contracts;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Nesto.Infrastructure.Shared
{
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
            if (enumerable != null && enumerable.Any())
            {
                TieneDatosIniciales = true;
            }
            ListaOriginal = new ObservableCollection<IFiltrableItem>(enumerable);
        }

        public ColeccionFiltrable(params IFiltrableItem[] collection) : this(collection as IEnumerable<IFiltrableItem>) { }
        #endregion

        #region Propiedades

        public string CamposFiltrables
        {
            get
            {
                if (ListaOriginal == null)
                {
                    return "No hay ningún campo filtrable";
                }
                string textoCampos = "La lista de campos filtrables es: \n";
                var primerElemento = ListaOriginal.FirstOrDefault();
                if (primerElemento == null) {
                    return "No hay ningún campo filtrable";
                }
                Type tipo = primerElemento.GetType();
                var campos = tipo.GetProperties();
                foreach (var campo in campos)
                {
                    textoCampos += "  - " + campo.Name + "\n";
                }
                return textoCampos;
            }
        }

        private IFiltrableItem _elementoSeleccionado;
        public IFiltrableItem ElementoSeleccionado
        {
            get => _elementoSeleccionado;
            set
            {
                if (_elementoSeleccionado == value) {
                    return;
                }
                IFiltrableItem oldElementoSeleccionado = _elementoSeleccionado;                
                ElementoSeleccionadoChangingEventArgs changingArgs = new()
                {
                    OldValue = oldElementoSeleccionado,
                    NewValue = value
                };
                OnElementoSeleccionadoChanging(changingArgs);
                //_elementoSeleccionado = value;
                SetProperty(ref _elementoSeleccionado, value);
                ElementoSeleccionadoChangedEventArgs changedArgs = new()
                {
                    OldValue = oldElementoSeleccionado,
                    NewValue = value
                };
                OnElementoSeleccionadoChanged(changedArgs);
                
                if (!TieneDatosIniciales && VaciarAlSeleccionar)
                {
                    Reiniciar();
                }
            }
        }

        public void Reiniciar()
        {
            ListaOriginal = new();
            FiltrosPuestos?.Clear();
        }

        private string _filtro;
        public string Filtro
        {
            get => _filtro;
            set
            {
                string oldValue = _filtro;
                SetProperty(ref _filtro, value); // ¿value.toLower()?
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
                if (SeleccionarPrimerElemento && TieneDatosIniciales && value != null && value.Any())
                {
                    ElementoSeleccionado = value.First();
                }
                if (value != null && value.Count == 1)
                {
                    ElementoSeleccionado = value.First();
                }
                if (ListaChanged != null)
                {
                    ListaChanged();
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
                RaisePropertyChanged(nameof(CamposFiltrables));
            }
        }

        // Determina si queremos que seleccione el primer elemento por defecto o no
        public bool SeleccionarPrimerElemento { get; set; } = true;

        public bool TieneDatosIniciales { get; set; }

        // Hay sitios donde al seleccionar un elemento hay que vacíar las listas
        public bool VaciarAlSeleccionar { get; set; }
        #endregion

        #region Funciones
        internal ObservableCollection<IFiltrableItem> AplicarFiltro(string filtro)
        {
            if (ListaFijada == null)
            {
                return new ObservableCollection<IFiltrableItem>();
            }
            if (string.IsNullOrEmpty(filtro) || filtro == "-")
            {
                return ListaFijada;
            }
            else
            {
                filtro = FormatearFiltro(filtro);
            }
            if (filtro.Contains(":"))
            {
                var partes = filtro.Split(':');
                if (partes.Count() != 2 || string.IsNullOrEmpty(partes[0]) || string.IsNullOrEmpty(partes[1]))
                {
                    return ListaFijada;
                }
                
                var nombreCampo = partes[0];
                var valorCampo = partes[1];
                bool esFiltroNegativo = filtro.StartsWith("-");

                if (esFiltroNegativo)
                {
                    nombreCampo = nombreCampo.Substring(1).Trim();
                }

                try
                {
                    return new ObservableCollection<IFiltrableItem>(ListaFijada.Where(l => {
                        var valorPropiedad = l.GetType().GetProperty(nombreCampo, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance).GetValue(l);
                        if (valorPropiedad == null)
                        {
                            return false;
                        }
                        bool esIgual = valorPropiedad.ToString().ToLower().Equals(valorCampo);

                        return esFiltroNegativo ? !esIgual : esIgual;
                    }
                    ));
                }
                catch
                {
                    return ListaFijada;
                }
            }
            else if (filtro.StartsWith("-"))
            {
                return new ObservableCollection<IFiltrableItem>(ListaFijada.Where(l => !l.Contains(filtro.Substring(1))));
            } 
            else if (filtro.Contains("|"))
            {
                var partes = filtro.Split('|');
                ObservableCollection<IFiltrableItem> listaJunta = new();
                foreach (var parte in partes)
                {
                    listaJunta = new ObservableCollection<IFiltrableItem>(listaJunta.Union(ListaFijada.Where(l => l.Contains(parte.Trim()))));
                }
                return listaJunta;
            }

            return new ObservableCollection<IFiltrableItem>(ListaFijada.Where(l => l.Contains(filtro)));
        }

        private string FormatearFiltro(string filtro)
        {
            string filtroFormateado = filtro.ToLower();
            if (filtroFormateado.Contains("|"))
            {
                var partes = filtroFormateado.Split('|');
                string nuevoFiltro = string.Empty;
                for (int i = 0; i < partes.Count(); i++)
                {
                    if (i == 0)
                    {
                        nuevoFiltro = partes[i];
                    }
                    else
                    {
                        nuevoFiltro += " | " + partes[i];
                    }
                }
                filtroFormateado = nuevoFiltro;
            }
            if (filtroFormateado.Contains(":"))
            {
                var partes = filtroFormateado.Split(':');
                if (partes.Count() == 2 && !string.IsNullOrEmpty(partes[0]) && !string.IsNullOrEmpty(partes[1]))
                {
                    string nuevoFiltro = partes[0].Trim() + ":" + partes[1].Trim();
                    
                    filtroFormateado = nuevoFiltro;
                }
            }
            return filtroFormateado;
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
                    if (HayQueCargarDatos != null)
                    {
                        HayQueCargarDatos(); //emite el evento
                    }
                }
                else
                {
                    Lista = AplicarFiltro(filtro);
                    ListaFijada = Lista;
                }
                FiltrosPuestos.Add(FormatearFiltro(filtro));
            }
            else
            {
                ElementoSeleccionado = null;
                FiltrosPuestos.Clear();
                ListaFijada = ListaOriginal;
            }
        }

        public DelegateCommand<string> QuitarFiltroCommand { get; private set; }
        private void OnQuitarFiltro(string filtro)
        {
            filtro = filtro.ToLower();
            if (Filtro != null && Filtro.ToLower() == filtro)
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

        public event EventHandler<ElementoSeleccionadoChangingEventArgs> ElementoSeleccionadoChanging;
        protected virtual void OnElementoSeleccionadoChanging(ElementoSeleccionadoChangingEventArgs e)
        {
            EventHandler<ElementoSeleccionadoChangingEventArgs> handler = ElementoSeleccionadoChanging;
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

        public event Action ListaChanged;
        #endregion
    }
}
