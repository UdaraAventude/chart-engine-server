# 🗺️ Chart Engine Server: Technical Implementation Roadmap (Next Steps)

Now that the core ingestion pipeline, DB persistence, SignalR logging, and performance hot paths are complete and fully operational, we have a clear set of next steps to transition this engine into a fully-fledged, production-ready enterprise application. 

Here are the remaining backend features needed to achieve full integration parity with the frontend dashboard:

---

## 1️⃣ Dynamic Drill-Down API (`GET /api/v1/datasets/{id}/drill`)
*   **Problem:** For large datasets, the full tree JSON is huge (e.g., **462MB** for a 2M-row CSV with 8 dimensions and 12 metrics). Loading the entire tree in a single query is a severe performance bottleneck.
*   **Solution:** Build an endpoint that allows the frontend to query the tree on-demand. When the user expands a node in the UI chart, the frontend calls this endpoint to retrieve *only* the immediate children of that node.
*   **Query Parameters:**
    *   `path` (array of strings, e.g. `["USA", "Engineering"]`)
*   **Execution Flow:**
    1. Load the `TreeJson` from SQL Server.
    2. Parse the JSON using `JsonDocument` (highly memory-efficient DOM reader).
    3. Traverse the tree following the path dimensions (e.g., Root -> USA -> Engineering).
    4. Extract and return only the immediate child nodes at that level (usually < 2KB).

---

## 2️⃣ Raw Row Retrieval API (`GET /api/v1/datasets/{id}/rows`)
*   **Problem:** To achieve full parity with client-side sheets, the UI needs a tabular grid display under the charts showing the raw rows. However, loading 2M+ rows in an API response will crash the network.
*   **Solution:** Implement server-side pagination, sorting, and filtering directly against the saved CSV file on disk.
*   **Query Parameters:**
    *   `page` (default `1`)
    *   `pageSize` (default `100`)
    *   `filters` (JSON string containing column filter operators)
*   **Execution Flow:**
    1. Read the physical CSV path stored on disk (`dataset.StoragePath`).
    2. Open a `StreamReader` and use `CsvHelper` to stream rows.
    3. Apply pagination on the fly by skipping `(page - 1) * pageSize` rows and reading only `pageSize` rows.
    4. Return the paginated array to the client. This keeps memory footprint at virtually **0MB** on the server!

---

## 3️⃣ Database Blob Storage Compression (GZip/Brotli)
*   **Problem:** Storing 462MB of plain text JSON inside a `VARCHAR(MAX)` SQL database column creates heavy disk I/O load, slow database reads, and high hosting storage costs.
*   **Solution:** Compress the `TreeJson` before saving it to the database, and decompress it on read.
*   **Execution Flow:**
    1. Change the database schema: Migrate `TreeJson` from `NVARCHAR(MAX)` to `VARBINARY(MAX)`.
    2. During database save: Stream the serialized JSON through a `GZipStream` to produce a compressed byte array. (A 462MB JSON tree typically compresses down to **under 20MB**!).
    3. During database read: Stream the compressed bytes back through `GZipStream` to deserialize/serve it.

---

## 4️⃣ Connecting Frontend Services to the New API
*   Transition the React frontend away from local client-side processing (Web Workers) and configure it to use:
    *   `POST /api/v1/datasets/upload` to upload datasets.
    *   SignalR connection to listen for dynamic processing progress percent.
    *   `GET /api/v1/datasets/{id}/schema` to populate available axes dropdowns.
    *   `GET /api/v1/datasets/{id}/drill` to render individual hierarchical visual segments.
