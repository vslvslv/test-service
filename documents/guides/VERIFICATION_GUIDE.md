# Test Service - Verification Steps

## ? Step 1: Verify Build

**Status**: PASSED ?

```bash
dotnet build test-service.sln
```

The solution builds successfully with 0 errors.

---

## Step 2: Start Infrastructure

### Option A: Using Docker Desktop (Recommended)

1. **Start Docker Desktop** - Make sure Docker Desktop is running on your machine

2. **Start MongoDB and RabbitMQ**:
   ```bash
   # Windows
   .\start-infrastructure.bat
   
   # Linux/Mac
   ./start-infrastructure.sh
   
   # Or manually
   docker compose up -d
   ```

3. **Verify services are running**:
   ```bash
   docker ps
   ```
   
   You should see:
   - `test-service-mongodb` running on port 27017
   - `test-service-rabbitmq` running on ports 5672 and 15672

4. **Access RabbitMQ Management UI**:
   - URL: http://localhost:15672
   - Username: `guest`
   - Password: `guest`

### Option B: Manual Installation (Without Docker)

If you prefer not to use Docker, install MongoDB and RabbitMQ manually:

#### MongoDB
- Download from: https://www.mongodb.com/try/download/community
- Install and start the service
- Default connection: `mongodb://localhost:27017`

#### RabbitMQ
- Download from: https://www.rabbitmq.com/download.html
- Install and start the service
- Default connection: `localhost:5672`
- Enable management plugin: `rabbitmq-plugins enable rabbitmq_management`

---

## Step 3: Run the API

```bash
cd TestService.Api
dotnet run
```

**Expected Output**:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**Access Points**:
- API: https://localhost:5001
- Swagger UI: https://localhost:5001/swagger
- HTTP: http://localhost:5000

---

## Step 4: Test the API Manually

### Using Swagger UI

1. Navigate to https://localhost:5001/swagger
2. Try the following operations:

#### Create Test Data (POST)
```json
{
  "name": "Test Item 1",
  "value": 150.75,
  "category": "Electronics",
  "metadata": {
    "brand": "TestBrand",
    "model": "Model-X"
  }
}
```

#### Get All Test Data (GET)
- Endpoint: `/api/testdata`

#### Get By Category (GET)
- Endpoint: `/api/testdata/category/Electronics`

#### Get Aggregated Data (GET)
- Endpoint: `/api/testdata/aggregated`

### Using cURL

```bash
# Create test data
curl -X POST https://localhost:5001/api/testdata -H "Content-Type: application/json" -d "{\"name\":\"Test Item\",\"value\":100.5,\"category\":\"TestCategory\"}" -k

# Get all test data
curl -X GET https://localhost:5001/api/testdata -k

# Get by category
curl -X GET https://localhost:5001/api/testdata/category/TestCategory -k

# Get aggregated data
curl -X GET https://localhost:5001/api/testdata/aggregated -k
```

### Using PowerShell

```powershell
# Create test data
$body = @{
    name = "Test Item"
    value = 100.5
    category = "TestCategory"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:5001/api/testdata" -Method Post -Body $body -ContentType "application/json" -SkipCertificateCheck

# Get all test data
Invoke-RestMethod -Uri "https://localhost:5001/api/testdata" -SkipCertificateCheck

# Get by category
Invoke-RestMethod -Uri "https://localhost:5001/api/testdata/category/TestCategory" -SkipCertificateCheck

# Get aggregated data
Invoke-RestMethod -Uri "https://localhost:5001/api/testdata/aggregated" -SkipCertificateCheck
```

---

## Step 5: Run Automated Tests

**Important**: Make sure MongoDB and RabbitMQ are running before running tests!

```bash
cd TestService.Tests
dotnet test
```

**Expected Output**:
```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10
```

### Test Coverage

The test suite includes:
1. ? Get all test data
2. ? Get test data returns JSON
3. ? Create test data with valid data
4. ? Get by ID with existing ID
5. ? Get by ID with non-existing ID returns 404
6. ? Get by category filters results
7. ? Update test data
8. ? Delete test data
9. ? Get aggregated data by category

---

## Step 6: Verify Message Bus Integration

1. **Access RabbitMQ Management UI**: http://localhost:15672

2. **Navigate to "Queues and Streams"**

3. **You should see**:
   - Exchange: `test-service-exchange`
   - Queue: `test-data-queue`

4. **Create test data via API** and check the queue for messages

5. **Monitor messages** in the queue to verify events are being published

---

## Step 7: Verify Database

### Using MongoDB Compass (GUI)

1. Download MongoDB Compass: https://www.mongodb.com/try/download/compass
2. Connect to: `mongodb://localhost:27017`
3. Navigate to database: `TestServiceDb`
4. View collection: `TestData`

### Using MongoDB Shell

```bash
# Connect to MongoDB
mongosh

# Switch to database
use TestServiceDb

# Show all documents
db.TestData.find()

# Count documents
db.TestData.countDocuments()

# Find by category
db.TestData.find({ category: "Electronics" })
```

---

## Troubleshooting

### Issue: Docker not running
**Error**: `The system cannot find the file specified`
**Solution**: Start Docker Desktop

### Issue: Port already in use
**Error**: `Address already in use`
**Solution**: 
```bash
# Check what's using the port
netstat -ano | findstr :27017
netstat -ano | findstr :5672

# Stop the process or change the port in appsettings.json
```

### Issue: Connection refused
**Error**: `No connection could be made`
**Solution**: 
- Make sure MongoDB and RabbitMQ are running
- Check firewall settings
- Verify ports are accessible

### Issue: Tests fail
**Error**: Various test failures
**Solution**: 
1. Ensure infrastructure is running
2. Check if ports are accessible
3. Clear test data: `docker compose down -v && docker compose up -d`

---

## Cleanup

### Stop the API
Press `Ctrl+C` in the terminal where the API is running

### Stop Infrastructure
```bash
# Stop containers but keep data
docker compose stop

# Stop and remove containers (keeps data volumes)
docker compose down

# Stop, remove containers and delete all data
docker compose down -v
```

---

## Summary Checklist

- [ ] Solution builds successfully
- [ ] Docker Desktop is running
- [ ] MongoDB container is running on port 27017
- [ ] RabbitMQ container is running on ports 5672 and 15672
- [ ] API starts and runs on https://localhost:5001
- [ ] Swagger UI is accessible
- [ ] Can create test data via API
- [ ] Can retrieve test data via API
- [ ] Can filter by category
- [ ] Can get aggregated data
- [ ] All automated tests pass (10/10)
- [ ] Messages appear in RabbitMQ queue
- [ ] Data is stored in MongoDB database

---

## Next Steps

Once everything is verified:

1. **Explore the API** - Try different operations through Swagger UI
2. **Add more test data** - Create items in different categories
3. **Monitor RabbitMQ** - Watch messages flow through the system
4. **View MongoDB data** - See how documents are stored
5. **Extend functionality** - Add new features or modify existing ones
6. **Deploy** - Consider deploying to Azure, AWS, or other cloud platforms

---

## Support

For issues or questions:
- Check the README.md for detailed documentation
- Review the code comments in the source files
- Verify all prerequisites are installed and running
