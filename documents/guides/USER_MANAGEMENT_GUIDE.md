# User Management & Authentication Guide

## Overview

The Test Service now includes comprehensive user management and JWT-based authentication to secure API endpoints.

---

## Features

? **User Management** - Create, read, update, and delete users  
? **Role-Based Access Control** - Admin and Contributor roles  
? **JWT Authentication** - Secure token-based authentication  
? **Password Security** - PBKDF2 hashing with salt  
? **Password Requirements** - Strong password validation  
? **Default Admin User** - Automatically created on first run  

---

## User Roles

### Admin
- Full access to all endpoints
- Can manage users (create, update, delete)
- Can manage schemas and entities
- Can access all API features

### Contributor  
- Can access most endpoints
- Cannot manage users
- Can create and manage entities
- Read-only access to user information

---

## Default Admin User

**Created automatically on first application start:**

```
Username: admin
Password: Admin@123
Email: admin@testservice.local
Role: Admin
```

?? **IMPORTANT**: Change the default admin password immediately after first login!

---

## API Endpoints

### Authentication

#### Login
```bash
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "Admin@123"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "username": "admin",
  "email": "admin@testservice.local",
  "role": 1,
  "expiresAt": "2025-01-06T11:30:00Z"
}
```

#### Get Current User
```bash
GET /api/auth/me
Authorization: Bearer {token}
```

#### Change Password
```bash
POST /api/auth/change-password
Authorization: Bearer {token}
Content-Type: application/json

{
  "currentPassword": "Admin@123",
  "newPassword": "NewSecure@Pass456"
}
```

---

### User Management (Admin Only)

#### Get All Users
```bash
GET /api/users
Authorization: Bearer {admin-token}
```

#### Get User by ID
```bash
GET /api/users/{id}
Authorization: Bearer {admin-token}
```

#### Get User by Username
```bash
GET /api/users/username/{username}
Authorization: Bearer {admin-token}
```

#### Create User
```bash
POST /api/users
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "username": "john.doe",
  "email": "john@example.com",
  "password": "SecurePass@123",
  "firstName": "John",
  "lastName": "Doe",
  "role": 0
}
```

**Roles:**
- `0` = Contributor
- `1` = Admin

#### Update User
```bash
PUT /api/users/{id}
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "email": "newemail@example.com",
  "firstName": "Jane",
  "role": 1,
  "isActive": true
}
```

**Note:** All fields are optional. Only provided fields will be updated.

#### Delete User
```bash
DELETE /api/users/{id}
Authorization: Bearer {admin-token}
```

**Note:** Cannot delete the last admin user.

---

## Password Requirements

All passwords must meet the following criteria:

