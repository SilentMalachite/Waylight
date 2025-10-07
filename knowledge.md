# Waylight - Local LLM Assistant

## Project Overview
ASP.NET Core web API providing a local LLM assistant with RAG (Retrieval-Augmented Generation) capabilities, tool execution, and streaming responses.

## Architecture

### Core Components
- **LLM Clients**: Supports both Ollama and LM Studio backends
- **RAG System**: Document ingestion, chunking, and vector-based retrieval
- **Tool System**: Extensible tool registry and execution framework
- **Streaming**: Server-Sent Events (SSE) for real-time responses

### Key Services
- `OllamaClient` / `LmStudioClient`: LLM provider implementations
- `EmbeddingsClient`: Generates embeddings for documents
- `Retriever`: Vector similarity search for relevant context
- `ToolRunner` / `ToolRegistry`: Tool execution infrastructure

### Database
- SQLite with Entity Framework Core
- Stores conversations, messages, documents, and tool logs
- Full-text search enabled for document retrieval

## Development

### Running the Project
```bash
dotnet run --project src/LocalLlmAssistant
```

### Building
```bash
dotnet build
```

### Testing
Check compilation and run any tests:
```bash
dotnet build && dotnet test
```

## Configuration
- Main settings in `appsettings.json` and `appsettings.Development.json`
- Configure LLM backend endpoints and model names
- Database connection string for SQLite

## API Endpoints
- `/api/messages`: Chat message handling
- `/api/stream`: SSE streaming for real-time responses
- `/api/admin/ingest`: Document ingestion for RAG
- `/api/health`: Health check endpoint

## Implemented Features (from SPEC.md)

### RAG with MMR Diversification
- FTS5 for candidate retrieval (configurable via `Rag:FtsCandidates`)
- Cosine similarity re-ranking
- MMR (Maximal Marginal Relevance) for diverse results
- Lambda parameter (`Rag:MmrLambda`) balances relevance vs diversity (0.5 default)
- TopK configurable via `Rag:TopK`

### History Compression
- `HistoryCompressor` service automatically compresses conversation history
- Keeps system messages and recent messages within token limits
- MAX_TOKENS_THRESHOLD: 8000, TARGET_TOKENS: 4000
- Future: LLM-based summarization (TODO)

### User Preferences
- `AccessibilityMode` flag for A11y support
- Backend selection (Ollama/LM Studio)
- Model, temperature, max_tokens configurable per user
- Tools and RAG can be enabled/disabled

### StreamBroker
- Added `TraceId` to StreamEvent for request tracing
- Per-user channel management
- SSE event publishing with trace support

### Configuration
- `RagConfig`: Centralized RAG parameters
- `LlmConfig`: Backend URLs (overridable by env vars)
- All settings injectable via IOptions<T>

## Setup Instructions

1. Run `scripts/init_db.sh` (or .cmd on Windows) to initialize database
2. Configure `appsettings.json` as needed
3. Start Ollama or LM Studio backend
4. Run `dotnet run --project src/LocalLlmAssistant`

## Future Enhancements (from SPEC docs)

- Weekly RAG re-indexing automation
- Health monitoring and backend failover
- CI accessibility checks
- LLM-based history summarization
- Authentication/authorization (Cookie/Token)
- Policy-based authorization (.NET standard)
- Prompt versioning
