# module.diagnostic-trace

純 C#、不依賴 Unity／simulation／testability 的有限增量 journal。
`src/API` 為讀寫 ports、`src/Contract` 為 cursor／batch、`src/Runtime` 為 buffer，測試在 `tests`。

```csharp
TraceBuffer<string> buffer = new TraceBuffer<string>(512);
ITraceWriter<string> writer = buffer.Writer;
ITraceReader<string> reader = buffer.Reader;
writer.Record("started");
TraceBatch<string> page = reader.Read(default, 64);
TraceCursor cursor = page.NextCursor;
TraceBatch<string> next = reader.Read(cursor, 64);
```

## API／Contract

- owner 分別提供 Writer／Reader facade；Reader 不能 cast 成 Writer 或 buffer。
- 紀錄 Sequence 從 1 遞增，是 journal 位置，不是遊戲 Action sequence、tick 或 wall clock。
- Cursor = StreamId + AfterSequence，採 exclusive 語意。default 表示從目前保留的最早紀錄讀起。
- Read 不消耗來源、不更動其他 reader。多個工具各自持有 cursor。
- 同 stream 的未來 cursor／非正數 maxItems 被拒絕；讀取量最多為來源容量。
- MissedCount 是這次 cursor 尚未讀到但已被覆蓋的筆數；使用 NextCursor 不會重複計入。
- OverwrittenCount 是來源自建立以來累計被覆蓋筆數，不等於某個 reader 遺失筆數。
- StreamChanged=true 表示 cursor 屬於另一個 stream。回傳新 stream 最早保留資料，consumer 必須清除舊 stream 的 local history。
- StreamChanged 無法量化舊 stream 最後尚未讀取的資料；此時 MissedCount 僅描述新 stream 已被覆蓋的前綴。
- HasMore 表示還有未讀的保留紀錄；NextCursor 是本頁最後已讀位置。
- Sequence 耗盡明確失敗，不 wrap；新 buffer 產生新 stream identity，沒有跨 stream 重用序號的混淆。
- Batch 擁有自己的唯讀紀錄陣列，但不深拷貝 T。T 必須為不可變 payload。
- 單執行緒；未提供 thread safety、持久化或 transport。容量限制的是筆數，不是任意 payload 的 byte 數。

Simulation 的 Session／Tick／Wave／Actor 欄位留在 framework.testability 的 TraceEntry payload，本 module 不解釋它們。
