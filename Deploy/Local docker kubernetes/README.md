# Local Kubernetes Demo: Azurite + KEDA + Queue-Based Scaling

This project demonstrates event-driven autoscaling using:
- **Azurite** as a local Azure Storage Queue emulator
- **KEDA** to scale a consumer application based on Azure Queue length
- A **producer** service (exposed externally) to enqueue messages
- A **consumer** deployment that scales from 0 to 20 replicas based on the `jobs` queue

Everything is designed to run on a **local Kubernetes cluster** using **Docker Desktop** (or any single-node Kubernetes provided by Docker).

## Prerequisites

- Docker Desktop with Kubernetes enabled
- `kubectl` configured to use the local cluster (e.g., `docker-desktop` context)
- KEDA **not** pre-installed (instructions below will install it)

## Step 1: Install KEDA

KEDA is required for the `ScaledObject` and `TriggerAuthentication` resources.

```bash
kubectl apply -f https://github.com/kedacore/keda/releases/latest/download/keda.yaml

Wait until all pods in the keda namespace are running:

kubectl get pods -n keda
```

## Step 2: Apply the manifests

Apply the YAML files in the following order:
```bash
# 1. Azurite (storage emulator)
kubectl apply -f k8s_azurite_deployment.yaml
kubectl apply -f k8s_azurite_service.yaml

# 2. Connection secret and KEDA authentication
kubectl apply -f k8s_keda_secret.yaml
kubectl apply -f k8s_keda_trigger_auth.yaml

# 3. Service alias in KEDA namespace (needed for connection string resolution)
kubectl apply -f k8s_keda_alias_for_azurite.yaml

# 4. Producer (enqueues messages)
kubectl apply -f k8s_producer_deployment.yaml
kubectl apply -f k8s_producer_service.yaml

# 5. Consumer (scaled by KEDA)
kubectl apply -f k8s_consumer_deployment.yaml

# 6. KEDA ScaledObject (the actual scaler)
kubectl apply -f k8s_scaled_object.yaml
```

## Step 3: Verify the deployment

```bash
kubectl get all -A | grep -E "azurite|producer|consumer|keda"
```

You should see:

azurite pod running
producer pod running
consumer deployment with 0 replicas initially
KEDA pods in the keda name

## Step 4: Test the setup

### Access the Producer

The producer is exposed on 3000:

Open
```bash
http://localhost:3000/swagger/index.html
```
in your browser.
Use the producer's API/UI to enqueue messages into the jobs queue (endpoint depends on your producer implementation).

### Observe scaling
Watch the consumer scale:

```bash
kubectl get deployment consumer --watch
```

When the jobs queue has 5 or more messages, KEDA will scale the consumer up. When the queue is empty, it scales back to 0.

## Cleanup

Remove everything (in reverse order):
```bash
kubectl delete -f k8s_scaled_object.yaml
kubectl delete -f k8s_consumer_deployment.yaml
kubectl delete -f k8s_producer_deployment.yaml
kubectl delete -f k8s_producer_service.yaml
kubectl delete -f k8s_keda_trigger_auth.yaml
kubectl delete -f k8s_keda_secret.yaml
kubectl delete -f k8s_keda_alias_for_azurite.yaml
kubectl delete -f k8s_azurite_service.yaml
kubectl delete -f k8s_azurite_deployment.yaml
```

## Important notes

The connection string uses the service name azurite. The alias in the keda namespace ensures KEDA can resolve it correctly.
Azurite data is stored in an emptyDir volume → data is lost if the pod restarts.
Consumer starts at 0 replicas to demonstrate true scale-from-zero.
Images producer:latest and consumer:latest must be built and available locally or in a reachable registry.

Sometimes I needed to execute :
```bash
kubectl rollout restart deploy/keda-operator -n keda
```
as it seems keda-operator kind of hangs sometimes.