# Reglas de Negocio: Creación de Albaranes y Facturas

## Fecha de Documentación
**Última actualización**: 2025-01-24

## Ubicación del Código
- **ViewModel**: `Nesto\Modulos\PedidoVenta\PedidoVenta\ViewModels\DetallePedidoViewModel.vb`
- **Funciones afectadas**:
  - `CanCrearAlbaranVenta()` (línea 866)
  - `CanCrearFacturaVenta()` (línea 897)
  - `CanCrearAlbaranYFacturaVenta()` (línea 1004)

## Regla 1: Creación de Albaranes (CanCrearAlbaranVenta)

### Condiciones que DEBEN cumplirse TODAS
1. ✅ El pedido existe (`Not IsNothing(pedido)`)
2. ✅ El pedido tiene líneas (`Not IsNothing(pedido.Lineas)`)
3. ✅ Al menos una línea está **PENDIENTE de albarán**:
   - Estado < `ESTADO_ALBARAN` (2)
   - Estado >= `ESTADO_LINEA_PENDIENTE` (-1)
4. ✅ Al menos una línea está en el **ALMACÉN del usuario actual**
   - `l.Almacen = AlmacenUsuario`

### Justificación
- La validación del **almacén es correcta** porque crear un albarán representa la **preparación física** de productos
- Solo el personal del almacén donde están los productos puede crear el albarán

---

## Regla 2: Creación de Facturas (CanCrearFacturaVenta) ⚠️ MODIFICADO

### Condiciones que DEBEN cumplirse TODAS
1. ✅ El pedido existe (`Not IsNothing(pedido)`)
2. ✅ El periodo de facturación **NO es "FIN_DE_MES"**:
   - `pedido.periodoFacturacion <> Constantes.PeriodosFacturacion.FIN_DE_MES`
   - Los clientes de fin de mes se facturan masivamente al final del mes
3. ✅ El pedido tiene líneas (`Not IsNothing(pedido.Lineas)`)
4. ✅ Al menos una línea está **ALBARANEADA**:
   - Estado = `ESTADO_ALBARAN` (2)
5. ✅ El usuario tiene **PERMISOS para facturar**:
   - Grupo "Almacén" (`Constantes.GruposSeguridad.ALMACEN`)
   - O Grupo "Tiendas" (`Constantes.GruposSeguridad.TIENDAS`)

### ⚠️ CAMBIO IMPORTANTE (2025-01-24)
**ANTES**: Validaba que hubiera líneas en el almacén del usuario (`l.Almacen = AlmacenUsuario`)

**AHORA**: Valida que el usuario tenga permisos (`EsGrupoQuePuedeFacturar`)

### Justificación del Cambio
- La factura es un **documento administrativo/contable**, NO requiere estar en el almacén específico
- Una vez que el albarán está creado (preparación física completada), cualquier usuario con permisos de facturación puede crear la factura
- Esto permite que usuarios de diferentes almacenes puedan facturar pedidos de la empresa "3" u otras empresas

---

## Regla 3: Creación de Albarán Y Factura (CanCrearAlbaranYFacturaVenta) ⚠️ REFACTORIZADO

### Condiciones que DEBEN cumplirse TODAS
1. ✅ Se puede crear el albarán (`CanCrearAlbaranVenta()`)
2. ✅ El pedido existe (`Not IsNothing(pedido)`)
3. ✅ El periodo de facturación **NO es "FIN_DE_MES"**
4. ✅ El usuario tiene **PERMISOS para facturar** (`EsGrupoQuePuedeFacturar`)

### ⚠️ REFACTORIZACIÓN (2025-01-24)
**ANTES**: Duplicaba toda la lógica de validación

**AHORA**: Reutiliza `CanCrearAlbaranVenta()` más las validaciones de facturación

```vb
Private Function CanCrearAlbaranYFacturaVenta() As Boolean
    Return CanCrearAlbaranVenta() AndAlso
           Not IsNothing(pedido) AndAlso
           pedido.periodoFacturacion <> Constantes.PeriodosFacturacion.FIN_DE_MES AndAlso
           EsGrupoQuePuedeFacturar
End Function
```

