If you want to execute azurite as a docker container you can use:

## Docker

```bash
docker run -d \
  --name azurite \
  -p 10000:10000 \
  -p 10001:10001 \
  -p 10002:10002 \
  -v azurite-data:/data \
  mcr.microsoft.com/azure-storage/azurite
  ```

  ## Docker compose

  ```bash
docker-compose up -d
  ```
