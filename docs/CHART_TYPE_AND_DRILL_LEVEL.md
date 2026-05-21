# Determining Chart Type and Drill Level from API Responses

This guide explains how to extract chart type and drill level information from the existing API endpoints.

---

## Key Finding

**The Chart Engine API does NOT explicitly return chart type or drill level.** Instead:
- **Chart Type** is determined **client-side** based on data structure
- **Drill Level** can be inferred from the response structure and path parameters

---

## 1. Understanding Drill Level

### What is Drill Level?

**Drill level** = how deep you are in the dimensional hierarchy.

```
Level 0 (Root): All data aggregated
  ↓
Level 1: First dimension (e.g., Country)
  ├─ USA
  ├─ Europe
  └─ Asia
    ↓
Level 2: Second dimension (e.g., Product)
  ├─ Electronics
  ├─ Clothing
  └─ Furniture
    ↓
Level 3: Third dimension (e.g., Month)
  ├─ January
  ├─ February
  └─ March
```

### How to Determine Drill Level

**From the `/drill` endpoint response:**

```typescript
interface TreeNode {
  name: string;              // "USA"
  dimensionName: string;     // "Country" ← tells you which dimension this is
  value: number;             // 400000
  rowCount: number;          // 20000
  children?: TreeNode[];     // sub-nodes at next level
}
```

**Method 1: Count path parameters**
```typescript
const drillPath = "USA,Electronics,January";
const drillLevel = drillPath.split(',').length;  // Level 3
```

**Method 2: Read dimensionName from response**
```typescript
const response = await fetch(`/api/v1/datasets/${id}/drill?path=USA,Electronics`);
const node = await response.json();

console.log(`Current drill level dimensionName: ${node.dimensionName}`);  // "Product"
```

**Method 3: Check depth from tree structure**
```typescript
function getTreeDepth(node: TreeNode, depth = 0): number {
  if (!node.children || node.children.length === 0) {
    return depth;
  }
  return getTreeDepth(node.children[0], depth + 1);
}

const maxDepth = getTreeDepth(rootNode);  // Total levels in tree
const currentDepth = drillPath.split(',').length;
```

### Code Example: Track Drill Level

```typescript
class ChartNavigator {
  private drillPath: string[] = [];
  private dimensions: string[] = ['Country', 'Product', 'Month', 'SalesChannel'];

  getCurrentDrillLevel(): number {
    return this.drillPath.length;
  }

  getCurrentDimension(): string {
    if (this.drillPath.length === 0) {
      return this.dimensions[0];  // First dimension is root level
    }
    return this.dimensions[this.drillPath.length];
  }

  canDrillDeeper(): boolean {
    return this.drillPath.length < this.dimensions.length - 1;
  }

  async drillInto(dimensionValue: string): Promise<TreeNode> {
    this.drillPath.push(dimensionValue);
    const pathStr = this.drillPath.join(',');
    
    const response = await fetch(
      `/api/v1/datasets/${this.datasetId}/drill?path=${pathStr}`
    );
    
    return await response.json();
  }

  drillUp(): void {
    this.drillPath.pop();
  }

  goToRoot(): void {
    this.drillPath = [];
  }
}
```

---

## 2. Inferring Chart Type from Data Structure

**Chart type is NOT returned by the API.** The client must decide based on:
- Number of dimensions
- Number of metrics  
- Cardinality (uniqueness) of dimensions
- Tree depth

### Decision Tree for Chart Type

```
Data has:
├─ 1 dimension, 1 metric
│  ├─ Cardinality < 10         → Bar Chart / Pie Chart
│  ├─ Cardinality 10-100       → Bar Chart
│  └─ Cardinality > 100        → Table
│
├─ 1 dimension (time), 1 metric → Line Chart
│
├─ 2 dimensions, 1 metric
│  ├─ Both categorical         → Stacked Bar Chart
│  ├─ Time × Category          → Line Chart (multi-line)
│  └─ Two categories           → Heatmap
│
├─ 2+ dimensions, 2+ metrics   → Drill-down Tree / Table
│
└─ Many rows, granular detail  → Table with pagination
```

### Code Example: Auto-Detect Chart Type

