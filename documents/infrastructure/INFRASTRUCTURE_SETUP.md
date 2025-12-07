# Infrastructure Documentation

## MongoDB Setup

### Local Development

**Using Docker:**
```bash
docker run -d \
  --name testservice-mongodb \
  -p 27017:27017 \
  -e MONGO_INITDB_ROOT_USERNAME=admin \
  -e MONGO_INITDB_ROOT_PASSWORD=password123 \
  -v mongodb_data:/data/db \
  mongo:latest
```

**Connection String:**
```
mongodb://admin:password123@localhost:27017/TestServiceDb?authSource=admin
```

### Production Setup

**Using Docker Compose:**
```yaml
version: '3.8'
services:
  mongodb:
    image: mongo:latest
    container_name: testservice-mongodb
    environment:
      MONGO_INITDB_ROOT_USERNAME: ${MONGO_USER}
      MONGO_INITDB_ROOT_PASSWORD: ${MONGO_PASSWORD}
    ports:
      - "27017:27017"
    volumes:
      - mongodb_data:/data/db
      - ./mongo-init:/docker-entrypoint-initdb.d
    networks:
      - testservice-network
    restart: unless-stopped

volumes:
  mongodb_data:

networks:
  testservice-network:
    driver: bridge
```

### Database Initialization

**Collections Created:**
- `entities` - Dynamic entities
- `schemas` - Entity schemas
- `users` - User accounts
- `environments` - Environment configurations

### Indexes

```javascript
// Entities collection indexes
db.entities.createIndex({ "entityType": 1, "isConsumed": 1 })
db.entities.createIndex({ "entityType": 1, "environment": 1 })
db.entities.createIndex({ "entityType": 1, "isConsumed": 1, "environment": 1 })

// Schemas collection indexes
db.schemas.createIndex({ "entityName": 1 }, { unique: true })

// Users collection indexes
db.users.createIndex({ "username": 1 }, { unique: true })

// Environments collection indexes
db.environments.createIndex({ "name": 1 }, { unique: true })
```

### Backup & Restore

**Backup:**
```bash
# Backup all databases
docker exec testservice-mongodb mongodump \
  --username admin \
  --password password123 \
  --authenticationDatabase admin \
  --out /backup

# Copy backup to host
docker cp testservice-mongodb:/backup ./mongodb-backup
```

**Restore:**
```bash
# Copy backup to container
docker cp ./mongodb-backup testservice-mongodb:/backup

# Restore
docker exec testservice-mongodb mongorestore \
  --username admin \
  --password password123 \
  --authenticationDatabase admin \
  /backup
```

### Monitoring

**Check Status:**
```bash
docker exec testservice-mongodb mongosh \
  --username admin \
  --password password123 \
  --authenticationDatabase admin \
  --eval "db.adminCommand('serverStatus')"
```

**View Databases:**
```bash
docker exec testservice-mongodb mongosh \
  --username admin \
  --password password123 \
  --authenticationDatabase admin \
  --eval "show dbs"
```

---

## RabbitMQ Setup

### Local Development

**Using Docker:**
```bash
docker run -d \
  --name testservice-rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=guest \
  -e RABBITMQ_DEFAULT_PASS=guest \
  rabbitmq:3-management
```

**Access Management UI:**
- URL: http://localhost:15672
- Username: guest
- Password: guest

### Production Setup

**Using Docker Compose:**
```yaml
version: '3.8'
services:
  rabbitmq:
    image: rabbitmq:3-management
    container_name: testservice-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    networks:
      - testservice-network
    restart: unless-stopped

volumes:
  rabbitmq_data:

networks:
  testservice-network:
    driver: bridge
```

### Message Queues

**Queues Created:**
- Entity events (created, updated, deleted, consumed)
- Schema events
- User events
- Environment events

**Exchange Configuration:**
```
Type: topic
Durable: true
Auto-delete: false
```

**Routing Keys:**
```
{entityType}.created
{entityType}.updated
{entityType}.deleted
{entityType}.consumed
```

### Monitoring

**View Queues:**
```bash
# Via Management UI
http://localhost:15672/#/queues

# Via CLI
docker exec testservice-rabbitmq rabbitmqctl list_queues
```

**View Connections:**
```bash
docker exec testservice-rabbitmq rabbitmqctl list_connections
```

---

## Complete Infrastructure Setup

### Docker Compose (All Services)

