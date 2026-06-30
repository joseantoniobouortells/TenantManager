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
5. **Día de Pago e Ingresos Previstos (Junio 2026):**
   - *Característica:* Se añadió el campo numérico `PaymentDay` a `RentalContract` y una tarjeta en el Dashboard para prever los ingresos del mes siguiente.
   - *Regla de Negocio (Prorrateo):* La estimación de ingresos futuros calcula los días exactos que cada contrato estará activo en el próximo mes para prorratear la cuota, ofreciendo un dato 100% realista en lugar de sumar cuotas mensuales enteras a ciegas.
6. **Refactorización de Layouts Dinámicos (Junio 2026):**
   - *Decisión:* Cambiar de proporciones fijas (ej. `2*, 1.2*`) a `*, Auto` en paneles con visibilidad condicional (`ContractsView.axaml` y `PropertiesView.axaml`), y ocultar por completo el listado de contratos mientras se está en modo edición.
   - *Motivo:* Evitar espacios en blanco vacíos y la redundancia vertical de mostrar la tabla de contratos debajo del formulario de edición. Liberar la altura máxima de la pantalla para el panel lateral de prórrogas.
   - *Impacto:* La tabla de contratos se expande al 100% en modo lectura. Al editar, la tabla se oculta de forma fluida y los botones de acción ("Ver PDF" y "Eliminar") se reubican al pie del propio formulario. Los botones de texto de las prórrogas se sustituyen por iconos vectoriales compactos (`PathIcon`) con ToolTips para que quepan perfectamente en el ancho del panel lateral (360px). Se eliminaron también columnas redundantes sin uso en `RoomsView.axaml`.
7. **Ordenación de Contratos Interactiva y Bidireccional (Junio 2026):**
   - *Decisión:* Las cabeceras de la tabla de contratos ("Inquilino", "Fecha de Inicio", "Fecha de Fin") se transformaron en botones planos con cursor `Hand` que ejecutan `SortCommand` pasando el parámetro de la columna.
   - *Funcionalidad:* Permite ordenación tanto ascendente como descendente (A-Z / Z-A para texto, y orden cronológico / cronológico inverso para fechas). Al volver a hacer clic en la misma cabecera activa, se invierte la dirección de ordenación.
   - *Indicador Visual:* Se muestra una flecha (`▲` o `▼`) usando el color de énfasis de la app (`PrimaryBrush`) junto a la cabecera activa para indicar con total claridad por qué columna está ordenada la lista y en qué sentido.
8. **Obligatoriedad de Fecha de Fin de Contrato (Junio 2026):**
   - *Decisión:* Se eliminó la etiqueta de "(opcional)" en la fecha de finalización (pasando a usar `EndDateLabel` en la traducción). Además, se añadió validación obligatoria en el ViewModel (`SaveContract`) para impedir guardar contratos sin esta fecha.
   - *Motivo:* Garantizar que todos los contratos tengan un periodo definido para evitar inconsistencias en el cálculo de previsiones y facturación mensual.
9. **Fechas Efectivas con Prórrogas en el Listado (Junio 2026):**
   - *Decisión:* Las propiedades expuestas en la tabla (`StartDate` y `EndDate` de `ContractDisplayItem`) se calculan dinámicamente. La fecha de inicio es la del contrato original y la fecha de fin se sobreescribe automáticamente con la de su prórroga más reciente (si existe).
   - *Motivo:* Evitar que contratos activos con prórrogas firmadas (como el caso de Erik Artigas) muestren visualmente fechas de fin antiguas ya vencidas.
   - *Impacto:* La ordenación de la tabla por "Fecha de Fin" ahora evalúa y clasifica los contratos utilizando la fecha de fin efectiva calculada en tiempo real.
10. **Flujo de Prórrogas: Persistencia de Selección y Confirmación (Junio 2026):**
    - *Decisión:* 
      - **Persistencia:** Al guardar o borrar una prórroga, se preserva el ID del contrato activo antes de recargar la lista de la base de datos y se vuelve a seleccionar al terminar. Esto mantiene el formulario de edición de contrato relleno y el panel de prórrogas abierto.
      - **Cálculo Inmediato:** Al persistir la selección, la fecha de fin efectiva calculada se actualiza visualmente en la tabla al instante.
      - **Confirmación en Línea:** Se reemplazó la eliminación directa por una confirmación en línea ("¿Eliminar prórroga? [Sí] [No]") en la propia botonera del panel, evitando diálogos nativos intrusivos.
    - *Motivo:* Corregir errores donde el formulario de contratos se vaciaba accidentalmente debido a la pérdida de selección provocada por el método `Clear()` de la lista vinculada de Avalonia.