### Justificación del Cambio
- **Evita duplicación de código**: Reutiliza la lógica existente
- **Más mantenible**: Si cambia la lógica de albaranes, se refleja automáticamente
- **Más legible**: La intención es clara

---

## Estados de Líneas de Pedido

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `ESTADO_LINEA_PENDIENTE` | -1 | Línea pendiente de servir |
| `ESTADO_SIN_FACTURAR` | 1 | En curso |
| `ESTADO_ALBARAN` | 2 | Albaraneada (lista para facturar) |
| `ESTADO_FACTURA` | 4 | Facturada |

## Periodos de Facturación

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `NORMAL` | "NRM" | Facturación normal |
| `FIN_DE_MES` | "FDM" | Facturación agrupada a fin de mes |

## Grupos de Seguridad que Pueden Facturar

| Grupo | Constante |
|-------|-----------|
| Almacén | `Constantes.GruposSeguridad.ALMACEN` |
| Tiendas | `Constantes.GruposSeguridad.TIENDAS` |

---

## Validación en el Backend (API)

La validación del cliente (ViewModel) es para **habilitar/deshabilitar botones UI**.

La validación **real de negocio** está en:
- **Backend**: `NestoAPI\Infraestructure\Facturas\ServicioFacturas.cs`
- **Método**: `CrearFactura(string empresa, int pedido, string usuario)` (línea 292)

```csharp
if (cabPedido.Periodo_Facturacion == Constantes.Pedidos.PERIODO_FACTURACION_FIN_DE_MES)
{
    // Caso especial: cliente de fin de mes
    return new CrearFacturaResponseDTO
    {
        NumeroFactura = cabPedido.Periodo_Facturacion,
        Empresa = empresa,
        NumeroPedido = pedido
    };
}
```

---

## Tests Requeridos

### ❌ Tests del Cliente WPF (NO IMPLEMENTADOS)
Para testear las funciones `CanCrear*` del ViewModel se requiere:
1. Crear proyecto de tests para `Nesto.Modulos.PedidoVenta`
2. Hacer las funciones `Friend` (internal) en lugar de `Private`
3. Agregar `InternalsVisibleTo` al módulo
4. Mockear dependencias de Prism (IRegionManager, IConfiguracion, etc.)

### ✅ Tests del Backend (PENDIENTE DE IMPLEMENTAR)
Los tests deberían validar:
1. ✅ Pedido con periodo "FDM" retorna el código especial
2. ✅ Pedido con periodo "NRM" crea la factura normalmente
3. ✅ Pedido sin líneas albaraneadas lanza excepción
4. ✅ Traspaso a empresa espejo funciona correctamente

**Archivo de tests**: `NestoAPI.Tests\Infrastructure\GestorFacturasTests.cs`

---

## Casos de Uso Comunes

### ✅ Caso 1: Crear Albarán de pedido de empresa "3"
- Usuario en almacén ALG
- Pedido tiene líneas pendientes en ALG
- ✅ **Botón "Crear Albarán" HABILITADO**

### ✅ Caso 2: Crear Factura de pedido de empresa "3" (CON CAMBIO)
- Usuario en almacén ALG
- Pedido tiene albarán creado (líneas en estado 2)
- Líneas están en almacén REI (diferente al usuario)
- Usuario pertenece al grupo "Almacén" o "Tiendas"
- ✅ **Botón "Crear Factura" HABILITADO** (antes estaba deshabilitado ❌)

### ❌ Caso 3: Crear Factura de cliente fin de mes
- Pedido con `periodoFacturacion = "FDM"`
- ❌ **Botón "Crear Factura" DESHABILITADO**
- Estos clientes se facturan masivamente a fin de mes

---

## Referencias
- **Constantes**: `Nesto\Infrastructure\Shared\Constantes.cs`
- **ViewModel**: `Nesto\Modulos\PedidoVenta\PedidoVenta\ViewModels\DetallePedidoViewModel.vb`
- **API Backend**: `NestoAPI\Infraestructure\Facturas\ServicioFacturas.cs`
