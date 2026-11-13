# Queue Lab

Demo de autoescalado en Kubernetes. El productor .NET escribe en la cola `jobs` de Azure Queue Storage, KEDA ajusta el número de réplicas del consumidor y el panel Blazor permite cambiar la carga y observar el resultado.

- [Demo completa en Kubernetes con Azurite](Deploy/Local%20docker%20kubernetes/README.md)
- [Despliegue en Azure](Deploy/Azure/README.md)

## Ejecutar sin Kubernetes

Con el SDK de .NET 10 y Docker, inicia Azurite desde la raíz del repositorio.

```powershell
docker compose -f "Deploy/Local azurite only/docker-compose.yaml" up -d
```

Ejecuta cada proyecto en su propia terminal.

```powershell
dotnet run --project Producer/Producer
dotnet run --project Consumer/Consumer
dotnet run --project blazor-ui/Dashboard/Dashboard --launch-profile http
```

Abre [el panel](http://localhost:5018). En este modo, los consumidores son los procesos que arranques manualmente. Puedes consultar la [API del productor](http://localhost:3000/swagger).

## Pruebas

```powershell
dotnet test Producer/Producer.UnitTests
dotnet test Consumer/Consumer.UnitTests
dotnet test Producer/Producer.IntegrationTests
```

Las pruebas de integración requieren Azurite. El consumidor vuelve a intentar mensajes fallidos y, al alcanzar el límite, los copia a la cola de mensajes fallidos antes de borrar el original. Azure Queue entrega los mensajes al menos una vez, por lo que un reintento puede producir duplicados.
