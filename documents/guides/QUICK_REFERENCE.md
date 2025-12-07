# Quick Reference Card

## ?? Quick Start (3 Steps)

```bash
# 1. Start infrastructure
docker compose up -d

# 2. Run API
cd TestService.Api && dotnet run

# 3. Open browser
https://localhost:5001/swagger
```

## ?? Common Commands

### Infrastructure
```bash
# Start services
docker compose up -d

# View running containers
docker ps

# Stop services
docker compose down

# Stop and remove data
docker compose down -v
```

### Development
```bash
# Build solution
dotnet build

# Run API
cd TestService.Api
dotnet run

# Run tests
cd TestService.Tests
dotnet test

# Watch mode (auto-restart on changes)
dotnet watch run
```

### Testing
```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test -v detailed

# Run specific test
dotnet test --filter "TestName~Create"
```

## ?? Quick Links

| Resource | URL |
|----------|-----|
| Swagger UI | https://localhost:5001/swagger |
| API Base | https://localhost:5001/api/testdata |
| RabbitMQ UI | http://localhost:15672 (guest/guest) |
| MongoDB | mongodb://localhost:27017 |

## ?? API Endpoints

```
GET    /api/testdata                    # Get all
GET    /api/testdata/{id}               # Get by ID
GET    /api/testdata/category/{cat}     # Get by category
GET    /api/testdata/aggregated         # Aggregated data
POST   /api/testdata                    # Create
PUT    /api/testdata/{id}               # Update
DELETE /api/testdata/{id}               # Delete
```

## ?? Example API Calls

### Create Test Data
```bash
curl -X POST https://localhost:5001/api/testdata \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Item",
    "value": 150.75,
    "category": "Electronics",
    "metadata": {"brand": "TestBrand"}
  }' -k
```

### PowerShell
```powershell
$data = @{
    name = "Test Item"
    value = 150.75
    category = "Electronics"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:5001/api/testdata" `
  -Method Post -Body $data -ContentType "application/json" `
  -SkipCertificateCheck
```

## ??? Troubleshooting

| Issue | Solution |
|-------|----------|
| Docker not running | Start Docker Desktop |
| Port in use | `docker compose down` or change port |
| Connection refused | Check if MongoDB/RabbitMQ are running |
| Build fails | Run `dotnet restore` |

## ?? Project Structure

```
test-service/
??? TestService.Api/           # Web API
?   ??? Configuration/         # Settings
?   ??? Controllers/           # API endpoints
?   ??? Models/               # Data models
?   ??? Services/             # Business logic
?
??? TestService.Tests/         # Tests
?   ??? TestDataApiTests.cs   # Integration tests
?
??? docker-compose.yml         # Infrastructure
??? README.md                  # Full docs
??? VERIFICATION_GUIDE.md      # Testing guide
```

## ?? Configuration

Edit `appsettings.json` to change:
- MongoDB connection string
- RabbitMQ settings
- Logging levels

## ?? Documentation

- **README.md** - Complete documentation
- **VERIFICATION_GUIDE.md** - Step-by-step testing
- **VERIFICATION_RESULTS.md** - Verification summary

## ? Checklist

- [ ] Docker Desktop running
- [ ] Infrastructure started (`docker compose up -d`)
- [ ] API running (`dotnet run`)
- [ ] Swagger UI accessible
- [ ] Tests passing (`dotnet test`)

---

**Need Help?** Check VERIFICATION_GUIDE.md for detailed instructions.
