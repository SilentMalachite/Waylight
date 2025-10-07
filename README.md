# Waylight - Local LLM Assistant (.NET 9 + SQLite)

ローカルPCで完結する .NET 9 / ASP.NET Core 製のLLMアシスタント。

## 主要機能

- **推論**: Ollama / LM Studio (OpenAI互換API) による完全ローカル推論
- **RAG**: SQLite FTS5 + MMR多様化による高度な文書検索
- **ストリーミング**: SSE による リアルタイムレスポンス配信
- **ツール実行**: 拡張可能な function-calling 風インタフェース
- **履歴管理**: トークン制限に応じた自動圧縮機能
- **アクセシビリティ**: A11yモード対応
- **設定**: ユーザーごとにbackend/model/prompt/temperature等をカスタマイズ可能

## セットアップ

### 前提条件

- .NET 9 SDK
- SQLite3
- Ollama または LM Studio (いずれか)

### インストール手順

1. **リポジトリをクローン**
```bash
git clone <repository-url>
cd Waylight
```

2. **データベースを初期化**
```bash
# Linux/macOS
chmod +x scripts/init_db.sh
./scripts/init_db.sh

# Windows
scripts\init_db.cmd
```

3. **設定ファイルを編集（オプション）**
```bash
cp src/LocalLlmAssistant/appsettings.json src/LocalLlmAssistant/appsettings.Development.json
# appsettings.Development.json を編集
```

4. **アプリケーションを起動**
```bash
dotnet run --project src/LocalLlmAssistant
```

5. **ブラウザで確認**
   - チャットUI: http://localhost:5099/
   - Swagger UI: http://localhost:5099/swagger
   - Health Check: http://localhost:5099/healthz

## 設定

### appsettings.json

```json
{
  "Rag": {
    "ChunkSize": 800,        // チャンクサイズ（文字数）
    "FtsCandidates": 200,    // FTS候補数
    "TopK": 4,               // 最終選択数
    "MmrLambda": 0.5         // MMRバランス (0.0-1.0)
  },
  "Llm": {
    "OllamaBaseUrl": "http://localhost:11434",
    "LmStudioBaseUrl": "http://localhost:1234/v1"
  }
}
```

### 環境変数（オプション）

- `OLLAMA_BASE_URL`: Ollama エンドポイント
- `LMSTUDIO_BASE_URL`: LM Studio エンドポイント
- `OPENAI_API_KEY`: LM Studio 用APIキー（デフォルト: dummy）

## APIエンドポイント

- `POST /api/messages`: チャットメッセージ送信
- `GET /api/stream`: SSEストリーム接続
- `POST /api/admin/ingest`: RAG文書投入
- `GET /healthz`: ヘルスチェック

## 開発

### ビルド
```bash
dotnet build
```

### テスト
```bash
dotnet test
```

### マイグレーション作成
```bash
dotnet ef migrations add <MigrationName> --project src/LocalLlmAssistant
```

## 実装済み機能（SPEC.md準拠）

### RAG with MMR多様化
- FTS5による候補抽出（`Rag:FtsCandidates`で設定可能）
- コサイン類似度による再ランク
- MMR（Maximal Marginal Relevance）による多様な結果選択
- Lambda パラメータ（`Rag:MmrLambda`）で関連性と多様性のバランス調整

### 履歴圧縮
- トークン制限に応じた自動会話履歴圧縮
- システムメッセージと最新メッセージを保持
- 将来: LLMベースの要約機能（TODO）

### ユーザー設定
- アクセシビリティモードフラグ
- バックエンド選択（Ollama/LM Studio）
- モデル、温度、最大トークン数などをユーザーごとに設定可能

### StreamBroker
- TraceIdによるリクエストトレーシング対応
- ユーザーごとのチャネル管理
- SSEイベント配信

## 今後の拡張（SPEC準拠）

- 週次RAG再インデックス自動化
- ヘルスモニタリングとバックエンドフェイルオーバー
- CIアクセシビリティチェック
- LLMベース履歴要約
- 認証/認可（Cookie/Token）
- プロンプトバージョニング

## ライセンス

LICENSEファイルを参照してください。
