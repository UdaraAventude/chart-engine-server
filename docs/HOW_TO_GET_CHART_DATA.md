# How to Get Chart Data and Responses

This guide explains how to retrieve chart visualization data from the Chart Engine API.

---

## Overview: Chart Data Query Flow

```
1. Upload Dataset
   ↓
2. Get Schema (what fields are available?)
   ↓
3. Get Tree (aggregated data structure)
   ↓
4. Drill Down (navigate into specific branches)
   ↓
5. Get Rows (detailed data for selected subset)
   ↓
6. Render Chart (client-side with dimensions + metrics)
```

---

## 1. Schema Endpoint — Discover Available Fields

### Request

```http
GET /api/v1/datasets/{datasetId}/schema
```

**Example:**
```http
GET /api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/schema
```

### Response (DatasetSchemaDto)

```json
{
  "datasetId": "550e8400-e29b-41d4-a716-446655440000",
  "dimensions": [
    "Country",
    "Region",
    "Product",
    "Month",
    "SalesChannel"
  ],
  "metrics": [
    "Revenue",
    "Quantity",
    "Profit",
    "AverageOrderValue"
  ]
}
```

**What this tells you:**
- **Dimensions**: fields suitable for grouping (categorical data like Country, Product, Month)
- **Metrics**: fields suitable for aggregation (numeric data like Revenue, Quantity, Profit)
- Use dimensions for **axes** and drill-down paths
- Use metrics for **values** to display

### Code Example

**C# / .NET:**
```csharp
using var client = new HttpClient();
var response = await client.GetAsync("https://localhost:5001/api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/schema");
var json = await response.Content.ReadAsStringAsync();
var schema = JsonSerializer.Deserialize<DatasetSchemaDto>(json);

Console.WriteLine($"Dimensions: {string.Join(", ", schema.Dimensions)}");
Console.WriteLine($"Metrics: {string.Join(", ", schema.Metrics)}");
```

**JavaScript / TypeScript:**
```typescript
const response = await fetch("https://localhost:5001/api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/schema");
const schema = await response.json();

console.log("Dimensions:", schema.dimensions);
console.log("Metrics:", schema.metrics);
```

---

## 2. Tree Endpoint — Get Full Aggregation Tree

### Request

```http
GET /api/v1/datasets/{datasetId}/tree
```

**Example:**
```http
GET /api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/tree
```

### Response (AggregationTree JSON)

The tree is a **multi-dimensional hierarchical structure** used for drill-down visualization.

**Example Response (simplified):**
```json
{
  "name": "root",
  "value": 1000000,
  "rowCount": 50000,
  "children": [
    {
      "name": "USA",
      "dimensionName": "Country",
      "value": 400000,
      "rowCount": 20000,
      "children": [
        {
          "name": "Electronics",
          "dimensionName": "Product",
          "value": 250000,
          "rowCount": 12000,
          "children": [
            {
              "name": "January",
              "dimensionName": "Month",
              "value": 75000,
              "rowCount": 3000
            },
            {
              "name": "February",
              "dimensionName": "Month",
              "value": 85000,
              "rowCount": 3500
            }
          ]
        },
        {
          "name": "Clothing",
          "dimensionName": "Product",
          "value": 150000,
          "rowCount": 8000
        }
      ]
    },
    {
      "name": "Europe",
      "dimensionName": "Country",
      "value": 350000,
      "rowCount": 18000,
      "children": [...]
    },
    {
      "name": "Asia",
      "dimensionName": "Country",
      "value": 250000,
      "rowCount": 12000,
      "children": [...]
    }
  ]
}
```

**Structure Explanation:**
- `name`: Dimension value (e.g., "USA", "Electronics")
- `dimensionName`: Which dimension this level represents (e.g., "Country", "Product")
- `value`: Aggregated metric value at this level
- `rowCount`: Number of detail rows in this group
- `children`: Sub-groups (drill-down levels)

### Use Cases

**Bar Chart (by Country):**
```json
[
  { "country": "USA", "revenue": 400000 },
  { "country": "Europe", "revenue": 350000 },
  { "country": "Asia", "revenue": 250000 }
]
```

**Pie Chart (by Product at USA level):**
```json
[
  { "product": "Electronics", "revenue": 250000 },
  { "product": "Clothing", "revenue": 150000 }
]
```

