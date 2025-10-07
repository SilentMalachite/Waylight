-- SQLite FTS5 virtual table & triggers
CREATE VIRTUAL TABLE IF NOT EXISTS document_chunks_fts USING fts5(text, content='DocumentChunks', content_rowid='Id');
CREATE TRIGGER IF NOT EXISTS document_chunks_ai AFTER INSERT ON DocumentChunks BEGIN
  INSERT INTO document_chunks_fts(rowid, text) VALUES (new.Id, new.Text);
END;
CREATE TRIGGER IF NOT EXISTS document_chunks_ad AFTER DELETE ON DocumentChunks BEGIN
  INSERT INTO document_chunks_fts(document_chunks_fts, rowid, text) VALUES('delete', old.Id, old.Text);
END;
CREATE TRIGGER IF NOT EXISTS document_chunks_au AFTER UPDATE ON DocumentChunks BEGIN
  INSERT INTO document_chunks_fts(document_chunks_fts, rowid, text) VALUES('delete', old.Id, old.Text);
  INSERT INTO document_chunks_fts(rowid, text) VALUES (new.Id, new.Text);
END;
