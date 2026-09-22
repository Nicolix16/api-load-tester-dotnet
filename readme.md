# ApiLoadTester - Console Load Testing Tool

Aplicación de consola desarrollada en C# (.NET) para ejecutar pruebas de carga controlada y medir resiliencia sobre endpoints de Web APIs.

## Funcionalidades
- Endpoint parametrizable por consola.
- Control del nivel de concurrencia mediante `SemaphoreSlim`.
- Medición de latencia promedio, tasa de éxito y recuento de fallos.
- Ejecución de pruebas asíncronas paralelas.
- Configuración de varias rondas con condiciones diferentes.
- Comparación de peticiones, concurrencia, éxito, latencia y tiempo total por ronda.

## Requisitos
- .NET 8.0 SDK o superior.

## Ejecución
```bash
dotnet run
```

La aplicación solicita la URL, credenciales de prueba y número de rondas. Para cada ronda
se pueden cambiar el número de peticiones y la concurrencia; al final muestra una tabla
comparativa.