**Stacked Bar (Country × Product):**
```json
[
  { "country": "USA", "product": "Electronics", "revenue": 250000 },
  { "country": "USA", "product": "Clothing", "revenue": 150000 },
  { "country": "Europe", "product": "Electronics", "revenue": 200000 },
  { "country": "Europe", "product": "Clothing", "revenue": 150000 }
]
```

### Code Example

**JavaScript / D3.js visualization:**
```typescript
const response = await fetch("https://localhost:5001/api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/tree");
const tree = await response.json();

// Extract root level (by Country)
const countryData = tree.children.map(node => ({
  name: node.name,
  value: node.value,
  children: node.children
}));

// Create bar chart
const barChart = {
  categories: countryData.map(d => d.name),
  series: [{
    name: 'Revenue',
    data: countryData.map(d => d.value)
  }]
};

// Render with chart library (e.g., Chart.js, ECharts)
renderBarChart(barChart);
```

---

## 3. Drill-Down Endpoint — Navigate the Tree

### Request

```http
GET /api/v1/datasets/{datasetId}/drill?path=<dimension1>,<dimension2>,...
```

**Examples:**
```http
# Get all countries
GET /api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/drill

# Drill into USA
GET /api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/drill?path=USA

# Drill into USA → Electronics
GET /api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/drill?path=USA,Electronics

# Drill into USA → Electronics → January
GET /api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/drill?path=USA,Electronics,January
```

### Response (Branch of tree at specified drill path)

**Response for `drill?path=USA`:**
```json
{
  "name": "USA",
  "dimensionName": "Country",
  "value": 400000,
  "rowCount": 20000,
  "children": [
    {
      "name": "Electronics",
      "dimensionName": "Product",
      "value": 250000,
      "rowCount": 12000
    },
    {
      "name": "Clothing",
      "dimensionName": "Product",
      "value": 150000,
      "rowCount": 8000
    }
  ]
}
```

**Response for `drill?path=USA,Electronics`:**
```json
{
  "name": "Electronics",
  "dimensionName": "Product",
  "value": 250000,
  "rowCount": 12000,
  "children": [
    {
      "name": "January",
      "dimensionName": "Month",
      "value": 75000,
      "rowCount": 3000
    },
    {
      "name": "February",
      "dimensionName": "Month",
      "value": 85000,
      "rowCount": 3500
    }
  ]
}
```

### Use Case: Interactive Drill-Down Chart

**Step 1: Initial view (all countries)**
```javascript
const root = await fetch("/api/v1/datasets/{id}/drill").then(r => r.json());
// Shows: USA, Europe, Asia

// User clicks on "USA"
// Step 2: Drill into USA
const usaBranch = await fetch("/api/v1/datasets/{id}/drill?path=USA").then(r => r.json());
// Shows: Electronics, Clothing

// User clicks on "Electronics"
// Step 3: Drill into USA → Electronics
const productBranch = await fetch("/api/v1/datasets/{id}/drill?path=USA,Electronics").then(r => r.json());
// Shows: January, February, etc.
```

---

## 4. Rows Endpoint — Get Detailed Data

### Request

```http
GET /api/v1/datasets/{datasetId}/rows?page=1&pageSize=10&drillPath=<path>
```

**Examples:**
```http
# Get first 10 rows
GET /api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/rows?page=1&pageSize=10

# Get rows for USA only
GET /api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/rows?page=1&pageSize=10&drillPath=USA

# Get rows for USA → Electronics only
GET /api/v1/datasets/550e8400-e29b-41d4-a716-446655440000/rows?page=1&pageSize=100&drillPath=USA,Electronics
```

### Response (PagedRowsDto)

```json
{
  "page": 1,
  "pageSize": 10,
  "totalCount": 20000,
  "totalPages": 2000,
  "rows": [
    {
      "Country": "USA",
      "Product": "Electronics",
      "Month": "January",
      "Revenue": 12500,
      "Quantity": 50,
      "Profit": 3750,
      "SalesChannel": "Online"
    },
    {
      "Country": "USA",
      "Product": "Electronics",
      "Month": "January",
      "Revenue": 8900,
      "Quantity": 35,
      "Profit": 2670,
      "SalesChannel": "Retail"
    }
    // ... 8 more rows
  ]
}
```

### Use Case: Detailed Table View