? Minimum **8 characters** long  
? At least **1 uppercase** letter (A-Z)  
? At least **1 lowercase** letter (a-z)  
? At least **1 digit** (0-9)  
? At least **1 special character** (!@#$%^&*()_+-)  

**Examples:**
- ? `SecurePass@123`
- ? `Admin@2025!`
- ? `password` (no uppercase, digit, or special char)
- ? `Pass123` (no special char, too short)

---

## Authentication Flow

### 1. Login
```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}' \
  -k
```

### 2. Save Token
```bash
# Response contains token
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-01-06T11:30:00Z"
}
```

### 3. Use Token in Requests
```bash
curl -X GET https://localhost:5001/api/users \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -k
```

---

## PowerShell Examples

### Login
```powershell
$loginBody = @{
    username = "admin"
    password = "Admin@123"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/login" `
  -Method Post -Body $loginBody -ContentType "application/json" `
  -SkipCertificateCheck

$token = $response.token
Write-Host "Token: $token"
```

### Create User
```powershell
$headers = @{
    Authorization = "Bearer $token"
}

$newUser = @{
    username = "john.doe"
    email = "john@example.com"
    password = "SecurePass@123"
    firstName = "John"
    lastName = "Doe"
    role = 0
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:5001/api/users" `
  -Method Post -Headers $headers -Body $newUser `
  -ContentType "application/json" -SkipCertificateCheck
```

### Get All Users
```powershell
$users = Invoke-RestMethod -Uri "https://localhost:5001/api/users" `
  -Headers $headers -SkipCertificateCheck

$users | Format-Table Username, Email, Role, IsActive
```

### Change Password
```powershell
$changePassword = @{
    currentPassword = "Admin@123"
    newPassword = "NewSecure@Pass456"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:5001/api/auth/change-password" `
  -Method Post -Headers $headers -Body $changePassword `
  -ContentType "application/json" -SkipCertificateCheck
```

---

## Security Features

### Password Hashing
- **Algorithm**: PBKDF2 with SHA256
- **Iterations**: 10,000
- **Salt Size**: 128 bits (16 bytes)
- **Key Size**: 256 bits (32 bytes)
- **Storage**: Base64-encoded (salt + hash)

### JWT Tokens
- **Algorithm**: HS256 (HMAC-SHA256)
- **Expiration**: 60 minutes (configurable)
- **Claims**: User ID, Username, Email, Role
- **Validation**: Issuer, Audience, Lifetime, Signature

### Authorization
- **Role-Based**: `[Authorize(Roles = "Admin")]`
- **Authenticated Only**: `[Authorize]`
- **Anonymous**: `[AllowAnonymous]`

---

## Configuration

### appsettings.json

```json
{
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLongForHS256Algorithm",
    "Issuer": "TestServiceApi",
    "Audience": "TestServiceClients",
    "ExpirationMinutes": 60
  }
}
```

**?? IMPORTANT:**
- Change `SecretKey` in production!
- Use environment variables for secrets
- Secret key must be at least 32 characters
- Never commit secrets to source control

---

## Protected Endpoints

### Require Authentication (Any Role)
- `GET /api/auth/me`
- `POST /api/auth/change-password`

### Require Admin Role
- `GET /api/users`
- `GET /api/users/{id}`
- `GET /api/users/username/{username}`
- `POST /api/users`
- `PUT /api/users/{id}`
- `DELETE /api/users/{id}`

### Anonymous (No Authentication Required)
- `POST /api/auth/login`
- All other existing endpoints (schemas, entities) remain unchanged

---

## Error Responses

### 401 Unauthorized
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

**Causes:**
- No Authorization header
- Invalid token
- Expired token

### 403 Forbidden
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403
}
```

**Causes:**
- User doesn't have required role
- Trying to access Admin endpoint as Contributor

### 400 Bad Request (Password validation)
```json
{
  "message": "Password must contain at least one uppercase letter"
}
```

### 409 Conflict (Duplicate user)
```json
{
  "message": "Username 'john.doe' already exists"
}
```

---

## Best Practices

### DO ?

1. **Change default admin password** immediately
2. **Use strong passwords** for all accounts
3. **Store tokens securely** (not in source code)
4. **Set token expiration** appropriately
5. **Rotate secret keys** periodically
6. **Use HTTPS** in production
7. **Validate user input** on client side
8. **Handle token expiration** gracefully
9. **Log authentication events** for audit
10. **Use environment variables** for secrets

### DON'T ?

1. **Don't hardcode** credentials
2. **Don't share** admin credentials
3. **Don't commit** secret keys to git
4. **Don't use HTTP** in production
5. **Don't store** passwords in plain text
6. **Don't ignore** password requirements
7. **Don't delete** the last admin user
8. **Don't reuse** passwords across systems
9. **Don't expose** internal user IDs
10. **Don't skip** token validation

---

## Troubleshooting

### Problem: "401 Unauthorized" on protected endpoint

**Solution:**
1. Check if token is included in Authorization header
2. Verify token hasn't expired
3. Ensure token format is: `Bearer {token}`
4. Re-login if token expired

### Problem: "403 Forbidden" on admin endpoint

**Solution:**
1. Check user role (must be Admin)
2. Verify token claims include correct role
3. Re-login to get fresh token

### Problem: "Password validation failed"

**Solution:**
1. Check password meets all requirements
2. Include uppercase, lowercase, digit, special char
3. Ensure minimum 8 characters

### Problem: "Cannot delete last admin"

**Solution:**
1. Create another admin user first
2. Then delete the old admin user

### Problem: Default admin not created

**Solution:**
1. Check MongoDB connection
2. Verify database permissions
3. Check application logs
4. Delete all users and restart application

---

## Testing with Swagger

1. Start the application
2. Navigate to `https://localhost:5001/swagger`
3. Click "POST /api/auth/login"
4. Click "Try it out"
5. Enter admin credentials
6. Click "Execute"
7. Copy the token from response
8. Click "Authorize" button at top
9. Enter: `Bearer {your-token}`
10. Click "Authorize"
11. Now you can test protected endpoints

---

## Migration Guide

### For Existing Installations

1. **Update application** to include authentication
2. **Start application** - default admin created automatically
3. **Login** with default credentials
4. **Change password** immediately
5. **Create additional users** as needed
6. **Update API clients** to include Authorization header

### For New Installations

1. **Configure JWT settings** in appsettings.json
2. **Start application**
3. **Login with default admin**
4. **Change password**
5. **Create users**
6. **Begin using API**

---

## Monitoring & Logging

### Authentication Events Logged

- User login (success/failure)
- Password change
- User creation
- User update
- User deletion
- Token validation failures

### Check Logs
```bash
# View authentication logs
dotnet run | Select-String "User"
```

---

## Production Checklist

- [ ] Change default admin password
- [ ] Change JWT secret key
- [ ] Use environment variables for secrets
- [ ] Enable HTTPS
- [ ] Configure appropriate token expiration
- [ ] Set up log monitoring
- [ ] Create backup admin account
- [ ] Document user accounts
- [ ] Test authentication flow
- [ ] Test role-based access

---

**Security is not optional. Always follow best practices!**
