# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Nesto is a WPF desktop application built with .NET Framework 4.8 (legacy VB.NET) and .NET 8 (newer C# modules) using Prism framework for modularity. The application provides a comprehensive ERP system for distribution companies, including sales orders, customer management, inventory, and various business operations.

## Solution Structure

- **Nesto**: Main WPF application project
- **Nesto.Models**: Data models (VB.NET, .NET Framework 4.8)
- **Nesto.ViewModels**: Global ViewModels
- **Infrastructure**: Cross-cutting services
- **ControlesUsuario**: Reusable WPF controls (C#, .NET 8)
- **Modulos/**: Prism modules
  - **PedidoVenta/**: Sales orders module (VB.NET)
    - **PedidoVenta/**: Main module
    - **PedidoVentaTests/**: Unit tests (C#, MSTest, FakeItEasy)
  - **PlantillaVenta/**: Sales template module (VB.NET)
  - **Cliente/**: Customer management module
  - **Inventario/**: Inventory module
  - And others...

## Building and Testing

### Build
**IMPORTANT**: The main application uses .NET Framework 4.8 and requires MSBuild (Visual Studio). Mixed projects (.NET Framework + .NET 8) require Visual Studio.

```bash
# CORRECT: Use MSBuild (requires Visual Studio installed)
msbuild Nesto.sln /t:Build /p:Configuration=Debug

# INCORRECT: Do NOT use dotnet CLI - it will fail with MSB4803
# dotnet build Nesto.sln  # ❌ This will NOT work for .NET Framework projects
```

**For Claude Code users**: Since MSBuild is typically not available in the Claude Code environment, assume code changes are syntactically correct after making them. The project must be built in Visual Studio by the user.

### Running Tests
Tests can be run using the .NET CLI for test projects that target .NET 8:
```bash
# Note: Tests may fail to compile if they reference .NET Framework projects
# Best practice: Run tests from Visual Studio Test Explorer

# Attempt to run tests (may require full Visual Studio environment)
dotnet test Modulos/PedidoVenta/PedidoVentaTests/PedidoVentaTests.csproj
```

## Architecture

### Prism Modularity
The application uses Prism for modular architecture:
- Each business module is a separate Prism module
- Modules communicate through Event Aggregator
- Dependency injection via Prism container

### MVVM Pattern
- **Views**: XAML files (WPF)
- **ViewModels**: Implement `BindableBase` and `INavigationAware`
- **Models**: Located in Nesto.Models project

### Data Access
- **Legacy code**: Entity Framework Database-First (VB.NET)
- **New code**: API consumption (NestoAPI) via HTTP clients
- Goal: Minimize direct database access, prefer API calls

### Key ViewModels

#### DetallePedidoViewModel
Located in: `Modulos/PedidoVenta/PedidoVenta/ViewModels/DetallePedidoViewModel.vb`

Responsible for:
- Creating and editing sales orders
- Managing order lines
- Price and discount calculations
- Integration with SelectorCliente control

Key properties:
- `pedido`: PedidoVentaWrapper (current order)
- `ClienteCompleto`: ClienteDTO (full customer info including empresa and contacto)
- `DireccionEntregaSeleccionada`: Delivery address

#### PlantillaVentaViewModel
Located in: `Modulos/PlantillaVenta/ViewModels/PlantillaVentaViewModel.vb`

Responsible for:
- Sales order creation wizard
- Template-based order creation
- Similar functionality to DetallePedidoViewModel but with different UI flow

### Reusable Controls

#### SelectorCliente
Located in: `ControlesUsuario/SelectorCliente/`

WPF control for customer selection with:
- Dependency properties: `Empresa`, `Cliente`, `Contacto`, `ClienteCompleto`
- Two-way data binding support
- Integration with customer search API

## Important Patterns and Conventions

### Language Usage
- **VB.NET**: Legacy modules (PedidoVenta, PlantillaVenta, etc.)
- **C#**: New code, tests, and some shared libraries
- When modifying VB.NET code, maintain VB.NET syntax

### Testing
- Use MSTest framework with `[TestClass]` and `[TestMethod]`
- FakeItEasy for mocking dependencies
- Test pattern: Arrange-Act-Assert (AAA)
- Recent test additions focus on TDD approach (Red-Green-Refactor)

### String Handling
- Database strings often padded with spaces (legacy system)
- Use `.Trim()` extensively when working with entity properties
- Client numbers, vendors, products use fixed-length string keys

### XAML Binding
- Use `RelativeSource` for ancestor bindings
- Two-way binding with `Mode=TwoWay` for editable properties
- Dependency properties for custom controls

## Key Business Rules

### Sales Orders
- Orders created from DetallePedidoViewModel and PlantillaVentaViewModel should be consistent
- Key fields:
  - `origen`: Origin company (from ClienteCompleto.empresa)
  - `contactoCobro`: Billing contact (from ClienteCompleto.contacto, temporary until proper API field exists)
  - `contacto`: Delivery contact (from DireccionEntregaSeleccionada.contacto)

### Customer Management
- ClienteDTO contains customer information
- Distinction between billing contact (contactoCobro) and delivery contact (contacto)

## Configuration

- Connection strings and API endpoints configured in App.config
- Prism module catalog defined in application bootstrapper
- Regional settings for XAML binding

## Recent Changes

Recent work includes:
- TDD refactoring of DetallePedidoViewModel to add ClienteCompleto property
- Addition of origen and contactoCobro field initialization
- Tests for customer complete information propagation

## Common Issues

1. **MSB4803 Error**: Requires MSBuild, not dotnet CLI
2. **Mixed Framework References**: .NET 8 test projects referencing .NET Framework 4.8 projects may show warnings
3. **VB.NET nullable warnings**: Legacy code may have BC42105 warnings - these are acceptable for legacy code