```typescript
// Get detailed rows for selected drill path
const response = await fetch(
  `https://localhost:5001/api/v1/datasets/${datasetId}/rows?page=1&pageSize=100&drillPath=USA,Electronics`
);
const { rows, totalCount } = await response.json();

// Render as table
const table = {
  columns: ['Country', 'Product', 'Month', 'Revenue', 'Quantity', 'Profit', 'SalesChannel'],
  data: rows,
  summary: {
    totalRecords: totalCount,
    sumRevenue: rows.reduce((s, r) => s + r.Revenue, 0),
    sumQuantity: rows.reduce((s, r) => s + r.Quantity, 0)
  }
};
```

---

## 5. Complete Example: Building a Dashboard

### Scenario
Build an interactive dashboard with:
1. **KPI Cards** showing total revenue
2. **Bar Chart** showing revenue by country
3. **Drill-down capability** to products and months
4. **Detailed table** for selected drill path

### Code

```typescript
// 1. Get Schema (what fields are available)
const schema = await fetch(`/api/v1/datasets/${datasetId}/schema`).then(r => r.json());

// 2. Get Tree (full aggregation)
const tree = await fetch(`/api/v1/datasets/${datasetId}/tree`).then(r => r.json());

// Extract root-level data (countries)
const countryData = tree.children.map(node => ({
  name: node.name,
  revenue: node.value,
  rowCount: node.rowCount
}));

// 3. Render KPI Cards
const totalRevenue = tree.value;
renderKpiCard('Total Revenue', formatCurrency(totalRevenue));
renderKpiCard('Total Rows', formatNumber(tree.rowCount));

// 4. Render Bar Chart (Countries)
renderBarChart({
  title: 'Revenue by Country',
  categories: countryData.map(d => d.name),
  data: countryData.map(d => d.revenue),
  onClick: async (countryName) => {
    // Handle drill-down on click
    const drilled = await fetch(`/api/v1/datasets/${datasetId}/drill?path=${countryName}`).then(r => r.json());
    
    // Update to show Products for selected country
    const productData = drilled.children.map(node => ({
      name: node.name,
      revenue: node.value
    }));
    
    renderBarChart({
      title: `Revenue by Product (${countryName})`,
      categories: productData.map(d => d.name),
      data: productData.map(d => d.revenue)
    });
    
    // 5. Render detailed table for this selection
    const rows = await fetch(
      `/api/v1/datasets/${datasetId}/rows?page=1&pageSize=100&drillPath=${countryName}`
    ).then(r => r.json());
    
    renderTable({
      columns: Object.keys(rows.rows[0] || {}),
      data: rows.rows,
      pagination: {
        page: rows.page,
        pageSize: rows.pageSize,
        totalPages: rows.totalPages
      }
    });
  }
});
```

---

## 6. Chart Types You Can Build

Based on the API responses, here are chart types you can create:

| Chart Type | Required Data | Endpoint | Example |
|---|---|---|---|
| **Bar Chart** | Single dimension × single metric | `/tree` or `/drill` | Revenue by Country |
| **Horizontal Bar** | Single dimension × single metric | `/tree` or `/drill` | Revenue by Product |
| **Pie Chart** | Single dimension × single metric | `/tree` or `/drill` | Market share by Region |
| **Line Chart** | Time dimension × metric | `/tree` or `/drill` | Revenue over months |
| **Area Chart** | Time dimension × multiple metrics | `/tree` or `/drill` | Revenue + Profit over time |
| **Stacked Bar** | Two dimensions × single metric | `/tree` | Revenue by Country + Product |
| **Heatmap** | Two dimensions × single metric | `/rows` | Product × Month grid |
| **Scatter Plot** | Multiple metrics | `/rows` | Revenue vs. Profit by transaction |
| **Table** | All dimensions + metrics | `/rows` | Detailed transaction view |
| **KPI Card** | Single aggregation | `/tree` | Total revenue, total rows |
| **Drill-down Tree** | Hierarchy | `/drill` | Interactive tree navigation |

---

## 7. Status Check Before Querying

Always check dataset status first to ensure processing is complete:

### Request

```http
GET /api/v1/datasets/{datasetId}/status
```

### Response

```json
{
  "datasetId": "550e8400-e29b-41d4-a716-446655440000",
  "fileName": "sales_data.csv",
  "status": "Ready",
  "totalRows": 50000,
  "createdAt": "2024-01-15T10:30:00Z",
  "processedAt": "2024-01-15T10:45:30Z",
  "errorMessage": null
}
```

**Status values:**
- `Pending`: Upload complete, awaiting processing
- `Processing`: Schema detection and tree building in progress
- `Ready`: Processing complete, data available for queries
- `Failed`: Error during processing, see `errorMessage`

### Check Status Before Querying

```typescript
const status = await fetch(`/api/v1/datasets/${datasetId}/status`).then(r => r.json());