```typescript
interface ChartRecommendation {
  chartType: 'bar' | 'pie' | 'line' | 'stacked' | 'heatmap' | 'table' | 'scatter';
  reason: string;
  config?: any;
}

function recommendChartType(
  data: TreeNode,
  drillLevel: number,
  schema: DatasetSchemaDto
): ChartRecommendation {
  const childCount = data.children?.length || 0;
  const nextDimensionName = schema.dimensions[drillLevel];
  const isTimeDimension = ['Month', 'Year', 'Date', 'Quarter'].some(t => 
    nextDimensionName.toLowerCase().includes(t.toLowerCase())
  );

  // Rule 1: Leaf node → show as table
  if (!data.children || data.children.length === 0) {
    return {
      chartType: 'table',
      reason: 'No children - leaf level'
    };
  }

  // Rule 2: Very few categories → pie chart
  if (childCount <= 5 && drillLevel === 0) {
    return {
      chartType: 'pie',
      reason: `Few categories (${childCount}) at root level`
    };
  }

  // Rule 3: Time series → line chart
  if (isTimeDimension) {
    return {
      chartType: 'line',
      reason: `Time dimension detected: ${nextDimensionName}`
    };
  }

  // Rule 4: Many categories → bar chart
  if (childCount > 5 && childCount <= 50) {
    return {
      chartType: 'bar',
      reason: `Medium cardinality (${childCount} categories)`
    };
  }

  // Rule 5: Very many categories → table
  if (childCount > 50) {
    return {
      chartType: 'table',
      reason: `High cardinality (${childCount} categories)`
    };
  }

  // Default
  return {
    chartType: 'bar',
    reason: 'Default recommendation'
  };
}
```

### Usage Example

```typescript
// Get the drill data
const response = await fetch(
  `/api/v1/datasets/${datasetId}/drill?path=USA,Electronics`
);
const treeNode = await response.json();

// Get schema
const schemaResponse = await fetch(`/api/v1/datasets/${datasetId}/schema`);
const schema = await schemaResponse.json();

// Recommend chart type
const recommendation = recommendChartType(treeNode, 2, schema);

console.log(`Recommended: ${recommendation.chartType}`);
console.log(`Reason: ${recommendation.reason}`);

// Render appropriate chart
renderChart(treeNode, recommendation.chartType);
```

---

## 3. Understanding Data Structure at Each Level

### Level 0: Root (All Data)

```http
GET /api/v1/datasets/{id}/tree
```

```json
{
  "name": "root",
  "value": 1000000,
  "rowCount": 50000,
  "children": [
    { "name": "USA", "dimensionName": "Country", "value": 400000 },
    { "name": "Europe", "dimensionName": "Country", "value": 350000 },
    { "name": "Asia", "dimensionName": "Country", "value": 250000 }
  ]
}
```

**→ Use**: KPI card, Bar chart (countries)

### Level 1: First Drill (Country selected)

```http
GET /api/v1/datasets/{id}/drill?path=USA
```

```json
{
  "name": "USA",
  "dimensionName": "Country",
  "value": 400000,
  "children": [
    { "name": "Electronics", "dimensionName": "Product", "value": 250000 },
    { "name": "Clothing", "dimensionName": "Product", "value": 150000 }
  ]
}
```

**→ Use**: Bar chart (products), Pie chart

### Level 2: Second Drill (Country → Product)

```http
GET /api/v1/datasets/{id}/drill?path=USA,Electronics
```

```json
{
  "name": "Electronics",
  "dimensionName": "Product",
  "value": 250000,
  "children": [
    { "name": "January", "dimensionName": "Month", "value": 75000 },
    { "name": "February", "dimensionName": "Month", "value": 85000 },
    { "name": "March", "dimensionName": "Month", "value": 90000 }
  ]
}
```

**→ Use**: Line chart (months over time), Bar chart

### Level 3+: Deep Drill (Country → Product → Month)

```http
GET /api/v1/datasets/{id}/drill?path=USA,Electronics,January
```

```json
{
  "name": "January",
  "dimensionName": "Month",
  "value": 75000,
  "children": null  // or []
}
```

**→ Use**: Show details via `/rows` endpoint, Table view

---

## 4. Complete Example: Dynamic Chart Navigation

