# Demo local de Kubernetes con Azurite y KEDA

Usa un clúster local con Docker Desktop o Kind. Comprueba el contexto antes de aplicar los manifiestos.

```powershell
kubectl config current-context
```

## Preparar las imágenes

Desde la raíz del repositorio:

```powershell
docker build --provenance=false -t producer:latest Producer
docker build --provenance=false -t consumer:latest Consumer
docker build --provenance=false -t dashboard:latest blazor-ui
```

Si usas Kind, carga las imágenes en el clúster. Sustituye `<nombre>` por el nombre del clúster.

```powershell
kind load docker-image --name <nombre> producer:latest consumer:latest dashboard:latest
```

## Instalar KEDA y desplegar

Si el clúster todavía no tiene KEDA, instala la versión utilizada para la demo y espera a que arranque.

```powershell
kubectl apply --server-side -f https://github.com/kedacore/keda/releases/download/v2.18.2/keda-2.18.2.yaml
kubectl -n keda wait --for=condition=Available deployment --all --timeout=180s
```

Aplica los manifiestos en el espacio de nombres `default`. La conexión a Azurite utiliza el nombre DNS del servicio en ese espacio de nombres.

```powershell
kubectl -n default apply -f "Deploy/Local docker kubernetes"
kubectl -n default port-forward service/dashboard 8080:8080
```

## Ver el autoescalado

1. Abre [el panel](http://localhost:8080). En otra terminal, ejecuta `kubectl -n default get deployment consumer --watch`.
2. Aplica un ritmo de **5 msg/s** e inicia la producción. El ritmo pide **3 consumidores** porque cada uno tiene un límite configurado de 2 msg/s. La cola puede pedir más réplicas si se acumula trabajo.
3. Pausa la producción y deja que la cola se vacíe. KEDA vuelve a cero tras 60 segundos sin producción ni mensajes en cola.

Kubernetes usa la mayor de las dos estimaciones de réplicas, con un máximo de 20. Divide el ritmo configurado entre 2 msg/s por consumidor y el recuento de la cola entre 5, redondeando hacia arriba. Una vez arrancada la primera réplica, puede añadir hasta dos cada 15 segundos. La reducción usa una ventana de estabilización de 30 segundos. Si cambias el límite del consumidor, actualiza `targetValue` en el escalador local.

La gráfica usa el recuento aproximado de Azure Queue y los registros de actividad del consumidor. Las réplicas que informa Kubernetes pueden tardar unos segundos en aparecer en el panel. Consulta la [documentación del escalador de Azure Storage Queue](https://keda.sh/docs/2.18/scalers/azure-storage-queue/) para ver cómo se calcula el objetivo.

## Limpiar la demo

```powershell
kubectl -n default delete -f "Deploy/Local docker kubernetes"
```

Azurite usa almacenamiento temporal. Al eliminar su pod se borran los mensajes de la demo.
