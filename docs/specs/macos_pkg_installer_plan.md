# Plan de Implementación: Instalador PKG para macOS

## Objetivo
Automatizar la instalación de TenantManager en macOS distribuyendo un paquete `.pkg` nativo en lugar de un `.dmg`. El paquete incluirá un script de post-instalación (postinstall) que se encargará automáticamente de eliminar los atributos de cuarentena (`com.apple.quarantine`) de la aplicación una vez copiada en `/Applications`, permitiendo al usuario ejecutarla directamente sin tener que utilizar la terminal.

## Herramientas a Utilizar
Se empleará la herramienta nativa de macOS `pkgbuild`, que viene preinstalada en el sistema, por lo que no será necesario instalar dependencias de terceros.

## Pasos de la Implementación

### 1. Directorios de Empaquetado
Crearemos la estructura de soporte de instalación dentro de un nuevo directorio `installer/macos/`.
- `installer/macos/scripts/`: Contendrá los scripts de instalación de Apple (específicamente `postinstall`).

### 2. Creación del Script `postinstall`
Se creará el archivo `installer/macos/scripts/postinstall` (sin extensión) y se le asignarán permisos de ejecución (`chmod +x`). 
Contenido del script:
```bash
#!/bin/bash
# postinstall
# Este script se ejecuta con permisos elevados tras copiar la app a /Applications

APP_PATH="/Applications/TenantManager.app"

if [ -d "$APP_PATH" ]; then
    # Limpieza general de atributos extendidos y cuarentena
    xattr -cr "$APP_PATH" || true
    xattr -rd com.apple.quarantine "$APP_PATH" || true
    
    # Asegurar que el binario es ejecutable
    chmod -R 755 "$APP_PATH"
fi

exit 0
```

### 3. Script de Generación del Instalador (`build-mac-installer.sh`)
Se creará un script dedicado `build-mac-installer.sh` (aislado de `run-mac.sh` que usamos solo para pruebas locales) que empaquete la versión de producción:
1. Compilar el proyecto en `Release`.
2. Ensamblar la estructura de `TenantManager.app` e inyectar el Info.plist e iconos.
3. Crear un directorio raíz de empaquetado temporal (`payload/`).
4. Mover `TenantManager.app` dentro de `payload/`.
5. Ejecutar `pkgbuild` para generar el instalador:
   ```bash
   pkgbuild --root payload \
            --identifier com.tenantmanager.app \
            --version 1.0.6 \
            --install-location /Applications \
            --scripts installer/macos/scripts \
            bin/TenantManager-macOS-arm64.pkg
   ```
6. Limpiar los archivos temporales de compilación.

## Experiencia Final del Usuario (UX)
1. El usuario descarga `TenantManager-macOS-arm64.pkg`.
2. **Primera ejecución:** Hace **Click Derecho -> Abrir** sobre el instalador (para sobreescribir el aviso inicial de Gatekeeper habitual en apps sin licencia de desarrollador de Apple).
3. Sigue el asistente de instalación estándar de macOS (Siguiente, Siguiente, Instalar).
4. El instalador coloca la app en la carpeta Aplicaciones y ejecuta silenciosamente el script `postinstall`, saltando así la cuarentena.
5. El usuario abre TenantManager desde su Launchpad de forma directa. No requiere intervención técnica.
