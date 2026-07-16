# TenantManager Evaluation

This directory contains the AI evaluation datasets and scripts.

## Usage

You can validate the dataset or run it live.

```bash
dotnet run --project src/TenantManager.Evaluation/TenantManager.Evaluation.csproj -- validate
```

```bash
dotnet run --project src/TenantManager.Evaluation/TenantManager.Evaluation.csproj -- live --endpoint http://localhost:1234/v1
```

- `validate`: Structurally validates the JSON scenario files in `evaluation/scenarios`.
- `live`: Runs the evaluation against the real LLM endpoint, checking the generated answer and internal states using `IAssistantExecutionObserver`.
