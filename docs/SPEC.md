# 仕様書（.NET 9 + SQLite 版）
本書は、.NET 9 / ASP.NET Core + SQLite で動くローカルLLMアシスタントの実運用仕様どおり。  
Rails 版と等価の機能を .NET で再現しています。

## 0. ゴール/非ゴール
- ゴール: 利用者ごと設定、RAG根拠提示、ツール実行、監査・履歴、A11y、SSE、ローカル完結。
- 非ゴール: クラウド依存のベンダー機能、分散スケール。

## 1. アーキテクチャ
- Web: ASP.NET Core Web API
- DB: SQLite3 (EF Core)
- RAG: FTS5 候補抽出 → Embeddings コサイン再ランク
- 推論: Ollama / LM Studio(OpenAI互換API)
- ストリーム: SSE (StreamBroker による per-user 配信)
- ジョブ: 同期実装（最小）。必要に応じて BackgroundService に置換可。

## 2. データモデル
- UserPreference(backend, model, system_prompt, temperature, max_tokens, tools_enabled, rag_enabled)
- Conversation/Message(model, prompt_version, tool_calls_json, error_json, token_count)
- Document/DocumentChunk(text, embedding(JSON), FTS5連携)
- ToolLog(name, arguments, result, success, duration_ms)

## 3. API
- POST /messages: 受付 → SSEで token/done/error を配信
- GET /stream: SSE
- POST /admin/ingest: RAG 文書投入
- GET /healthz: ヘルス

## 4. RAG
- 800字チャンク、FTSで200候補、コサイン再ランクで上位k=4を system 追記
- 引用: UI側で [n] を根拠リストに結び付け
- ACL: 将来の認可導入で強化（UserId/Visibility）

## 5. ツール実行
- schema は function-calling 互換（get_time, echo）。
- 実行ログは ToolLogs にJSONで保存。

## 6. 認証/認可（ガイド）
- 今は guest。運用時は Cookie/Token 認証を追加し UserId を埋める。
- Pundit 相当は .NET 標準の Policy で実装。

## 7. 観測性
- ASP.NET の構造化ログ + イベントキー（chat.completed, tool.invoked など）

## 8. セキュリティ/プライバシー
- PII最小化、削除要求対応、IDは推測困難値（将来 GUID）。

## 9. デプロイ/性能
- 1台PCでOK。FTSと埋め込みで数千〜数万チャンクも現実的。

## 10. テスト
- Retriever の再ランク determinism、SSE 初期トークン遅延など。

## 11. 変更容易性
- backend/model/prompt は UserPreference に集約。Prompt versioning を将来追加。

以上。