11. **Borrado Contextual con Confirmación en todas las Tablas (Junio 2026):**
    - *Decisión:*
      - Se añadió una columna adicional en todas las tablas principales de la aplicación (**Contratos, Viviendas, Habitaciones, Inquilinos, Facturas y Pagos**) que contiene un botón de eliminación directo por fila (icono de papelera).
      - Se diseñó un sistema de **confirmación modal unificado** por pantalla: al pulsar la papelera de una fila, la interfaz de esa vista se congela y emerge una tarjeta centrada que solicita confirmación (`¿Confirmar eliminación? Esta acción no se puede deshacer. [Confirmar] [Cancelar]`).
      - Las acciones de borrado se han parametrizado en todos los ViewModels para recibir el elemento concreto a eliminar.
    - *Motivo:* Mejorar la accesibilidad y rapidez de las operaciones de administración (evitando tener que seleccionar primero el elemento en la tabla y luego buscar el botón de borrado en otras partes de la pantalla) garantizando a la vez la seguridad de los datos contra clics accidentales.
12. **Rediseño Completo del Dashboard e Histórico de Ingresos y Gastos (Junio 2026):**
    - *Decisión:*
      - **Simplificación y Espaciado:** Se eliminaron las tarjetas y listados redundantes (*Cobro de Ingresos*, *Habitaciones Disponibles* y *Archivos de Contrato Faltantes*). Esto liberó el espacio suficiente para ampliar la tarjeta de *Ocupación de Habitaciones* (evitando desbordamientos visuales) y habilitar una distribución de 3 columnas superiores y 2 inferiores.
      - **Gráfico de Barras Histórico:** Se implementó un gráfico de barras comparativo (Verde para Ingresos, Rojo para Gastos) programado en XAML puro con escalado dinámico de alturas, ToolTips interactivos en hover y ScrollViewer horizontal automático. Se añadió un padding inferior de 16px al `ScrollViewer` para evitar que el comportamiento dinámico de expansión de la barra de scroll nativa de macOS solape o tape las etiquetas de los meses y las barras.
      - **Intervalo Temporal Dinámico:** Se añadió un `ComboBox` en la cabecera del panel del gráfico para alternar el intervalo temporal visible entre **3, 6, 12 meses o "Desde el inicio"**. Esta última opción calcula dinámicamente el mes de origen buscando el registro de pago o factura de gasto más antiguo para esa propiedad, provocando el recálculo y la recarga automática en caliente de los datos.
      - **Bloque de Totales y Beneficio:** Se integró un panel superior en la tarjeta del gráfico para ver instantáneamente el **Total Ingresado**, **Total Gastado** y el **Beneficio Neto** del intervalo de meses elegido.
    - *Motivo:* Resolver los solapamientos visuales por falta de espacio, eliminar widgets que no aportaban valor al propietario y proveer una herramienta interactiva para analizar el balance financiero histórico del piso de forma cómoda y limpia.
13. **Consistencia de Datos en Pagos y Auto-reparación de Registros (Junio 2026):**
    - *Decisión:*
      - **Auto-reparación en Carga:** Al cargar el Dashboard, si existen cobros con estado `Paid` (Cobrado) pero con un importe cobrado (`PaidAmount`) de 0, el sistema los repara automáticamente actualizando el importe cobrado al importe total esperado del cobro.
      - **Auto-rellenado y Bloqueo en Formulario:** En el formulario de edición de pagos (`MonthlyPaymentListViewModel.cs` y `PaymentsView.axaml`), la caja de texto del importe cobrado (`EditPaidAmount`) queda **bloqueada y deshabilitada** para todos los estados excepto `Partial`. Al elegir `Paid`, se auto-rellena con el 100% esperado; al elegir `Pending`/`Late`/`Waived`, se auto-rellena con `0 €`. Solo se puede editar libremente si el estado es `Partial`.
    - *Motivo:* Garantizar la consistencia absoluta de los datos de ingresos, impidiendo que el usuario pueda registrar manualmente importes incoherentes (como marcar un cobro como Paid pero dejar el importe pagado en 0 o viceversa) a la vez que se conserva la capacidad de registrar cobros parciales reales.
14. **Simplificación de Estados de Pago (Junio 2026):**
    - *Decisión:*
      - Se eliminaron los estados `Late` (Atrasado) y `Waived` (Exento/Anulado) de la aplicación, dejando únicamente los 3 estados esenciales: **`Pending` (Pendiente)**, **`Paid` (Cobrado)** y **`Partial` (Cobro Parcial)**.
      - **Limpieza del Donut Chart:** Se actualizaron la gráfica circular y su leyenda en el Dashboard para reflejar y computar únicamente estos 3 estados de forma visual.
      - **Remoción de Recursos:** Se limpiaron las traducciones correspondientes de `en.axaml` y `es.axaml`.
    - *Motivo:* Reducir la sobre-ingeniería en los estados de facturación. Un pago fuera de plazo sigue considerándose "Pendiente", y los cobros exentados se pueden suprimir o registrar a cero euros, manteniendo la lógica del negocio sumamente clara y directa.
