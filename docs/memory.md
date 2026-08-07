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
84. **Automatización de Estado de Inquilinos y Eliminación de Switch Manual (Junio 2026):**
    - *Decisión:* Se eliminó la propiedad `IsActive` de la entidad `Tenant` (y su respectiva columna en base de datos), así como el botón de activación/desactivación en la UI.
    - *Motivo:* Reducir el trabajo manual y la redundancia. El sistema ya es inteligente calculando estados a través de los Contratos (fechas de inicio y fin). El Dashboard ahora calcula los "Inquilinos Activos" basándose exclusivamente en si tienen un contrato vigente en el día actual, y los selectores muestran siempre todos los inquilinos para permitir re-contratar a ex-inquilinos de manera fluida.
85. **Buscador y Ordenación Bidireccional en Pagos Mensuales (Junio 2026):**
    - *Decisión:* Se estandarizó la pantalla de Pagos Mensuales aplicando el mismo patrón de caché en memoria (`_allPayments`) y ordenación interactiva implementado previamente en Contratos y Gastos.
    - *Motivo:* Proveer consistencia a lo largo de toda la aplicación, reduciendo las llamadas de lectura intensiva a SQLite y permitiendo filtrar por Inquilino, Año o Mes al instante.
86. **Buscador y Ordenación Bidireccional en Inquilinos (Junio 2026):**
    - *Decisión:* Se eliminaron los botones obsoletos de Editar/Actualizar y se introdujo la búsqueda y ordenación interactiva (▲/▼) por Nombre y Teléfono en la vista de Inquilinos, utilizando la caché local en memoria (`_allTenants`).
    - *Motivo:* Finalizar la estandarización UX de las tablas de datos, logrando una interfaz limpia y rápida donde los filtrados ocurren en tiempo real del lado del cliente.
87. **Infraestructura de Empaquetado y Publicación (Junio 2026):**
    - *Decisión:* Se configuró un workflow en GitHub Actions para generar binarios multiplataforma automáticos. En Windows se configuró un instalador MSI usando WiX v4 (empaquetando el `.cab` internamente) y asegurando la inclusión de las librerías nativas de SQLite (`IncludeNativeLibrariesForSelfExtract=true`) para evitar cierres al arranque. En macOS se reescribió la creación del `.dmg` montando un disco virtual temporal para aislar el proceso y evitar el desbordamiento de memoria al crear el enlace simbólico a `/Applications`.
