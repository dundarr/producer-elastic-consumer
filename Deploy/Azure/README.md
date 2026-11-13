# Demo en Azure Kubernetes Service

La demo usa AKS, Azure Container Registry (ACR) y Azure Queue Storage. El panel y el productor se ejecutan en el espacio de nombres `default`.

## Preparar AKS y publicar las imágenes

Los ejemplos usan los nombres del proyecto. Ajusta las variables y las referencias a `pcdemoacr.azurecr.io` en los manifiestos si usas otro registro. El clúster debe tener acceso de lectura al ACR y acceso de red al servicio de colas de la cuenta de almacenamiento. Consulta la [integración de AKS con ACR](https://learn.microsoft.com/en-us/azure/aks/cluster-container-registry-integration).

Desde la raíz del repositorio, con la sesión de Azure iniciada:

```powershell
$resourceGroup = 'producer-consumer-demo-rg'
$cluster = 'producer-consumer-demo-aks'
$registry = 'pcdemoacr'
$storage = 'prodconsdemost'

az acr build --registry $registry --image producer:latest Producer
az acr build --registry $registry --image consumer:latest Consumer
az acr build --registry $registry --image dashboard:latest blazor-ui

az aks get-credentials --resource-group $resourceGroup --name $cluster
kubectl --context $cluster get nodes
```

Instala KEDA 2.18.2, la versión de la demo local, si el clúster carece de KEDA. Si ya tiene KEDA, incluido el complemento administrado de AKS, usa esa instalación y comprueba que admite los escaladores `azure-queue` y `metrics-api`. Consulta la [documentación de KEDA en AKS](https://learn.microsoft.com/en-us/azure/aks/keda-about).

```powershell
kubectl --context $cluster apply --server-side -f https://github.com/kedacore/keda/releases/download/v2.18.2/keda-2.18.2.yaml
kubectl --context $cluster -n keda wait --for=condition=Available deployment --all --timeout=180s
```

## Configurar la cola y desplegar

Crea la cola `jobs` si aún no existe y guarda la conexión en el Secret que comparten el productor, el consumidor y KEDA. Estos comandos usan la clave de la cuenta de almacenamiento.

```powershell
$connection = az storage account show-connection-string --resource-group $resourceGroup --name $storage --query connectionString --output tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($connection)) {
    throw 'No se pudo obtener la conexión de Azure Storage.'
}
try {
    $env:AZURE_STORAGE_CONNECTION_STRING = $connection
    az storage queue create --name jobs --only-show-errors --output none
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar la cola jobs.' }

    kubectl --context $cluster -n default create secret generic azure-queue-connection --from-literal="connection=$connection" --dry-run=client -o yaml |
        kubectl --context $cluster -n default apply --server-side -f -
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar la conexión en AKS.' }
} finally {
    Remove-Item Env:AZURE_STORAGE_CONNECTION_STRING -ErrorAction SilentlyContinue
    Remove-Variable connection
}

kubectl --context $cluster -n default apply -f Deploy/Azure
kubectl --context $cluster -n default rollout status deployment/producer --timeout=180s
kubectl --context $cluster -n default rollout status deployment/dashboard --timeout=180s
kubectl --context $cluster -n default wait --for=condition=Ready scaledobject/consumer-queue-scaler --timeout=120s
```

Si vuelves a publicar la etiqueta `latest` y el manifiesto del despliegue sigue igual, reinicia el productor y el panel con `kubectl --context $cluster -n default rollout restart deployment/producer deployment/dashboard`. Si hay consumidores en ejecución, reinicia también `deployment/consumer`. Las nuevas réplicas descargan la imagen de ACR al arrancar.

## Abrir la demo

El panel y Swagger se abren mediante túneles locales. Ejecuta cada comando en una terminal.

```powershell
kubectl --context producer-consumer-demo-aks -n default port-forward service/dashboard 5018:8080
kubectl --context producer-consumer-demo-aks -n default port-forward service/producer 3000:3000
```

Abre el [panel](http://localhost:5018/) o [Swagger](http://localhost:3000/swagger/index.html). Si ya tienes la demo local abierta en esos puertos, cambia el número que precede a los dos puntos.

Con **5 msg/s** configurados y el productor activo, el ritmo pide **3 consumidores**. La cola puede pedir más réplicas si acumula mensajes, con un objetivo de **5 mensajes por réplica** y un máximo de **20**. Al pausar y vaciar la cola, KEDA espera **60 segundos** de inactividad antes de bajar a cero. Si cambias el límite de **2 msg/s por consumidor**, actualiza también el `targetValue` de `metrics-api` en el manifiesto.