```typescript
interface NavigationState {
  datasetId: string;
  drillPath: string[];
  schema: DatasetSchemaDto;
  currentNode: TreeNode;
  chartType: 'bar' | 'pie' | 'line' | 'table';
}

class ChartNavigationController {
  async loadInitialState(datasetId: string): Promise<NavigationState> {
    // 1. Get schema
    const schemaResp = await fetch(`/api/v1/datasets/${datasetId}/schema`);
    const schema = await schemaResp.json();

    // 2. Get root tree
    const treeResp = await fetch(`/api/v1/datasets/${datasetId}/tree`);
    const rootNode = await treeResp.json();

    // 3. Recommend chart type
    const chartType = this.recommendChartType(rootNode, 0, schema).chartType;

    return {
      datasetId,
      drillPath: [],
      schema,
      currentNode: rootNode,
      chartType
    };
  }

  async drillInto(
    state: NavigationState,
    dimensionValue: string
  ): Promise<NavigationState> {
    const newPath = [...state.drillPath, dimensionValue];
    const pathStr = newPath.join(',');

    const response = await fetch(
      `/api/v1/datasets/${state.datasetId}/drill?path=${pathStr}`
    );
    const newNode = await response.json();

    const currentLevel = newPath.length;
    const chartType = this.recommendChartType(newNode, currentLevel, state.schema).chartType;

    return {
      ...state,
      drillPath: newPath,
      currentNode: newNode,
      chartType
    };
  }

  async drillUp(state: NavigationState): Promise<NavigationState> {
    if (state.drillPath.length === 0) {
      return state;  // Already at root
    }

    const newPath = state.drillPath.slice(0, -1);
    const pathStr = newPath.length === 0 ? '' : newPath.join(',');

    const response = await fetch(
      `/api/v1/datasets/${state.datasetId}/drill${pathStr ? `?path=${pathStr}` : ''}`
    );
    const newNode = await response.json();

    const currentLevel = newPath.length;
    const chartType = this.recommendChartType(newNode, currentLevel, state.schema).chartType;

    return {
      ...state,
      drillPath: newPath,
      currentNode: newNode,
      chartType
    };
  }

  private recommendChartType(
    node: TreeNode,
    level: number,
    schema: DatasetSchemaDto
  ): { chartType: string; reason: string } {
    const childCount = node.children?.length || 0;

    if (childCount === 0) {
      return { chartType: 'table', reason: 'Leaf node' };
    }

    if (childCount <= 5) {
      return { chartType: 'pie', reason: 'Few items' };
    }

    const nextDim = schema.dimensions[level];
    if (['Month', 'Year', 'Date'].some(t => nextDim?.includes(t))) {
      return { chartType: 'line', reason: 'Time dimension' };
    }

    if (childCount > 30) {
      return { chartType: 'table', reason: 'Many items' };
    }

    return { chartType: 'bar', reason: 'Default' };
  }
}

// Usage
const controller = new ChartNavigationController();
let state = await controller.loadInitialState(datasetId);
// → renders bar/pie chart at root level

// User clicks on "USA"
state = await controller.drillInto(state, 'USA');
// → recommends and renders chart for next level

// User clicks "Back"
state = await controller.drillUp(state);
// → returns to parent level
```

---

## 5. Do We Need a New "Chart Type Recommendation" API?

### Current Situation (Existing Endpoints)

| Approach | Pros | Cons |
|---|---|---|
| **Client-side inference** (current) | ✅ No extra API call<br>✅ Smart logic encapsulated in UI<br>✅ Responsive recommendations | ❌ Client must implement logic<br>❌ Inconsistent across clients |
| **Dedicated recommendation API** | ✅ Centralized logic<br>✅ Consistent across all clients<br>✅ Easy to update/maintain | ❌ Extra API call overhead<br>❌ Added server complexity |

### Recommendation

**For NOW:** Use **client-side inference** with the existing endpoints.

**If needed LATER:** Create a dedicated endpoint like:

```http
POST /api/v1/datasets/{datasetId}/chart-recommendation
Content-Type: application/json

{
  "drillPath": ["USA", "Electronics"],
  "dimensions": ["Country", "Product", "Month"],
  "metrics": ["Revenue", "Profit"]
}
```

**Response:**
```json
{
  "chartType": "bar",
  "alternativeCharts": ["line", "scatter"],
  "reason": "Bar chart recommended for 3 product categories",
  "config": {
    "xAxis": "Product",
    "yAxis": "Revenue",
    "stacking": null,
    "sort": "desc"
  }
}
```

---

