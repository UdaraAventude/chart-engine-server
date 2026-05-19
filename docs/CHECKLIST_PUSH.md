Pre-push checklist for ChartEngine Server

1. Ensure no secrets are committed
   - appsettings.json and appsettings.Development.json should NOT contain real credentials.
   - Use `dotnet user-secrets` for development secrets.
   - For production, use environment variables or a managed secret store.

2. Verify build and tests
   - `dotnet build`
   - `dotnet test`

3. Update dependencies
   - Ensure vulnerable packages are updated (e.g., Newtonsoft.Json upgraded).

4. Git hygiene
   - Add/verify `.gitignore` includes `secrets.json`, `/bin`, `/obj`.
   - If secrets were committed previously, rotate and remove from history (use BFG or git filter-repo).

5. CI/CD
   - Ensure GitHub Actions or other CI runs `dotnet build` and `dotnet test` on PRs.
   - Store production secrets in CI secret store, not in repo.

6. Documentation
   - Add `appsettings*.json.template` files with placeholders.
   - Document how to configure `dotnet user-secrets` and production secrets.

7. Final checks before push
   - `git status` to review changes
   - `git add -A`
   - `git commit -m "<commit message>"`
   - `git push origin <branch>`

Commands to move dev connection string to user-secrets (example):

```powershell
cd src\ChartEngine.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost\\SQLEXPRESS;Database=ChartEngine;Trusted_Connection=True;TrustServerCertificate=True;"
```

Commands to update package (example):

```powershell
cd <repo-root>
dotnet add src\ChartEngine.Application\ChartEngine.Application.csproj package Newtonsoft.Json --version 13.0.3
dotnet restore
dotnet build
dotnet test
```
