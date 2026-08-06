# Especificación Técnica (Hard-Spec): Excepciones de Gastos por Categoría

## 1. Definición del Problema
Actualmente, el sistema calcula los gastos variables imputables al inquilino basándose en un único campo `VariableExpensePercentage` situado en el `RentalContract`. Este enfoque monolítico es insuficiente para escenarios reales donde distintos servicios (Luz, Agua, Internet) requieren reglas de reparto diferenciadas para un mismo contrato.

## 2. Solución Arquitectónica
Se implementará un modelo de **Excepciones (Overrides)**. El `VariableExpensePercentage` seguirá funcionando como el porcentaje por defecto. Se introducirá una nueva entidad relacional `ContractExpensePercentageOverride` que asociará un `RentalContract` con una `ExpenseCategory` específica, asignándole un porcentaje particular (ej. 30%).

## 3. Modelo de Dominio (Entity Framework Core)

### 3.1. Entidad `ContractExpensePercentageOverride`
```csharp
namespace TenantManager.App.Domain;

public class ContractExpensePercentageOverride
{
    public int Id { get; set; }
    public int RentalContractId { get; set; }
    public int CategoryId { get; set; }
    public decimal Percentage { get; set; }

    public RentalContract? RentalContract { get; set; }
    public ExpenseCategory? Category { get; set; }
}
```

### 3.2. Modificaciones en `RentalContract`
Se añadirá una propiedad de navegación:
```csharp
public ICollection<ContractExpensePercentageOverride> ExpenseOverrides { get; set; } = new List<ContractExpensePercentageOverride>();
```

### 3.3. Configuración en `AppDbContext`
Se deberá configurar la nueva entidad como un `DbSet` y configurar el mapeo relacional:
```csharp
public DbSet<ContractExpensePercentageOverride> ContractExpensePercentageOverrides { get; set; }

// En OnModelCreating:
modelBuilder.Entity<ContractExpensePercentageOverride>()
    .HasOne(o => o.RentalContract)
    .WithMany(c => c.ExpenseOverrides)
    .HasForeignKey(o => o.RentalContractId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<ContractExpensePercentageOverride>()
    .HasOne(o => o.Category)
    .WithMany()
    .HasForeignKey(o => o.CategoryId)
    .OnDelete(DeleteBehavior.Restrict);
```

## 4. Lógica de Cálculo de Cuotas (Capa de Vista-Modelo)
Durante la carga de facturas imputables y el cálculo de la porción del inquilino (ej. en `ComputeExpense` y cálculos de Dashboard):
- Por cada factura imputable del mes actual, se obtendrá su `CategoryId`.
- Se buscará en la colección `ExpenseOverrides` del contrato si existe una entrada para dicho `CategoryId`.
- Si existe, el porcentaje a aplicar sobre la factura será `override.Percentage`.
- Si no existe, el porcentaje a aplicar será el `VariableExpensePercentage` por defecto del contrato.

## 5. Interfaz de Usuario (Avalonia)
### 5.1. Vista `ContractsView.axaml`
Se añadirá una sección de "Excepciones de Gastos" dentro del formulario de edición del contrato, conteniendo:
- Un listado dinámico de las excepciones actuales.
- Una interfaz (combo box para categoría y numeric up-down para el porcentaje) para añadir nuevas excepciones.

### 5.2. `ContractsViewModel.cs`
- Propiedades reactivas para gestionar la nueva excepción en borrador.
- Comando `AddExpenseOverrideCommand` que inserta un nuevo objeto a la colección en memoria.
- Comando `RemoveExpenseOverrideCommand` que lo elimina.
- Refactorización de `SaveContract()` para asegurar que se persiste la colección `ExpenseOverrides` asociada al contrato de forma íntegra.

## 6. Migraciones
El despliegue exigirá una migración `AddContractExpenseOverrides` para crear la tabla de relación. No será necesaria la migración de datos existentes, pues el sistema automáticamente operará con el porcentaje base al carecer de overrides.