15. **Filtrado Inteligente de Inquilinos y Auto-Relleno en Pagos (Junio 2026):**
    - *Decisión:*
      - **Método centralizado `GetContractForTenantMonth`:** Se extrajo un método reutilizable que, dado un `tenantId`, año y mes, devuelve los datos del contrato o prórroga activa (renta esperada, gasto esperado, tipo de gasto) o `null` si no hay cobertura. Materializa consultas a memoria antes de iterar para evitar el error de múltiples lectores activos de SQLite.
      - **Auto-relleno de importes:** Al crear un pago nuevo, los campos `ExpectedRentAmount` y `ExpectedExpenseAmount` se rellenan automáticamente desde el contrato activo cada vez que se selecciona un inquilino o se cambia el mes/año. Elimina la necesidad de que el usuario rellene manualmente importes que ya están definidos en el contrato.
      - **Filtrado contextual por mes/año del pago:** El desplegable de inquilinos se filtra dinámicamente según el mes y año que el usuario está introduciendo en el formulario (no según la fecha de hoy). Cambiar el mes de Junio a Julio oculta automáticamente a los inquilinos cuyo contrato termina en Junio.
      - **Opción A para edición:** Al editar un pago existente y cambiar el mes a uno sin contrato, el inquilino original permanece visible en el desplegable (para no perder contexto), pero el guardado queda bloqueado por validación.
      - **Validación en `SavePayment`:** Antes de guardar cualquier pago (nuevo o editado), se valida que el inquilino tenga contrato activo para el mes/año seleccionado. Si no lo tiene, el guardado se bloquea silenciosamente.
      - **Refactorización de `GenerateBatch`:** La generación batch ahora usa `GetContractForTenantMonth` en lugar de duplicar la lógica de búsqueda de contratos, y automáticamente salta los meses sin contrato activo.
      - **Corrección del orden en `StartNewPayment`:** Se asignan año/mes (vía campos privados + `OnPropertyChanged`) ANTES de llamar a `LoadAvailableTenants`, para que el filtro use los valores correctos desde el principio.
    - *Motivo:* Garantizar consistencia total entre contratos y pagos, eliminando la posibilidad de crear cobros en meses sin cobertura contractual, y simplificando la experiencia del usuario al auto-rellenar importes.
81. **Gastos Imputables (Junio 2026):**
    - *Decisión:* Se añadió una propiedad booleana `IsChargeableToTenant` a las facturas de gastos (`ExpenseInvoice`), gestionable mediante un `ToggleSwitch` en la interfaz. Al calcular los cobros variables, el sistema ahora filtra dinámicamente y solo suma los gastos que están marcados explícitamente como imputables.
    - *Motivo:* Diferenciar entre gastos estructurales del propietario (ej. IBI o reparaciones) y suministros consumibles imputables a los inquilinos (ej. luz, agua), sin necesidad de crear tablas o entidades separadas complejas.
82. **Refinamiento de Interfaz de Gastos (Junio 2026):**
    - *Decisión:* Se internacionalizó el título de la vista (ahora utiliza recursos del diccionario), se sustituyó el botón "Nuevo Gasto" por un icono circular estandarizado, se eliminó la botonera contextual inferior de edición/borrado al seleccionar una fila (las filas ya se editan haciendo clic en ellas), y se integró un botón contextual con icono para "Ver PDF" directamente en cada fila, el cual se deshabilita automáticamente (gracias a la propiedad computada `HasFile`) si el gasto no tiene factura vinculada.
    - *Motivo:* Modernizar la UX y eliminar clics redundantes, acercando el diseño visual de la tabla de gastos al estándar móvil/moderno del resto de la aplicación.
83. **Buscador y Ordenación Bidireccional en Gastos (Junio 2026):**
    - *Decisión:* Se introdujo un campo de búsqueda en tiempo real (barra superior) y cabeceras interactivas en la tabla de Gastos, reutilizando el patrón de almacenamiento en memoria (`_allInvoices`) empleado en los contratos. Se puede ordenar haciendo clic en "Type", "Año", "Mes", "Amount" y "Repercutible", actualizando los indicadores visuales "▲/▼".
    - *Motivo:* Proveer a la vista de Gastos con las mismas capacidades de filtrado y ordenación avanzadas y rápidas que tiene la sección de Contratos, mejorando sustancialmente el manejo de históricos de facturación largos sin penalizar a la base de datos (SQLite).

## ⚠️ 3. Deuda Técnica y "Gotchas" (¡Cuidado!)
* **Pérdida de datos en migraciones con SQLite:** Al pasar propiedades de `Tenant` a `Contract`, la migración generada por EF Core recreó la tabla, perdiendo la relación Inquilino-Habitación de los datos en producción que no tenían un contrato asociado. *Regla aprendida:* Si hay refactorizaciones de DB destructivas en SQLite (donde el DROP COLUMN no es nativo y requiere recrear la tabla), avisar explícitamente y preparar scripts de migración de datos si es necesario.

## 🚀 4. Roadmap / Próximos Pasos Pendientes
* **[ ] Extracción a Librería Compartida:** Queda pendiente ejecutar el plan de `library_refactor_spec.md` para separar la lógica Core en una librería agnóstica reutilizable, permitiendo en un futuro integraciones con Web o Mobile.
* **[ ] (Añadir futuros hitos aquí...)**
