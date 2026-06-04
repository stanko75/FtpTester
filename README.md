# FTP Tester

Production-oriented ASP.NET Core Minimal API dashboard for testing FTP, FTPS, and SFTP connectivity and transfer performance. The app is designed for Azure App Service on Linux and uses FluentFTP for FTP/FTPS plus SSH.NET for SFTP.

## Features

- Test FTP, FTPS, and SFTP connections.
- Upload, download, list directory, and delete remote files.
- Benchmark upload/download duration and throughput.
- Single-page Bootstrap 5 dashboard with jQuery AJAX.
- Timestamped UI logs, operation metrics, and in-memory test history.
- `/health` endpoint for Azure health checks.
- Environment-variable configuration through the `FTPTESTER_` prefix.

## API Endpoints

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/api/test-connection` | Tests server connectivity and credentials. |
| `POST` | `/api/upload` | Uploads a multipart file to a remote path. |
| `POST` | `/api/download` | Downloads a remote file. |
| `POST` | `/api/list-directory` | Lists a remote directory. |
| `POST` | `/api/delete-file` | Deletes a remote file. |
| `POST` | `/api/benchmark` | Runs upload/download benchmark using a temporary remote file. |
| `GET` | `/api/history` | Returns recent in-memory operation history. |
| `GET` | `/health` | Health probe endpoint. |

## Configuration

Settings can be supplied through `appsettings.json` or Azure App Service application settings using the `FTPTESTER_` prefix.

Example Azure setting:

```text
FTPTESTER_Limits__MaxUploadBytes=104857600
```

The app intentionally stores history in memory to avoid local file dependencies. For multi-instance App Service plans, history is per instance; persist it to Azure Table Storage, Cosmos DB, or SQL if durable cross-instance history is required.

## Local Run

```bash
dotnet restore
dotnet build
dotnet run
```

Open `https://localhost:<port>/` and use the dashboard.

## Azure App Service Linux Deployment

1. Create an App Service using a Linux runtime that supports .NET 10.
2. Configure application settings:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `FTPTESTER_Limits__MaxUploadBytes=104857600` (adjust as needed)
3. Enable HTTPS Only in the App Service TLS/SSL settings.
4. Configure Health Check path as `/health`.
5. Publish and deploy:

```bash
dotnet publish -c Release -o ./publish
az webapp deploy --resource-group <resource-group> --name <app-name> --src-path ./publish --type zip
```

Alternatively, deploy from GitHub Actions or Azure DevOps using the generated publish output.

## Security Notes

- Passwords are never stored in local files by this app.
- Operation history stores host, protocol, timing, and result metadata, not passwords.
- Keep FTPS certificate validation enabled for production usage.
- Limit upload size with `FTPTESTER_Limits__MaxUploadBytes` based on your App Service SKU.