## 6. Practical Implementation: Extract Drill Metadata

Use this function to extract drill information from any API response:

```typescript
interface DrillMetadata {
  currentDrillLevel: number;
  currentDimension: string;
  canDrillDeeper: boolean;
  childCount: number;
  nextDimension?: string;
  isLeafNode: boolean;
}

function extractDrillMetadata(
  node: TreeNode,
  drillPath: string[],
  schema: DatasetSchemaDto
): DrillMetadata {
  const currentLevel = drillPath.length;
  const currentDimension = schema.dimensions[currentLevel - 1] || 'root';
  const nextDimension = schema.dimensions[currentLevel];
  const childCount = node.children?.length || 0;
  const isLeafNode = !node.children || node.children.length === 0;
  const canDrillDeeper = !isLeafNode && currentLevel < schema.dimensions.length;

  return {
    currentDrillLevel: currentLevel,
    currentDimension,
    nextDimension,
    childCount,
    canDrillDeeper,
    isLeafNode
  };
}

// Usage
const metadata = extractDrillMetadata(treeNode, drillPath, schema);

console.log(`Level: ${metadata.currentDrillLevel}`);
console.log(`Current: ${metadata.currentDimension}`);
console.log(`Next: ${metadata.nextDimension}`);
console.log(`Can drill deeper: ${metadata.canDrillDeeper}`);
console.log(`Is leaf: ${metadata.isLeafNode}`);
```

---

## 7. Summary: Answer to Your Question

### Can we determine Chart Type and Drill Level with Existing Endpoints?

| Question | Answer | How |
|---|---|---|
| **Get Drill Level?** | ✅ YES | Count path parameters or read `dimensionName` |
| **Get Chart Type?** | ⚠️ PARTIALLY | Client must infer from: childCount, cardinality, dimensions |
| **Get Recommended Chart Type?** | ❌ NO | Not exposed by API; implement client-side logic |

### Do We Need New API?

**No, not required.** Use existing endpoints with client-side chart type inference.

**Optional:** Create `/chart-recommendation` endpoint if you want centralized chart type logic server-side.

---

## 8. Code Template: Ready-to-Use

Save this and use in your frontend:

```typescript
// File: utils/chartRecommender.ts

export interface ChartConfig {
  type: string;
  dimensions: string[];
  metrics: string[];
  drillLevel: number;
}

export class ChartRecommender {
  static async recommend(
    datasetId: string,
    drillPath: string[] = []
  ): Promise<ChartConfig> {
    // 1. Get schema
    const schema = await this.getSchema(datasetId);

    // 2. Get tree/drill data
    const pathStr = drillPath.length > 0 ? drillPath.join(',') : '';
    const treeData = await this.getTreeOrDrill(datasetId, pathStr);

    // 3. Infer chart type
    const childCount = treeData.children?.length || 0;
    const drillLevel = drillPath.length;
    const nextDim = schema.dimensions[drillLevel];

    let chartType = 'bar';
    if (childCount === 0) {
      chartType = 'table';
    } else if (childCount <= 5) {
      chartType = 'pie';
    } else if (this.isTimeDimension(nextDim)) {
      chartType = 'line';
    }

    return {
      type: chartType,
      dimensions: schema.dimensions.slice(0, drillLevel + 1),
      metrics: schema.metrics,
      drillLevel
    };
  }

  private static async getSchema(datasetId: string) {
    const res = await fetch(`/api/v1/datasets/${datasetId}/schema`);
    return await res.json();
  }

  private static async getTreeOrDrill(datasetId: string, path: string) {
    const endpoint = path
      ? `/api/v1/datasets/${datasetId}/drill?path=${path}`
      : `/api/v1/datasets/${datasetId}/tree`;
    const res = await fetch(endpoint);
    return await res.json();
  }

  private static isTimeDimension(dim: string): boolean {
    const timeDims = ['month', 'year', 'date', 'quarter', 'week', 'day'];
    return timeDims.some(t => dim?.toLowerCase().includes(t));
  }
}

// Usage in React component
function ChartViewer({ datasetId }: { datasetId: string }) {
  const [config, setConfig] = useState<ChartConfig | null>(null);

  useEffect(() => {
    ChartRecommender.recommend(datasetId).then(setConfig);
  }, [datasetId]);

  return config ? <Chart type={config.type} /> : <Skeleton />;
}
```

