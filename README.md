# CloudFlare DNS Updater Service

This service updates all `A` records in all `zones` to the actual external ip.

# Build

```bash
docker-compose -f docker-compose.yml build
```

# Configure

Add your credentials to `docker-compose.yml`.
It is enough to fill `Email` and `ApiKey` **or just** `ApiToken`

```yaml
version: '3.4'

services:
  cloudflarednsupdater:
    image: cloudflarednsupdater
    container_name: cloudflarednsupdater
    build:
      context: .
      dockerfile: CloudFlareDnsUpdater/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - CloudFlare__Email=email@example.com
      - CloudFlare__ApiKey=yourApiKey
      # or
      - CloudFlare__ApiToken=yourApiToken
    restart: unless-stopped
```

# Run

```bash
docker-compose -f docker-compose.yml up -d
```

# Example output
![Output](https://github.com/zingz0r/CloudFlareDnsUpdater/blob/master/output.png)
