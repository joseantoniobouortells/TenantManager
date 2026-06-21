# Hard Spec - MVP Local Tenant Manager

## Objetivo

Crear una aplicación desktop multiplataforma para gestionar habitaciones alquiladas, inquilinos, contratos enlazados por ruta local y pagos mensuales.

## Contexto

El usuario alquila habitaciones en un piso y necesita una herramienta sencilla para centralizar la información operativa: ocupación, contratos y pagos.

La aplicación será inicialmente de uso personal y local. No se plantea como SaaS en esta fase.

## Usuario objetivo

Propietario particular que gestiona un piso alquilado por habitaciones.

## Alcance

Esta versión incluye:

- Gestión de habitaciones.
- Gestión de inquilinos.
- Asociación de inquilinos a habitaciones.
- Gestión de contratos mediante rutas locales de fichero.
- Validación de existencia del fichero de contrato.
- Apertura del contrato con la aplicación predeterminada del sistema.
- Gestión manual de pagos mensuales.
- Vista resumen mínima con ocupación y pagos pendientes.
- Base de datos local SQLite.
- Aplicación desktop multiplataforma con Avalonia.

## Fuera de alcance

Esta versión no incluye:

- CRM de candidatos.
- Gestión de varios pisos.
- Usuarios o login.
- Sincronización en la nube.
- Backend separado.
- API HTTP.
- App móvil.
- Portal del inquilino.
- Firma digital.
- Generación automática de contratos.
- Pagos online.
- Integración bancaria.
- Almacenamiento de documentos como BLOB.
- Facturación o fiscalidad.
- Notificaciones automáticas.

## Requisitos funcionales

- RF-001: El usuario puede crear, editar, listar y desactivar habitaciones.
- RF-002: Una habitación tiene nombre, renta mensual, estado activo/inactivo y notas opcionales.
- RF-003: El usuario puede crear, editar, listar y desactivar inquilinos.
- RF-004: Un inquilino tiene nombre, teléfono opcional, email opcional, fecha de entrada, fecha de salida opcional, fianza y notas opcionales.
- RF-005: Un inquilino activo puede estar asociado a una habitación.
- RF-006: El usuario puede asociar uno o varios contratos a un inquilino.
- RF-007: Cada contrato guarda una ruta local de fichero.
- RF-008: La aplicación no guarda el contenido del contrato en la base de datos.
- RF-009: La aplicación muestra si la ruta del contrato existe o está rota.
- RF-010: El usuario puede abrir el contrato desde la aplicación usando la aplicación predeterminada del sistema.
- RF-011: El usuario puede crear y editar pagos mensuales por inquilino.
- RF-012: Un pago mensual tiene año, mes, importe esperado, importe pagado, estado, fecha de pago opcional y notas opcionales.
- RF-013: No puede haber dos pagos para el mismo inquilino, año y mes.
- RF-014: La aplicación muestra una vista resumen con habitaciones ocupadas y pagos pendientes del mes actual.

## Requisitos técnicos

- RT-001: La aplicación será desktop multiplataforma.
- RT-002: La aplicación usará .NET y C#.
- RT-003: La interfaz se implementará con Avalonia.
- RT-004: La persistencia usará SQLite.
- RT-005: El acceso a datos usará Entity Framework Core.
- RT-006: La base de datos se almacenará localmente.
- RT-007: Los contratos se almacenarán como rutas de sistema de ficheros.
- RT-008: La aplicación debe funcionar sin conexión.
- RT-009: El proyecto debe poder mantenerse por una sola persona.
- RT-010: El MVP debe evitar arquitectura innecesaria.

## Restricciones

- No implementar funcionalidades fuera del alcance.
- No añadir autenticación.
- No añadir backend separado.
- No añadir API HTTP.
- No añadir sincronización cloud.
- No añadir pagos online.
- No guardar ficheros en SQLite.
- No introducir arquitectura compleja.
- No añadir dependencias sin justificación.
- No implementar CRM en esta fase.
- No implementar soporte multi-piso en esta fase.

## Criterios de aceptación

- CA-001: Se puede crear una habitación.
- CA-002: Se puede crear un inquilino asociado a una habitación.
- CA-003: Se puede guardar una ruta de contrato asociada a un inquilino.
- CA-004: La aplicación indica si el fichero del contrato existe.
- CA-005: La aplicación permite abrir el contrato desde su ruta.
- CA-006: Se puede crear un pago mensual para un inquilino.
- CA-007: No se permite duplicar pago para el mismo inquilino, año y mes.
- CA-008: Se pueden ver pagos pendientes del mes actual.
- CA-009: Se pueden ver habitaciones ocupadas.
- CA-010: Los datos persisten al cerrar y abrir la aplicación.
- CA-011: La aplicación funciona sin conexión.

## Tests esperados

- Test de creación de habitación.
- Test de creación de inquilino.
- Test de asociación inquilino-habitación.
- Test de creación de contrato con ruta.
- Test de detección de ruta existente.
- Test de detección de ruta rota.
- Test de creación de pago mensual.
- Test de prevención de pago duplicado.
- Test de consulta de pagos pendientes del mes actual.

## Riesgos

- Las rutas de contrato pueden romperse si los ficheros se mueven.
- La aplicación puede crecer demasiado si se añade CRM antes de validar el núcleo.
- Avalonia puede requerir ajustes específicos para apertura de ficheros en cada sistema operativo.
- La gestión de datos personales requerirá más cuidado si el proyecto evoluciona a producto comercial.
- El diseño visual puede consumir tiempo sin aportar validación real al MVP.

## Decisiones tomadas

- La primera versión usará un solo proyecto Avalonia con carpetas internas para Domain, Data, Services, ViewModels y Views.
- El MVP será una aplicación desktop multiplataforma.
- Se usará Avalonia.
- Se usará .NET y C#.
- Se usará SQLite.
- Se usará Entity Framework Core.
- Los contratos se enlazarán por ruta local.
- Los contratos no se incrustarán en la base de datos.
- El CRM queda fuera del primer MVP.
- La aplicación será local y sin backend separado.

## Dudas abiertas

- Ubicación exacta de la base de datos local.
- Si las rutas de contrato serán absolutas o relativas a una carpeta base configurable.
- Si los tests se crearán desde la fase inicial o después de estabilizar dominio y persistencia.
