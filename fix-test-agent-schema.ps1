# Script to fix test-agent schema with proper uniqueness settings

$baseUrl = "http://localhost:5000"

Write-Host "`n=== Fixing test-agent Schema ===" -ForegroundColor Cyan

# Step 1: Login
Write-Host "`n1. Logging in..." -ForegroundColor Yellow
$loginResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"admin","password":"Admin@123"}'
$token = $loginResponse.token
Write-Host "   ✓ Logged in successfully" -ForegroundColor Green

# Step 2: Delete existing schema
Write-Host "`n2. Deleting existing test-agent schema..." -ForegroundColor Yellow
try {
    Invoke-RestMethod -Uri "$baseUrl/api/schemas/test-agent" -Method DELETE -Headers @{Authorization="Bearer $token"} | Out-Null
    Write-Host "   ✓ Schema deleted" -ForegroundColor Green
} catch {
    Write-Host "   ⚠ Schema may not exist or already deleted" -ForegroundColor Yellow
}

# Step 3: Create new schema with proper uniqueness
Write-Host "`n3. Creating new schema with UserId and Username as unique..." -ForegroundColor Yellow

$newSchema = @{
    entityName = "test-agent"
    fields = @(
        @{
            name = "UserId"
            type = "number"
            required = $true
            isUnique = $true
            description = "Unique user identifier"
        },
        @{
            name = "UserTypeId"
            type = "number"
            required = $true
            isUnique = $false
        },
        @{
            name = "UserStatusId"
            type = "number"
            required = $true
            isUnique = $false
        },
        @{
            name = "Username"
            type = "string"
            required = $true
            isUnique = $true
            description = "Unique username"
        },
        @{
            name = "LabelId"
            type = "number"
            required = $true
            isUnique = $false
        },
        @{
            name = "BrandId"
            type = "number"
            required = $true
            isUnique = $false
        },
        @{
            name = "Password"
            type = "string"
            required = $true
            isUnique = $false
        }
    )
    filterableFields = @("BrandId", "Username")
    excludeOnFetch = $true
}

$schemaJson = $newSchema | ConvertTo-Json -Depth 10

$createdSchema = Invoke-RestMethod -Uri "$baseUrl/api/schemas" -Method POST -Headers @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
} -Body $schemaJson

Write-Host "   ✓ Schema created successfully" -ForegroundColor Green

# Step 4: Verify schema
Write-Host "`n4. Verifying schema..." -ForegroundColor Yellow
$verifySchema = Invoke-RestMethod -Uri "$baseUrl/api/schemas/test-agent" -Headers @{Authorization="Bearer $token"}

Write-Host "`n   Schema Details:" -ForegroundColor Cyan
Write-Host "   Entity Name: $($verifySchema.entityName)" -ForegroundColor White
Write-Host "   Fields with isUnique=true:" -ForegroundColor White

$uniqueFields = $verifySchema.fields | Where-Object { $_.isUnique -eq $true }
if ($uniqueFields) {
    foreach ($field in $uniqueFields) {
        Write-Host "     ✓ $($field.name) ($($field.type))" -ForegroundColor Green
    }
} else {
    Write-Host "     ✗ No unique fields found!" -ForegroundColor Red
}

# Step 5: Test uniqueness validation
Write-Host "`n5. Testing uniqueness validation..." -ForegroundColor Yellow

# Create first entity
$entity1 = @{
    fields = @{
        UserId = 12283347
        UserTypeId = 3
        UserStatusId = 1
        Username = "AutomationMgr_213"
        LabelId = 1
        BrandId = 14
        Password = "welcome123"
    }
    environment = "qa"
} | ConvertTo-Json

Write-Host "   Creating first entity with UserId=12283347, Username=AutomationMgr_213..." -ForegroundColor White
try {
    $result1 = Invoke-RestMethod -Uri "$baseUrl/api/entities/test-agent" -Method POST -Headers @{
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    } -Body $entity1
    Write-Host "   ✓ First entity created: $($result1.id)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Failed to create first entity: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Try to create duplicate (should fail)
Write-Host "`n   Attempting to create duplicate with same UserId..." -ForegroundColor White
$entity2 = @{
    fields = @{
        UserId = 12283347  # Same UserId - should fail
        UserTypeId = 5
        UserStatusId = 2
        Username = "DifferentUsername"
        LabelId = 2
        BrandId = 15
        Password = "password456"
    }
    environment = "qa"
} | ConvertTo-Json

try {
    $result2 = Invoke-RestMethod -Uri "$baseUrl/api/entities/test-agent" -Method POST -Headers @{
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    } -Body $entity2 -ErrorAction Stop
    Write-Host "   ✗ FAILED: Duplicate UserId was allowed (validation not working!)" -ForegroundColor Red
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 400) {
        Write-Host "   ✓ SUCCESS: Duplicate UserId rejected (validation working!)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ Unexpected error: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Try to create duplicate username (should fail)
Write-Host "`n   Attempting to create duplicate with same Username..." -ForegroundColor White
$entity3 = @{
    fields = @{
        UserId = 99999999  # Different UserId
        UserTypeId = 5
        UserStatusId = 2
        Username = "AutomationMgr_213"  # Same Username - should fail
        LabelId = 2
        BrandId = 15
        Password = "password789"
    }
    environment = "qa"
} | ConvertTo-Json

try {
    $result3 = Invoke-RestMethod -Uri "$baseUrl/api/entities/test-agent" -Method POST -Headers @{
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    } -Body $entity3 -ErrorAction Stop
    Write-Host "   ✗ FAILED: Duplicate Username was allowed (validation not working!)" -ForegroundColor Red
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 400) {
        Write-Host "   ✓ SUCCESS: Duplicate Username rejected (validation working!)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ Unexpected error: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
Write-Host "`nNote: Existing entities with duplicate values were created before the schema was fixed." -ForegroundColor Yellow
Write-Host "You can clean them up with: DELETE /api/entities/test-agent/{id}" -ForegroundColor Yellow
