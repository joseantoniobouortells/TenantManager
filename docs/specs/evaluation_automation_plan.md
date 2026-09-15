# Plan de Implementación: Automatización de Evaluación de Modelos y Mejoras de Resiliencia IA

## 1. Objetivos
1. **Automatizar el Benchmarking de Modelos:** Modificar la aplicación `TenantManager.Evaluation` para que descubra automáticamente los modelos disponibles en el servidor local (LM Studio / Ollama), ejecute la suite de pruebas contra cada uno de ellos de forma secuencial, y genere un informe comparativo final estableciendo qué modelo es el mejor.
2. **Mejorar la Resiliencia del Código (Core):** Solucionar los fallos encontrados durante las pruebas implementando un parser JSON defensivo y forzando la herencia del contexto semántico por código.

---

## 2. Fase 1: Mejoras de Resiliencia en `TenantManager.Core`

### 2.1. Parser JSON Defensivo (Trailing Commas y Basura)
Los LLMs a veces añaden comas adicionales al final de los arrays o propiedades en el JSON, lo que rompe la deserialización estricta de `.NET`.
- **Archivo:** `src/TenantManager.Core/Services/AI/AiQueryService.cs`
- **Acción:**
  1. Utilizar un bloque `Regex` para identificar el primer `{` y el último `}` en caso de que las comillas inversas (`` ``` ``) fallen o el LLM devuelva prosa adicional.
  2. Aplicar un reemplazo de Regex para limpiar *trailing commas* en diccionarios o arrays (ej. `,\s*}` -> `}`).
  3. Reemplazar los errores técnicos (`DESERIALIZE ERROR`) por un mensaje *user-friendly* en producción, pero mantener la traza en la consola de depuración.

### 2.2. Context Fallback Programático (Herencia C#)
Cuando el usuario hace preguntas de seguimiento temporal (ej. "¿Y en Abril?"), los LLMs pequeños suelen olvidar incluir el año porque asumen que ya lo sabemos.
- **Archivo:** `src/TenantManager.Core/Services/AI/SemanticRequestResolver.cs` (Método: `EnrichPlanWithPeriod`)
- **Acción:** 
  - Si el `SemanticQueryPlan` retornado por el LLM **no** tiene un filtro de campo `year`, pero el `AssistantContext` **sí** tiene un `LastYear` guardado, se debe inyectar el filtro `year` programáticamente en la lista de `Filters` del plan. Esto garantizará que las preguntas de seguimiento que dependan del contexto histórico siempre se resuelvan correctamente aunque el modelo sea menos capaz.

---

## 3. Fase 2: Automatización en `TenantManager.Evaluation`

### 3.1. Descubrimiento Automático de Modelos (API Rest)
- **Archivo:** `src/TenantManager.Evaluation/Program.cs`
- **Acción:** 
  1. Si no se provee el argumento `--model`, hacer una petición HTTP `GET` al endpoint `{endpoint}/models` (ej. `http://localhost:1234/v1/models`).
  2. Parsear la respuesta JSON (formato estándar de OpenAI compatible con LM Studio/Ollama) para extraer la lista de IDs de modelos (`data[].id`).
  3. Filtrar modelos que contengan palabras clave como `embed` o `embedding` para no ejecutar pruebas contra modelos no generativos.

### 3.2. Ejecución Secuencial y Recolección de Métricas
- **Archivo:** `src/TenantManager.Evaluation/Evaluator.cs` y `Program.cs`
- **Acción:**
  1. Crear una clase `ModelBenchmarkResult` para almacenar: `ModelName`, `Passed`, `Failed`, `ParseErrors`.
  2. Modificar el bucle principal en `Program.cs` para iterar sobre la lista de modelos obtenidos. Por cada modelo, se inicializará un `Evaluator`, se inyectará el nombre en los `Settings` del cliente local, y se ejecutará el `RunLiveAsync()`.
  3. El Evaluator acumulará los resultados y los retornará a `Program.cs`.

### 3.3. Resumen y Veredicto Final
- **Archivo:** `src/TenantManager.Evaluation/Program.cs`
- **Acción:** 
  - Una vez que todos los modelos hayan terminado, limpiar la consola e imprimir una tabla resumen en formato Markdown o ASCII.
  - Ordenar los modelos por cantidad de tests pasados (de mayor a menor).
  - Imprimir un "Veredicto" indicando cuál es el modelo recomendado para el usuario basándose en la tasa de éxito.

---

## 4. Fase 3: Ajustes en Escenarios de Prueba

### 4.1. Fast-Fails (`tenant-unknown-en.json`)
- **Archivo:** `evaluation/scenarios/en/tenant-unknown-en.json`
- **Acción:** Cambiar la expectativa `"queryExecution": "required"` por `"queryExecution": "not-required"`. El `SemanticQueryExecutor` bloquea correctamente la consulta a la BD si detecta que la entidad principal (ej. "Alice") no existe tras normalizar, por lo que nunca llega a ejecutar el árbol LINQ completo. El test debe reflejar este atajo de seguridad como un éxito, no como un fallo.