if (status.status === 'Ready') {
  // Proceed with querying
  const tree = await fetch(`/api/v1/datasets/${datasetId}/tree`).then(r => r.json());
} else if (status.status === 'Processing') {
  console.log('Data still processing, please wait...');
} else if (status.status === 'Failed') {
  console.error('Processing failed:', status.errorMessage);
}
```

---

## 8. Real-Time Progress with SignalR

Monitor processing progress in real-time while dataset is being processed:

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/dataset")
    .withAutomaticReconnect()
    .build();

connection.on("ProgressUpdated", (data) => {
    console.log(`Progress: ${data.percent}% - ${data.message}`);
    updateProgressBar(data.percent);
});

connection.on("ProcessingComplete", (data) => {
    console.log("Processing complete! Data is ready for queries.");
    showDataQueryButtons();
});

await connection.start();

// Join dataset-specific group
await connection.invoke("JoinGroup", `dataset-${datasetId}`);
```

---

## 9. Error Handling

### Common Error Responses

**Dataset Not Found (404):**
```json
{
  "error": "Dataset 550e8400-e29b-41d4-a716-446655440000 not found."
}
```

**Dataset Not Ready Yet (400):**
```json
{
  "error": "Dataset 550e8400-e29b-41d4-a716-446655440000 is not ready yet."
}
```

**Invalid Drill Path (404):**
```json
{
  "error": "Path not found or dataset not ready."
}
```

**Processing Error (400):**
```json
{
  "error": "Tree not found or empty."
}
```

### Error Handling Code

```typescript
async function queryChartData(datasetId, path = null) {
  try {
    // Check status first
    const statusResponse = await fetch(`/api/v1/datasets/${datasetId}/status`);
    if (!statusResponse.ok) {
      throw new Error('Dataset not found');
    }
    
    const status = await statusResponse.json();
    if (status.status !== 'Ready') {
      throw new Error(`Dataset not ready: ${status.status}`);
    }
    
    // Get tree or drill data
    const endpoint = path 
      ? `/api/v1/datasets/${datasetId}/drill?path=${path}`
      : `/api/v1/datasets/${datasetId}/tree`;
    
    const treeResponse = await fetch(endpoint);
    if (!treeResponse.ok) {
      const error = await treeResponse.json();
      throw new Error(error.error);
    }
    
    return await treeResponse.json();
    
  } catch (error) {
    console.error('Error querying chart data:', error);
    showErrorMessage(error.message);
    return null;
  }
}
```

---

## 10. Request Flow Diagram

```
Client App
  ↓
1. POST /api/v1/datasets/upload  [Upload file]
  ↓ (Async processing starts)
2. GET /api/v1/datasets/{id}/status  [Poll or use SignalR]
  ↓ (When status = "Ready")
3. GET /api/v1/datasets/{id}/schema  [Get available fields]
  ↓
4. GET /api/v1/datasets/{id}/tree  [Get aggregation structure]
  ↓
5. Build visualization
  ├─ Bar Chart (countries) → /drill?path=Country
  ├─ Drill → /drill?path=Country,Product
  ├─ Detailed table → /rows?drillPath=Country,Product
  └─ KPI cards (from tree root value)
```

---

## Summary

**To get chart data:**
1. Check dataset status (`/status`) — ensure it's `Ready`
2. Get schema (`/schema`) — understand available dimensions and metrics
3. Get tree (`/tree`) — get aggregated hierarchical data for visualization
4. Use drill (`/drill`) — navigate to deeper levels for drill-down charts
5. Use rows (`/rows`) — get detailed transaction-level data for tables

**Chart types:**
- Build any chart type based on aggregated tree data (bar, pie, line, area, etc.)
- Support interactive drill-down by calling `/drill` with deeper paths
- Show detailed data tables using `/rows` endpoint

**Real-time updates:**
- Use SignalR Hub to monitor processing progress while dataset is being ingested
- React to `ProgressUpdated` and `ProcessingComplete` events