```yaml
version: '3.8'

services:
  mongodb:
    image: mongo:latest
    container_name: testservice-mongodb
    environment:
      MONGO_INITDB_ROOT_USERNAME: admin
      MONGO_INITDB_ROOT_PASSWORD: ${MONGO_PASSWORD:-password123}
    ports:
      - "27017:27017"
    volumes:
      - mongodb_data:/data/db
    networks:
      - testservice-network
    healthcheck:
      test: echo 'db.runCommand("ping").ok' | mongosh localhost:27017/test --quiet
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  rabbitmq:
    image: rabbitmq:3-management
    container_name: testservice-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD:-guest}
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    networks:
      - testservice-network
    healthcheck:
      test: rabbitmq-diagnostics -q ping
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: testservice-api
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:80
      MongoDB__ConnectionString: mongodb://admin:${MONGO_PASSWORD:-password123}@mongodb:27017/TestServiceDb?authSource=admin
      RabbitMQ__HostName: rabbitmq
      RabbitMQ__UserName: guest
      RabbitMQ__Password: ${RABBITMQ_PASSWORD:-guest}
      Jwt__Secret: ${JWT_SECRET}
      Jwt__Issuer: TestService
      Jwt__Audience: TestServiceUsers
    ports:
      - "5000:80"
    depends_on:
      mongodb:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    networks:
      - testservice-network
    restart: unless-stopped

  web:
    build:
      context: ./testservice-web
      dockerfile: Dockerfile
    container_name: testservice-web
    environment:
      VITE_API_BASE_URL: http://api
    ports:
      - "3000:80"
    depends_on:
      - api
    networks:
      - testservice-network
    restart: unless-stopped

volumes:
  mongodb_data:
  rabbitmq_data:

networks:
  testservice-network:
    driver: bridge
```

### Environment Variables

Create `.env` file:

```env
# MongoDB
MONGO_PASSWORD=your_secure_mongo_password

# RabbitMQ
RABBITMQ_PASSWORD=your_secure_rabbitmq_password

# JWT
JWT_SECRET=your_very_long_and_secure_jwt_secret_key_at_least_32_characters

# API
ASPNETCORE_ENVIRONMENT=Production
```

### Quick Start

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down

# Stop and remove volumes (CAUTION: deletes data)
docker-compose down -v
```

### Health Checks

```bash
# MongoDB
curl http://localhost:27017

# RabbitMQ
curl http://localhost:15672

# API
curl http://localhost:5000/health

# Web
curl http://localhost:3000
```

---

## Production Considerations

### Security

1. **Change Default Passwords**
   - MongoDB admin password
   - RabbitMQ password
   - JWT secret

2. **Enable TLS/SSL**
   - MongoDB connection encryption
   - RabbitMQ TLS
   - HTTPS for API and Web

3. **Network Isolation**
   - Use private networks
   - Restrict port access
   - Use firewall rules

4. **Authentication**
   - Enable MongoDB authentication
   - Configure RabbitMQ users
   - Implement API authentication

### Scalability

1. **MongoDB Replica Set**
   - High availability
   - Read scaling
   - Automatic failover

2. **RabbitMQ Cluster**
   - Message distribution
   - Load balancing
   - High availability

3. **API Scaling**
   - Multiple API instances
   - Load balancer
   - Session management

### Monitoring

1. **MongoDB**
   - MongoDB Atlas (cloud)
   - Prometheus exporter
   - Custom monitoring scripts

2. **RabbitMQ**
   - Management UI
   - Prometheus plugin
   - Alert configuration

3. **API**
   - Application Insights
   - Health check endpoints
   - Custom metrics

### Backup Strategy

1. **Automated Backups**
   - Daily MongoDB backups
   - RabbitMQ message persistence
   - Configuration backups

2. **Backup Retention**
   - Keep 7 daily backups
   - Keep 4 weekly backups
   - Keep 12 monthly backups

3. **Disaster Recovery**
   - Test restore procedures
   - Document recovery steps
   - Maintain off-site backups

---

## Troubleshooting

### MongoDB Connection Issues

```bash
# Check if MongoDB is running
docker ps | grep mongodb

# View MongoDB logs
docker logs testservice-mongodb

# Test connection
docker exec testservice-mongodb mongosh \
  --username admin \
  --password password123 \
  --authenticationDatabase admin \
  --eval "db.adminCommand('ping')"
```

### RabbitMQ Connection Issues

```bash
# Check if RabbitMQ is running
docker ps | grep rabbitmq

# View RabbitMQ logs
docker logs testservice-rabbitmq

# Check RabbitMQ status
docker exec testservice-rabbitmq rabbitmqctl status
```

### API Connection Issues

```bash
# Check API logs
docker logs testservice-api

# Test API health
curl http://localhost:5000/health

# Check API configuration
docker exec testservice-api cat /app/appsettings.json
```

---

**For deployment instructions, see:** [DEPLOYMENT_GUIDE.md](../deployment/DEPLOYMENT_GUIDE.md)
