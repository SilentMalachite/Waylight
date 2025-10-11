# Waylight - 開発者向けクイックリファレンス

## 🚀 クイックスタート

### 前提条件
```bash
# .NET 9 SDK
dotnet --version

# Ollama または LM Studio
# Ollama: https://ollama.ai/
# LM Studio: https://lmstudio.ai/
```

### 初回セットアップ
```bash
# 1. リポジトリクローン
git clone <repository-url>
cd Waylight

# 2. データベース初期化
chmod +x scripts/init_db.sh
./scripts/init_db.sh

# 3. モデルダウンロード (Ollama)
ollama pull qwen2.5-coder:7b
ollama pull nomic-embed-text:latest

# 4. アプリケーション起動
dotnet run --project src/LocalLlmAssistant
```

### アクセス
- **チャットUI**: http://localhost:5099/
- **Swagger API**: http://localhost:5099/swagger
- **Health Check**: http://localhost:5099/healthz

---

## 📁 プロジェクト構造

```
Waylight/
├── src/LocalLlmAssistant/
│   ├── Controllers/          # API エンドポイント
│   │   ├── MessagesController.cs      # チャット処理
│   │   ├── StreamController.cs        # SSE ストリーム
│   │   ├── AdminIngestController.cs   # RAG 文書投入
│   │   └── ChainController.cs         # Chain-of-Thought
│   ├── Services/
│   │   ├── Llm/              # LLM クライアント
│   │   ├── Rag/              # RAG 検索
│   │   ├── Embeddings/       # 埋め込みベクトル
│   │   ├── Tools/            # ツール実行
│   │   └── Chain/            # チェーン処理
│   ├── Models/               # データモデル
│   ├── Data/                 # DB コンテキスト
│   ├── DTOs/                 # リクエスト/レスポンス
│   ├── SSE/                  # Server-Sent Events
│   └── wwwroot/              # フロントエンド
├── scripts/                  # セットアップスクリプト
└── docs/                     # ドキュメント
```

---

## 🔧 開発コマンド

### ビルド & 実行
```bash
# ビルド
dotnet build

# 実行
dotnet run --project src/LocalLlmAssistant

# ウォッチモード（自動再起動）
dotnet watch --project src/LocalLlmAssistant
```

### データベース
```bash
# マイグレーション作成
dotnet ef migrations add <MigrationName> --project src/LocalLlmAssistant

# マイグレーション適用
dotnet ef database update --project src/LocalLlmAssistant

# マイグレーション削除
dotnet ef migrations remove --project src/LocalLlmAssistant

# データベースリセット
rm -f src/LocalLlmAssistant/App_Data/app.db*
dotnet ef database update --project src/LocalLlmAssistant
```

### テスト
```bash
# テスト実行（実装後）
dotnet test

# カバレッジ取得
dotnet test /p:CollectCoverage=true
```

---

## 🔌 API エンドポイント

### チャット
```bash
# メッセージ送信
POST /api/messages
Content-Type: application/json

{
  "content": "こんにちは",
  "backend": "ollama",
  "conversationId": 1
}

# SSE ストリーム接続
GET /api/stream?userId=guest
```

### RAG 文書投入
```bash
POST /api/admin/ingest
Content-Type: application/json

{
  "text": "投入する文書内容..."
}
```

### Chain-of-Thought
```bash
POST /api/chain
Content-Type: application/json

{
  "query": "複雑な問題を段階的に解決してください",
  "chainModels": ["reasoner", "synthesizer"],
  "maxIterations": 3,
  "enableCoT": true
}
```

### ヘルスチェック
```bash
GET /healthz
```

---

## ⚙️ 設定

### appsettings.json
```json
{
  "Rag": {
    "ChunkSize": 800,
    "FtsCandidates": 200,
    "TopK": 4,
    "MmrLambda": 0.5
  },
  "Llm": {
    "OllamaBaseUrl": "http://localhost:11434",
    "LmStudioBaseUrl": "http://localhost:1234/v1"
  },
  "Chain": {
    "MaxIterations": 5,
    "EnableCoT": true
  }
}
```

