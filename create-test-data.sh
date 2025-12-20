#!/bin/bash

API_URL="http://localhost:5000"

echo "======================================"
echo "Creating Test Data"
echo "======================================"
echo ""

# Create a test-agent schema
echo "📝 Creating test-agent schema..."
curl -s -X POST "${API_URL}/api/schemas" \
  -H "Content-Type: application/json" \
  -d '{
    "entityName": "test-agent",
    "fields": [
      {"name": "username", "type": "string", "required": true, "isUnique": true},
      {"name": "password", "type": "string", "required": true},
      {"name": "firstName", "type": "string", "required": true},
      {"name": "lastName", "type": "string", "required": true},
      {"name": "email", "type": "string", "required": false},
      {"name": "brandId", "type": "string", "required": false},
      {"name": "labelId", "type": "string", "required": false},
      {"name": "agentType", "type": "string", "required": false},
      {"name": "department", "type": "string", "required": false},
      {"name": "status", "type": "string", "required": false},
      {"name": "phoneNumber", "type": "string", "required": false},
      {"name": "hireDate", "type": "string", "required": false}
    ],
    "filterableFields": ["username", "brandId", "labelId", "agentType", "department", "status"],
    "excludeOnFetch": true
  }' | jq -r '.entityName // "error"'

echo "✅ Schema created!"
echo ""

# Create test agents
echo "👥 Creating test agents..."

brands=("brand-alpha" "brand-beta" "brand-gamma")
labels=("label-vip" "label-standard" "label-premium")
types=("support" "sales" "technical" "manager")
departments=("customer-service" "sales" "it" "operations" "management")
statuses=("active" "inactive" "training" "on-leave")
environments=("dev" "qa" "staging")

for i in {1..25}; do
  brand="${brands[$((RANDOM % 3))]}"
  label="${labels[$((RANDOM % 3))]}"
  type="${types[$((RANDOM % 4))]}"
  dept="${departments[$((RANDOM % 5))]}"
  status="${statuses[$((RANDOM % 4))]}"
  env="${environments[$((RANDOM % 3))]}"
  
  username="agent_$(printf '%03d' $i)"
  firstName="Agent"
  lastName="User$(printf '%03d' $i)"
  email="${username}@testservice.com"
  phone="+1-555-$(printf '%04d' $((1000 + i)))"
  hireDate="2024-$(printf '%02d' $((RANDOM % 12 + 1)))-$(printf '%02d' $((RANDOM % 28 + 1)))"
  
  echo "  Creating $username ($type, $brand, $dept)..."
  
  curl -s -X POST "${API_URL}/api/entities/test-agent" \
    -H "Content-Type: application/json" \
    -d "{
      \"environment\": \"$env\",
      \"fields\": {
        \"username\": \"$username\",
        \"password\": \"Test@123\",
        \"firstName\": \"$firstName\",
        \"lastName\": \"$lastName\",
        \"email\": \"$email\",
        \"brandId\": \"$brand\",
        \"labelId\": \"$label\",
        \"agentType\": \"$type\",
        \"department\": \"$dept\",
        \"status\": \"$status\",
        \"phoneNumber\": \"$phone\",
        \"hireDate\": \"$hireDate\"
      }
    }" > /dev/null
done

echo ""
echo "✅ Created 25 test agents!"
echo ""

# Create a product schema
echo "📝 Creating product schema..."
curl -s -X POST "${API_URL}/api/schemas" \
  -H "Content-Type: application/json" \
  -d '{
    "entityName": "product",
    "fields": [
      {"name": "sku", "type": "string", "required": true, "isUnique": true},
      {"name": "name", "type": "string", "required": true},
      {"name": "description", "type": "string", "required": false},
      {"name": "price", "type": "number", "required": true},
      {"name": "category", "type": "string", "required": false},
      {"name": "inStock", "type": "boolean", "required": false},
      {"name": "supplier", "type": "string", "required": false}
    ],
    "filterableFields": ["category", "supplier", "inStock"],
    "excludeOnFetch": false
  }' | jq -r '.entityName // "error"'

echo "✅ Schema created!"
echo ""

# Create products
echo "📦 Creating test products..."

categories=("electronics" "furniture" "clothing" "books" "toys")
suppliers=("supplier-a" "supplier-b" "supplier-c")
inStockOptions=("true" "false")

for i in {1..15}; do
  category="${categories[$((RANDOM % 5))]}"
  supplier="${suppliers[$((RANDOM % 3))]}"
  inStock="${inStockOptions[$((RANDOM % 2))]}"
  
  sku="PRD-$(printf '%05d' $i)"
  name="Product $i"
  description="Description for product $i in $category category"
  price=$((RANDOM % 900 + 100))
  
  echo "  Creating $sku ($category, $supplier)..."
  
  curl -s -X POST "${API_URL}/api/entities/product" \
    -H "Content-Type: application/json" \
    -d "{
      \"fields\": {
        \"sku\": \"$sku\",
        \"name\": \"$name\",
        \"description\": \"$description\",
        \"price\": $price,
        \"category\": \"$category\",
        \"inStock\": $inStock,
        \"supplier\": \"$supplier\"
      }
    }" > /dev/null
done

echo ""
echo "✅ Created 15 test products!"
echo ""

echo "======================================"
echo "✅ Test Data Creation Complete!"
echo "======================================"
echo ""
echo "📊 Summary:"
echo "  • test-agent: 25 entities (with auto-consume)"
echo "  • product: 15 entities"
echo ""
echo "🌐 Access the web UI:"
echo "  http://localhost:3000"
echo ""
echo "💡 Try these features:"
echo "  1. Go to Entities → test-agent"
echo "  2. Use the Columns button to show/hide fields"
echo "  3. Filter by environment (dev, qa, staging)"
echo "  4. Search for specific agents"
echo "  5. Use 'Get Next' to consume agents"
echo ""
