﻿using Claytondus.AmazonMWS.Finances;
using Prism.Commands;
using Prism.Interactivity.InteractionRequest;
using Prism.Regions;
using Microsoft.VisualBasic;
using Nesto.Contratos;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.CanalesExternos.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows.Input;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalesExternosPagosViewModel : Contratos.ViewModelBase
    {
        private const string PROVEEDOR_AMAZON = "869";
        private const string CONTACTO_PROVEEDOR_AMAZON = "0";
        private const string BANCO_AMAZON = "57200013";
        private const string CUENTA_COMISIONES = "62300023";

        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        public CanalesExternosPagosViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            Titulo = "Canales Externos Pagos";

            CargarPagosCommand = new DelegateCommand(OnCargarPagos);
            CargarDetallePagoCommand = new DelegateCommand(OnCargarDetallePago);
            ContabilizarPagoCommand = new DelegateCommand(OnContabilizarPago, CanContabilizarPago);

            NotificationRequest = new InteractionRequest<INotification>();
            ConfirmationRequest = new InteractionRequest<IConfirmation>();
        }

        #region "Propiedades Prism"
        public InteractionRequest<INotification> NotificationRequest { get; private set; }
        public InteractionRequest<IConfirmation> ConfirmationRequest { get; private set; }
        private bool ResultadoConfirmacion { get; set; }
        #endregion

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get { return _estaOcupado; }
            set { SetProperty(ref _estaOcupado, value); }
        }

        private DateTime _fechaDesde = DateTime.Today.AddMonths(-2);
        public DateTime FechaDesde
        {
            get { return _fechaDesde; }
            set { SetProperty(ref _fechaDesde, value); }
        }

        private ObservableCollection<PagoCanalExterno> _listaPagos;
        public ObservableCollection<PagoCanalExterno> ListaPagos
        {
            get { return _listaPagos; }
            set { SetProperty(ref _listaPagos, value); }
        }

        private int _numeroMaxGruposEventos = 40;
        public int NumeroMaxGruposEventos
        {
            get { return _numeroMaxGruposEventos; }
            set { SetProperty(ref _numeroMaxGruposEventos, value); }
        }

        private PagoCanalExterno _pagoSeleccionado;
        public PagoCanalExterno PagoSeleccionado
        {
            get { return _pagoSeleccionado; }
            set { 
                SetProperty(ref _pagoSeleccionado, value);
                CargarDetallePagoCommand.Execute(null);
                ((DelegateCommand)ContabilizarPagoCommand).RaiseCanExecuteChanged();
            }
        }


        public ICommand CargarDetallePagoCommand { get; private set; }
        private async void OnCargarDetallePago()
        {
            if (PagoSeleccionado == null)
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                await Task.Run(() =>
                {
                    CabeceraDetallePagoCanalExterno cabeceraPago = MWSFinancesServiceSample.LeerFinancialEvents(PagoSeleccionado.PagoExternalId, NumeroMaxGruposEventos);
                    PagoSeleccionado.AjusteRetencion = cabeceraPago.AjusteRetencion;
                    PagoSeleccionado.RestoAjustes = cabeceraPago.RestoAjustes;
                    PagoSeleccionado.Comision = cabeceraPago.Comision;
                    PagoSeleccionado.Publicidad = cabeceraPago.Publicidad;
                    PagoSeleccionado.FacturaPublicidad = cabeceraPago.FacturaPublicidad;
                    PagoSeleccionado.DetallesPago = cabeceraPago.DetallePagos;
                    if (PagoSeleccionado.MonedaOriginal != Constantes.Empresas.MONEDA_CONTABILIDAD)
                    {
                        PagoSeleccionado.Importe = Math.Round(PagoSeleccionado.ImporteOriginal * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        PagoSeleccionado.AjusteRetencion = Math.Round(PagoSeleccionado.AjusteRetencion * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        PagoSeleccionado.RestoAjustes = Math.Round(PagoSeleccionado.RestoAjustes * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        PagoSeleccionado.Comision = Math.Round(PagoSeleccionado.Comision * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        PagoSeleccionado.Publicidad = Math.Round(PagoSeleccionado.Publicidad * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        foreach (var detalle in PagoSeleccionado.DetallesPago)
                        {
                            detalle.Comisiones = Math.Round(detalle.Comisiones * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                            detalle.Importe = Math.Round(detalle.Importe * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                            detalle.Promociones = Math.Round(detalle.Promociones* PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        }
                    }
                });
                
                OnPropertyChanged(() => PagoSeleccionado);
            }
            catch (Exception ex)
            {
                NotificationRequest.Raise(new Notification { Content = ex.Message, Title = "Error" });
            }
            finally
            {
                EstaOcupado = false;
            }

        }

        public ICommand CargarPagosCommand { get; private set; }
        private void OnCargarPagos()
        {
            try
            {
                EstaOcupado = true;
                ListaPagos = MWSFinancesServiceSample.LeerFinancialEventGroups(FechaDesde, NumeroMaxGruposEventos);
            }
            catch (Exception ex)
            {
                NotificationRequest.Raise(new Notification { Content = ex.Message, Title = "Error" });
            }
            finally
            {
                EstaOcupado = false;
            }

        }

        public ICommand ContabilizarPagoCommand { get; private set; }
        private bool CanContabilizarPago()
        {
            return PagoSeleccionado != null;
        }
        private async void OnContabilizarPago()
        {
            ConfirmationRequest.Raise(new Confirmation
            {
                Title = "Contabilizar pago",
                Content = "¿Desea contabilizar el pago?"
            }, c=> ResultadoConfirmacion = c.Confirmed);

            if (!ResultadoConfirmacion)
            {
                return;
            }

            EstaOcupado = true;
            string nombreMarket = DatosMarkets.NombreMarket[DatosMarkets.MarketCuentaPago[PagoSeleccionado.DetallesPago.First().CuentaContablePago]];
            string documento = "AMZ" + PagoSeleccionado.FechaPago.ToString("ddMMyy");
            DateTime fechaPago = new DateTime(PagoSeleccionado.FechaPago.Year, PagoSeleccionado.FechaPago.Month, PagoSeleccionado.FechaPago.Day);
            int asiento = 0;
            bool success = false;
            TransactionOptions transactionOptions = new TransactionOptions
            {
                Timeout = TimeSpan.FromSeconds(300)
            };
            using (var scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
            {
                using (var db = new NestoEntities())
                {
                    db.Database.CommandTimeout = 300;
                    PreContabilidad apunteIngreso = new PreContabilidad
                    {
                        Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                        Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                        Asiento = 1,
                        Fecha = fechaPago,
                        FechaVto = fechaPago,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                        Nº_Cuenta = PROVEEDOR_AMAZON,
                        Contacto = CONTACTO_PROVEEDOR_AMAZON,
                        Concepto = string.Format("Pago {0}", nombreMarket),
                        Haber = PagoSeleccionado.Importe,
                        Nº_Documento = documento,
                        NºDocumentoProv = Strings.Right(PagoSeleccionado.PagoExternalId, 20),
                        Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                        FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                    };
                    db.PreContabilidad.Add(apunteIngreso);

                    PreContabilidad apunteBanco = new PreContabilidad
                    {
                        Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                        Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                        Asiento = 1,
                        Fecha = fechaPago,
                        FechaVto = fechaPago,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                        Nº_Cuenta = BANCO_AMAZON,
                        Concepto = string.Format("Pago {0}", nombreMarket),
                        Debe = PagoSeleccionado.Importe,
                        Nº_Documento = documento,
                        NºDocumentoProv = Strings.Right(PagoSeleccionado.PagoExternalId, 20),
                        Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                        FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                    };
                    db.PreContabilidad.Add(apunteBanco);

                    PreContabilidad apunteProveedor = new PreContabilidad
                    {
                        Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                        Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                        Asiento = 2,
                        Fecha = fechaPago,
                        FechaVto = fechaPago,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                        Nº_Cuenta = PROVEEDOR_AMAZON,
                        Contacto = CONTACTO_PROVEEDOR_AMAZON,
                        Concepto = string.Format("Liq. Pagos {0}. Retenido {1}", nombreMarket, (-PagoSeleccionado.AjusteRetencion).ToString("C")),
                        Debe = PagoSeleccionado.TotalDetallePagos,
                        Nº_Documento = documento,
                        NºDocumentoProv = Strings.Right(PagoSeleccionado.PagoExternalId, 20),
                        Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                        FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                    };
                    db.PreContabilidad.Add(apunteProveedor);

                    PreContabilidad apunteGastos = new PreContabilidad
                    {
                        Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                        Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                        Asiento = 3,
                        Fecha = fechaPago,
                        FechaVto = fechaPago,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                        Nº_Cuenta = PROVEEDOR_AMAZON,
                        Contacto = CONTACTO_PROVEEDOR_AMAZON,
                        Concepto = string.Format("Gastos {0}", nombreMarket),
                        Haber = - PagoSeleccionado.TotalDetalleComisiones - PagoSeleccionado.Comision - PagoSeleccionado.TotalDetallePromociones - PagoSeleccionado.Publicidad,
                        Nº_Documento = documento,
                        NºDocumentoProv = Strings.Right(PagoSeleccionado.PagoExternalId, 20),
                        Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                        FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                    };
                    db.PreContabilidad.Add(apunteGastos);

                    if (PagoSeleccionado.Comision != 0)
                    {
                        PreContabilidad apunteComisionCabecera = new PreContabilidad
                        {
                            Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                            Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                            Asiento = 3,
                            Fecha = fechaPago,
                            FechaVto = fechaPago,
                            TipoApunte = Constantes.TiposApunte.PAGO,
                            TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                            Nº_Cuenta = CUENTA_COMISIONES,
                            Concepto = string.Format("Comisiones Cabecera {0}", nombreMarket),
                            Debe = -PagoSeleccionado.Comision,
                            Nº_Documento = documento,
                            NºDocumentoProv = Strings.Right(PagoSeleccionado.PagoExternalId, 20),
                            Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                            FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO,
                            CentroCoste = "CA", // Esto hay que meejorarlo
                            Departamento = "ADM"
                        };
                        db.PreContabilidad.Add(apunteComisionCabecera);
                    }

                    if (PagoSeleccionado.Publicidad != 0)
                    {
                        PreContabilidad apuntePublicidad = new PreContabilidad
                        {
                            Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                            Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                            Asiento = 3,
                            Fecha = fechaPago,
                            FechaVto = fechaPago,
                            TipoApunte = Constantes.TiposApunte.PAGO,
                            TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                            Nº_Cuenta = PROVEEDOR_AMAZON,
                            Contacto = CONTACTO_PROVEEDOR_AMAZON,
                            Concepto = string.Format("Publicidad {0} {1}", nombreMarket, PagoSeleccionado.FacturaPublicidad),
                            Debe = -PagoSeleccionado.Publicidad,
                            Nº_Documento = documento,
                            NºDocumentoProv = Strings.Right(PagoSeleccionado.PagoExternalId, 20),
                            Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                            FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                        };
                        db.PreContabilidad.Add(apuntePublicidad);
                    }

                    foreach (var detalle in PagoSeleccionado.DetallesPago)
                    {
                        if (detalle.Importe != 0)
                        {
                            PreContabilidad apunteEnvio = new PreContabilidad
                            {
                                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                                Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                                Asiento = 2,
                                Fecha = fechaPago,
                                FechaVto = fechaPago,
                                TipoApunte = Constantes.TiposApunte.PAGO,
                                TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                                Nº_Cuenta = detalle.CuentaContablePago,
                                Concepto = string.Format("Liq. Pago Pedido {0}", detalle.ExternalId),
                                Haber = detalle.Importe,
                                Nº_Documento = documento,
                                NºDocumentoProv = Strings.Right(PagoSeleccionado.PagoExternalId, 20),
                                Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                                FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                            };
                            db.PreContabilidad.Add(apunteEnvio);
                        }

                        if (detalle.Comisiones != 0)
                        {
                            PreContabilidad apunteComisiones = new PreContabilidad
                            {
                                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                                Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                                Asiento = 3,
                                Fecha = fechaPago,
                                FechaVto = fechaPago,
                                TipoApunte = Constantes.TiposApunte.PAGO,
                                TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                                Nº_Cuenta = PagoSeleccionado.DetallesPago.First().CuentaContableComisiones,
                                Concepto = string.Format("Liq. Comisiones {0}", detalle.ExternalId),
                                Debe = -detalle.Comisiones,
                                Nº_Documento = documento,
                                NºDocumentoProv = Strings.Right(PagoSeleccionado.PagoExternalId, 20),
                                Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                                FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                            };
                            db.PreContabilidad.Add(apunteComisiones);
                        }

                        if (detalle.Promociones != 0)
                        {
                            PreContabilidad apuntePromociones = new PreContabilidad
                            {
                                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                                Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                                Asiento = 3,
                                Fecha = fechaPago,
                                FechaVto = fechaPago,
                                TipoApunte = Constantes.TiposApunte.PAGO,
                                TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                                Nº_Cuenta = PagoSeleccionado.DetallesPago.First().CuentaContableComisiones,
                                Concepto = string.Format("Liq. Promociones {0}", detalle.ExternalId),
                                Debe = -detalle.Promociones,
                                Nº_Documento = documento,
                                NºDocumentoProv = Strings.Right(PagoSeleccionado.PagoExternalId, 20),
                                Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                                FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                            };
                            db.PreContabilidad.Add(apuntePromociones);
                        }
                    }

                    try
                    {
                        await Task.Run(() =>
                        {
                            db.SaveChanges();
                            db.prdContabilizar(Constantes.Empresas.EMPRESA_DEFECTO, Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS);
                        });                        
                    }
                    catch (Exception ex)
                    {
                        scope.Dispose();
                        EstaOcupado = false;
                        throw ex;
                    }
                    
                    scope.Complete();

                    // Buscamos los nº de orden y liquidamos 
                    // A desarrollar cuando ya todo lo pendiente del proveedor hecho automático
                    
                    EstaOcupado = false;
                    NotificationRequest.Raise(new Notification { Content = "Se ha contabilizado correctamente el pago", Title = "Pago contabilizado" });

                }
            }

        }

    }
}