### 環境変数
```bash
# Ollama 設定
export OLLAMA_BASE_URL="http://localhost:11434"
export OLLAMA_MODEL="qwen2.5-coder:7b"

# LM Studio 設定
export LMSTUDIO_BASE_URL="http://localhost:1234/v1"
export OPENAI_API_KEY="dummy"

# 埋め込みモデル
export EMBEDDINGS_BACKEND="ollama"
export EMBEDDINGS_MODEL="nomic-embed-text:latest"
```

---

## 🐛 デバッグ

### ログレベル変更
```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "LocalLlmAssistant": "Debug"
    }
  }
}
```

### よくある問題

#### 1. データベース接続エラー
```bash
# 解決方法
rm -f src/LocalLlmAssistant/App_Data/app.db*
./scripts/init_db.sh
```

#### 2. Ollama/LM Studio 接続エラー
```bash
# Ollama起動確認
curl http://localhost:11434/api/tags

# LM Studio起動確認
curl http://localhost:1234/v1/models
```

#### 3. FTS検索エラー
```bash
# FTSテーブル作成
sqlite3 src/LocalLlmAssistant/App_Data/app.db < scripts/create_fts.sql
```

---

## 📊 パフォーマンス最適化

### 推奨設定
```json
// appsettings.json
{
  "Rag": {
    "ChunkSize": 800,        // 小さすぎると検索精度低下
    "FtsCandidates": 200,    // 多いほど高精度だが遅い
    "TopK": 4,               // 最終的に使用するチャンク数
    "MmrLambda": 0.5         // 0.0=多様性重視, 1.0=関連性重視
  }
}
```

### データベースメンテナンス
```bash
# VACUUM（データベース最適化）
sqlite3 src/LocalLlmAssistant/App_Data/app.db "VACUUM;"

# 統計情報更新
sqlite3 src/LocalLlmAssistant/App_Data/app.db "ANALYZE;"
```

---

## 🧪 テスト（実装例）

### 単体テスト
```csharp
// Tests/Services/Rag/RetrieverTests.cs
public class RetrieverTests
{
    [Fact]
    public async Task SimilarChunksAsync_ReturnsTopK()
    {
        // Arrange
        var retriever = CreateRetriever();
        
        // Act
        var result = await retriever.SimilarChunksAsync("test query", k: 3);
        
        // Assert
        Assert.Equal(3, result.Count);
    }
}
```

### 統合テスト
```csharp
// Tests/Controllers/MessagesControllerTests.cs
public class MessagesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Create_ReturnsAccepted()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { content = "Hello" };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/messages", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
```

---

## 🔐 セキュリティチェックリスト

- [ ] 入力検証（最大長、null チェック）
- [ ] 出力エスケープ（XSS 対策）
- [ ] SQL インジェクション対策（EF Core パラメータ化）
- [ ] レート制限
- [ ] 認証・認可
- [ ] HTTPS 強制（本番環境）
- [ ] 機密情報の環境変数化
- [ ] エラーメッセージの適切な隠蔽

---

## 📚 参考リンク

### ドキュメント
- [README.md](README.md) - プロジェクト概要
- [FIXES_SUMMARY.md](FIXES_SUMMARY.md) - 過去の修正履歴
- [SECURITY_IMPROVEMENTS.md](SECURITY_IMPROVEMENTS.md) - セキュリティ改善
- [ISSUES_FIXED.md](ISSUES_FIXED.md) - 最新の修正内容

### 外部リソース
- [.NET 9 Documentation](https://learn.microsoft.com/ja-jp/dotnet/core/whats-new/dotnet-9)
- [Entity Framework Core](https://learn.microsoft.com/ja-jp/ef/core/)
- [Ollama Documentation](https://github.com/ollama/ollama)
- [SQLite FTS5](https://www.sqlite.org/fts5.html)

---

## 🤝 コントリビューション

### ブランチ戦略
```bash
main          # 本番環境
├── develop   # 開発環境
└── feature/* # 機能開発
```

### コミットメッセージ規約
```
<type>(<scope>): <subject>

Types:
- feat: 新機能
- fix: バグ修正
- docs: ドキュメント
- style: コードスタイル
- refactor: リファクタリング
- test: テスト
- chore: その他
```

### プルリクエスト
1. feature ブランチ作成
2. 変更とテスト
3. PR 作成
4. レビュー
5. マージ

---

**最終更新**: 2025年10月11日  
**バージョン**: 1.0.0