88. **Notificaciones Nativas Interactivas en macOS (Julio 2026):**
    - *Decisión:* Se desechó el uso de librerías de terceros (ej. DesktopNotifications) debido a la falta de binarios nativos actualizados en macOS, y en su lugar se implementó un puente ligero en Objective-C puro (`MacNotifier.m`) compilado localmente en una `.dylib`. Este puente expone `NSUserNotificationCenter`.
    - *Funcionalidad:* El Dashboard de la aplicación verifica los impagos transcurridas 12h y lanza notificaciones nativas en el escritorio. Estas notificaciones evitan la deduplicación del sistema (mediante inyección de UUIDs y `deliveryDate` en tiempo real).
    - *Interacción:* Se enlazó un callback (mediante puntero a función en C e invocación `DllImport` en C#) desde macOS hasta el hilo de Avalonia (`Dispatcher.UIThread`), de modo que al pulsar en "Mostrar" en la notificación de impago, la app salta automáticamente a la pestaña de "Pagos" (índice 5 de la ventana) y se sitúa en primer plano.
    - *Solución para Desarrollo:* Dado que macOS bloquea notificaciones ricas (iconos) en binarios sin un `Info.plist` y Bundle ID válido, se abandonó el uso directo de `dotnet run`. Se creó un script de desarrollo (`run-mac.sh`) que compila la aplicación y la empaqueta "al vuelo" como un auténtico `TenantManager.app` para poder testear funciones nativas y notificaciones sin problemas durante el desarrollo local.
    - *Motivo:* Proveer instaladores nativos, sólidos y profesionales (en lugar de obligar al usuario a usar scripts) superando los fallos estándar de despliegue de XCOPY / SingleFile en .NET.
88. **Extensión del Área de Cliente y Título Dinámico (Junio 2026):**
    - *Decisión:* Se activó `ExtendClientAreaToDecorationsHint="True"` en Avalonia, fusionando el marco de la app con la barra de título del sistema operativo. Se introdujeron márgenes estáticos para evitar el solapamiento con los botones de control de Windows/macOS. Se implementó un "breadcrumb" dinámico (ej. Vivienda Principal / Inquilinos) y se inyectó la versión compilada dinámicamente en el pie de la barra lateral.
    - *Motivo:* Proveer un diseño excepcionalmente moderno y "premium", liberando espacio vertical y otorgando un look-and-feel nativo a la altura de las aplicaciones comerciales top.
89. **Cálculo Automático y Dinámico de Cuotas Pendientes (Julio 2026):**
    - *Decisión:* Se eliminó el estado `Pending` de la base de datos y se quitó el flujo manual de "Generar Lote". Ahora, los pagos pendientes se calculan en tiempo real cruzando los contratos activos de cada inquilino frente a los pagos ya cobrados (`Paid` o `Partial`) registrados en BD. Los gastos (fijos o variables calculados a partir de facturas imputables) se computan dinámicamente mes a mes.
    - *Motivo:* Evitar que el propietario tenga que generar manualmente lotes de cuotas cada mes. Ahora las cuotas pendientes aparecen de forma reactiva y automática en el Dashboard y en la sección superior de Pagos con un botón directo para "Cobrar".
90. **Indicador Visual LED de Estado de Contratos (Julio 2026):**
    - *Decisión:* Se añadió una propiedad computada `IsActive` en el modelo de vista de contratos y un indicador visual LED interactivo en la lista de contratos (verde para contratos activos, rojo para contratos vencidos/inactivos) usando `RadialGradientBrush` y `BoxShadow`.
    - *Motivo:* Mejorar la UX proporcionando retroalimentación visual instantánea y clara sobre la validez del contrato al momento de consultar el listado.
91. **Notificaciones Nativas Cross-Platform (Julio 2026):**
    - *Decisión:* Se implementó `NativeNotificationService` utilizando llamadas directas sin dependencias al sistema operativo (`osascript` en macOS, `notify-send` en Linux y un script `PowerShell` inyectado para WinRT en Windows). 
    - *Motivo:* Proveer notificaciones nativas de escritorio (al arrancar la app y de forma periódica con enfriamiento de 12 horas) cuando existen cobros pendientes, evitando dependencias de terceros desactualizadas incompatibles con Avalonia 12.
92. **Separación de Concepto y Categoría en Gastos (Julio 2026):**
    - *Decisión:* Tras un intento de fusionar el texto libre en las categorías, se rectificó el diseño para separar el **Concepto** (texto libre, ej. "Factura luz de agosto") de la **Categoría** (entidad relacional `ExpenseCategory`, ej. "Suministros").
    - *Migración de Recuperación:* Se utilizó SQL Raw inyectado en la migración `FixExpenseConceptAndCategory` para extraer el concepto desde la tabla contaminada de categorías, crear 4 categorías estándar nuevas (Suministros, Mantenimiento, Seguros, Otros), y vincular las facturas automáticamente dependiendo de si su flag original de `IsChargeableToTenant` era verdadero o falso.
    - *Interfaz (UI):* Se eliminó el botón inline confuso para crear categorías y se implementó un *Overlay / Dialog* moderno (un grid transparente sobre el UserControl) para gestionar la creación "al vuelo" de agrupaciones.
93. **Ajuste de Cabecera para Windows y Bump de Versión 1.0.4 (Julio 2026):**
    - *Decisión:* Se incrementó el padding derecho de la barra de título superior (`Border`) de `140` a `220` en `MainWindow.axaml`. Además, se corrigieron errores de compilación en `DomainTests.cs` (usando `Concept` en lugar del antiguo `ExpenseType`) y se actualizó la versión de la aplicación a `1.0.4` en el `.csproj` y `run-mac.sh`.
    - *Motivo:* Evitar que los botones de control nativos de maximizar/minimizar de la ventana de Windows se solaparan visualmente con el selector desplegable de vivienda activa cuando `ExtendClientAreaToDecorationsHint` está activo.
94. **Corrección de Cálculo de Ingresos Previstos (Julio 2026):**
    - *Bug:* El bucle de prorrateo diario usaba `new DateTimeOffset(..., TimeSpan.Zero)` (mediodía UTC) para comparar contra las fechas de los contratos. Algunas fechas se almacenan sin offset (p.ej. `2026-08-31 00:00:00`) y SQLite las trata como UTC medianoche. Al ser medianoche < mediodía UTC, la comparación `EndDate >= date` fallaba en el último día, excluyéndolo del prorrateo y produciendo un resultado ~1/31 más bajo de lo correcto (ej. 543€ en vez de 550€ en agosto).
    - *Solución:* Se reemplazó la comparación `DateTimeOffset` por comparaciones de `DateTime.Date` (componente fecha sin hora ni timezone). Se extraen `contract.StartDate.Date` y `contract.EndDate?.Date` fuera del bucle y se construye `dayDate = new DateTime(year, month, day)` para comparar, eliminando por completo el problema de zona horaria.
    - *Validación:* La simulación con datos reales de la BD para agosto 2026 (Erik Pradas 210€ + Erik Artigas 340€) produce exactamente **550€**, coincidiendo con el cálculo manual esperado.

## ⚠️ 3. Deuda Técnica y "Gotchas" (¡Cuidado!)
* **Pérdida de datos en migraciones con SQLite:** Al pasar propiedades de `Tenant` a `Contract`, la migración generada por EF Core recreó la tabla, perdiendo la relación Inquilino-Habitación de los datos en producción que no tenían un contrato asociado. *Regla aprendida:* Si hay refactorizaciones de DB destructivas en SQLite (donde el DROP COLUMN no es nativo y requiere recrear la tabla), avisar explícitamente y preparar scripts de migración de datos si es necesario.
* **Fechas sin timezone en SQLite:** Algunas fechas persisten sin sufijo de offset (ej. `2026-08-31 00:00:00`) porque se insertaron antes de que la aplicación normalizara todos los `DateTimeOffset`. Siempre usar `.Date` para comparaciones de fechas en el ViewModel, nunca comparar directamente `DateTimeOffset` raw contra otro con timezone diferente.

## 🚀 4. Roadmap / Próximos Pasos Pendientes
* **[x] Extracción a Librería Compartida:** Se separó la lógica de dominio y asistente IA en `TenantManager.Core`, haciéndola independiente del framework UI (Avalonia) y reutilizable.
* **[ ] (Añadir futuros hitos aquí...)**
95. **Internacionalización (i18n) en Vistas y Notificaciones (Julio 2026):**
    - *Decisión:* Se auditaron todas las vistas (`.axaml`) y se extrajeron las cadenas literales estáticas sustituyéndolas por `DynamicResource`, apoyándose en diccionarios de recursos dinámicos (`en.axaml` y `es.axaml`). 
    - *Notificaciones:* Se actualizó el puente nativo `MacNotifier.m` y el servicio `NativeNotificationService` para aceptar y resolver dinámicamente las cadenas traducidas desde Avalonia (título, cuerpo y botón de acción) mediante `Avalonia.Application.Current.TryGetResource`, garantizando que la notificación del sistema operativo concuerde con el idioma seleccionado en la interfaz.
96. **Migración de DMG a PKG en macOS (Julio 2026):**
    - *Decisión:* Se reemplazó la generación de instaladores `.dmg` por paquetes `.pkg` en `bundle-mac.sh` y el workflow de GitHub Actions.
    - *Motivo:* Proveer un script de Apple `postinstall` oculto en el `.pkg` que elimina automáticamente el atributo de cuarentena (`com.apple.quarantine`) y los archivos temporales (`._*`, `.DS_Store`) usando `xattr -cr` de la aplicación una vez extraída en `/Applications`. De esta forma, el usuario final ya no necesita usar la terminal para autorizar la ejecución en macOS tras la descarga.
97. **Asistente IA Local Integrado (Fase 1-6) (Julio 2026):**
    - *Decisión:* Se implementó un asistente de IA local para permitir a los usuarios consultar datos directamente (p.ej. "¿Cuándo se va Erik Artigas?").
    - *Arquitectura:* Se optó por una resolución determinista y segura de la intención (`AiQueryService`) combinada con un LLM generativo compatible con la API de OpenAI (vía `LocalAiClient`).
    - *Privacidad (PII):* El LLM nunca tiene acceso directo a la base de datos ni a los datos personales. Un intermediario (`SafeContextBuilder`) inyecta estrictamente los datos relevantes de manera anonimizada (ej. contratos, fechas) redactando teléfonos y correos electrónicos.
    - *Independencia:* Se construyó utilizando puro `HttpClient` y la serialización JSON del framework para evitar dependencias pesadas como Semantic Kernel o la SDK oficial de OpenAI, dado que la meta era un cliente agnóstico ultra ligero enfocado en conectar contra LM Studio local.
98. **Planificador Semántico de Consultas Seguro (Julio 2026):**
    - *Decisión:* Se implementó un planificador semántico de consultas (Fases 1-10) en `TenantManager.Core` con un catálogo de operaciones permitidas por recurso (Rooms, Tenants, Contracts, Payments, Expenses, Dashboard).
    - *Motivo:* Evitar la fragilidad de la extracción de keywords del LLM anterior permitiendo responder preguntas estructuradas complejas (conteos, listas filtradas, sumas de facturas/pagos y resúmenes) de forma 100% segura y determinista en español e inglés sin que el LLM genere o ejecute código SQL.
    - *Impacto:* Compilación con 0 advertencias y suite de 82 pruebas unitarias, de seguridad y funcionales ejecutadas con éxito sobre SQLite en memoria. Se controla estrictamente la privacidad omitiendo PII y limitando las consultas al scope de la propiedad activa.
99. **Capa de Interpretación Semántica (SemanticRequest MVP) (Julio 2026):**
    - *Decisión:* Se introdujo un paso previo (`SemanticRequest`) antes del pipeline de `SemanticQueryPlan`.
    - *Motivo:* Permitir al asistente procesar "múltiples salidas" en la misma pregunta (ej. "¿Cuánto se ha ingresado y a qué mes corresponde?") y posibilitar heurísticas deterministas sin DB ni llamadas al LLM para preguntas sobre el periodo del resultado anterior (ej. "¿A qué mes corresponde?").
    - *Arquitectura:* En `TenantManager.Core` se crearon contratos inmutables (`SemanticRequest`, `RequestedOutput`), constructores (`SemanticRequestBuilder`), y resolvedores deterministas por palabra clave (`SemanticRequestResolver`). `AssistantContext` se extendió para almacenar `LastFormattedAnswer` y `LastExecutionResult`.
    - *Impacto:* Compilación exitosa y 126 pruebas. Resoluciones directas (fast-path) al preguntar por datos relativos al resultado previo.
100. **AI Evaluation Runner y Test Dataset (Julio 2026):**
    - *Decisión:* Se creó el proyecto de consola `TenantManager.Evaluation` y el proyecto de pruebas `TenantManager.Evaluation.Tests` para validar el comportamiento y precisión de la IA local determinista y en vivo.
    - *Arquitectura:* Se introdujo un seam de diagnósticos `IAssistantExecutionObserver` en `AiQueryService` para trazar el request semántico, periodos, planes generados y el resultado final sin afectar el comportamiento central. Se implementaron los modos `validate` y `live`.
    - *Datos de Prueba:* Se utiliza un `deterministic-fixture.json` que carga en SQLite de memoria un contexto simulado, permitiendo ejecutar y validar escenarios contra LLMs locales a través de LocalAiClient.
101. **Pagos Manuales y Arrastre de Saldos (Agosto 2026):**
    - *Decisión:* Se habilitó la edición de la cantidad pagada (`PaidAmount`) en la vista de pagos incluso cuando el estado está marcado como `Paid`. Se introdujo la función `GetTenantBalance` que hace un cálculo histórico de todo lo pagado menos todo lo esperado para el inquilino en la vivienda actual.
    - *Motivo:* Permitir registrar cobros por encima o por debajo de la cuota y arrastrar saldos a favor o en contra (ej. si el inquilino paga 10€ de más un mes, al mes siguiente se sugiere la cuota restándole esos 10€).
102. **Creación de Pagos Manuales desde Cero (Agosto 2026):**
    - *Decisión:* Se introdujo el botón "Nuevo Pago" en `PaymentsView.axaml` con el comando `NewPaymentCommand`. Se creó la propiedad `IsNewManualPayment` para desbloquear dinámicamente los campos `Inquilino`, `Año` y `Mes` exclusivamente al crear un recibo manual desde cero.
    - *Motivo:* El sistema dependía totalmente de los cálculos automáticos de cuotas pendientes. Esto da total libertad al administrador para introducir cobros manuales (adelantados, atrasados o regulaciones especiales) sin depender del sistema automatizado.
103. **Excepciones de Porcentaje de Gastos Variables por Categoría (Agosto 2026):**
    - *Decisión:* Se introdujo la entidad `ContractExpensePercentageOverride` para permitir configurar porcentajes personalizados de cobro de gastos variables, granularmente por categoría (ej. Luz al 30%, Internet al 50%).
    - *Arquitectura:* En `TenantManager.App.ViewModels`, el método `ComputeVariableExpense` itera sobre las facturas imputables del mes y busca si existe una excepción para la categoría de dicha factura; si existe, aplica ese porcentaje, de lo contrario aplica el porcentaje base del contrato (`VariableExpensePercentage`).
    - *Interfaz:* Se diseñó un panel en `ContractsView.axaml` dentro del formulario de edición de contrato (aislado para pantallas pequeñas) que permite añadir y borrar estas excepciones de la base de datos de manera intuitiva y reactiva. Los inputs formatados de porcentaje (`0.00 %`) facilitan la lectura.
    - *Motivo:* Soportar contratos asimétricos o acuerdos especiales donde un inquilino no asume una parte lineal de todos los gastos del hogar, sino que asume diferentes responsabilidades (o queda exento de ellas al 0%) dependiendo de la naturaleza del gasto (categoría de la factura).
