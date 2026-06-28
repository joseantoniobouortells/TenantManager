# 🧠 Tenant Manager - Agent Memory (Context & Traceability)

*Nota para el Agente: Lee este archivo para entender el estado actual del proyecto, las decisiones históricas recientes y el trabajo pendiente. Para reglas de arquitectura, consulta `AGENTS.md`.*

## 📌 1. Estado Actual del Proyecto
* **Fase:** Desarrollo inicial / Refactorización de Arquitectura.
* **Último gran hito:** Transición completa de un modelo centrado en el Inquilino (`Tenant`) a un modelo centrado en el Contrato (`RentalContract`) para gestionar asignaciones de habitaciones y finanzas.
* **Estado de la Base de Datos:** Las migraciones están al día. Usamos SQLite de forma local.

## 🏗️ 2. Decisiones Arquitectónicas y Refactorizaciones Recientes
1. **Contract-Based Model (Junio 2026):** 
   - *Decisión:* Se movieron `RoomId` y `DepositAmount` de `Tenant` a `RentalContract`.
   - *Motivo:* Permitir que un mismo inquilino pueda tener un histórico de distintos alquileres y habitaciones a lo largo del tiempo.
   - *Impacto:* La UI de Inquilinos se simplificó. Los cálculos del Dashboard y Recibos Mensuales ahora cruzan datos con los Contratos Activos en lugar de los Inquilinos.
2. **Evaluación de Fechas en SQLite:**
   - *Problema:* Entity Framework Core en SQLite falla al traducir consultas LINQ que comparan `DateTime` vs `DateTimeOffset` (ej. `EndDate >= DateTime.Today`).
   - *Solución (Estándar del proyecto):* Se usa `.AsEnumerable()` para cargar la lista en memoria y evaluar las fechas del lado del cliente (`client-side evaluation`), ya que el volumen de datos por propiedad suele ser manejable localmente.
3. **UI / UX:**
   - *Decisión:* Sustitución de botones de "Activar/Desactivar" por controles `ToggleSwitch` modernos en las listas principales (Inquilinos, Habitaciones, Propiedades) para un diseño más limpio tipo app móvil.
4. **Estado de Formularios:**
   - *Decisión:* Al cancelar o guardar la edición de un contrato, se limpia la propiedad `SelectedItem` (`SelectedItem = null`).
   - *Motivo:* Evitar que los paneles laterales contextuales (como el de Prórrogas) se queden abiertos mostrando datos de un elemento que ya no está en edición activa.

## ⚠️ 3. Deuda Técnica y "Gotchas" (¡Cuidado!)
* **Pérdida de datos en migraciones con SQLite:** Al pasar propiedades de `Tenant` a `Contract`, la migración generada por EF Core recreó la tabla, perdiendo la relación Inquilino-Habitación de los datos en producción que no tenían un contrato asociado. *Regla aprendida:* Si hay refactorizaciones de DB destructivas en SQLite (donde el DROP COLUMN no es nativo y requiere recrear la tabla), avisar explícitamente y preparar scripts de migración de datos si es necesario.

## 🚀 4. Roadmap / Próximos Pasos Pendientes
* **[ ] Extracción a Librería Compartida:** Queda pendiente ejecutar el plan de `library_refactor_spec.md` para separar la lógica Core en una librería agnóstica reutilizable, permitiendo en un futuro integraciones con Web o Mobile.
* **[ ] (Añadir futuros hitos aquí...)**
