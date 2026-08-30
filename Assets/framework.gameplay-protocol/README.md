# Gameplay Protocol Framework — v1 core

供 External Debug Overlay、Fuzzer、AI Test 共用的 transport-neutral 協定執行核心。
純 C#、noEngineReferences；不依賴 GameplayObservation、Unity、HTTP 或特定 client。
結構為 src/API、src/Contract、src/Runtime、tests。

## API

- `IProtocolIngress.Enqueue(client, request)`：可從背景 thread 呼叫，只排隊，回 Task。
- `IProtocolPump.Drain(maxRequests)`：只允許建立 endpoint 的 thread 呼叫；由 composition 在 tick 間驅動。
- `ProtocolEndpoint.Register/Seal/Describe`：註冊並封存操作目錄。handler 回傳 payload 字串，或丟出 ProtocolFault。
- `AcquireControl/ReleaseControl`：session-scoped exclusive control；供可信 composition／授權 route 使用。

## Contract

Envelope version 1：Version、RequestId、SessionId、Operation、PayloadJson。
Response：Version、RequestId、SessionId、Code、PayloadJson；Code=ok 才是 protocol operation 成功。
PayloadJson 是 JSON **字串**，不是 polymorphic object；外層序列化會跳脫，避免依賴遊戲型別。
Gameplay admission 與執行成功仍由 payload／ActionResult 判定，不能把 protocol ok 當成攻擊成功。

權限 Observe／Act／Drive／Admin 彼此獨立。ProtocolClient 由伺服端可信 composition 建立，
**不是** wire DTO，不接受 client 自報權限。Id 是顯示識別，真正 scope 是 server-held client instance。
Transport 必須維持該 instance，不能每個 request 都 new client，也不能只靠任意輸入 Id 做登入。

每個 client instance 的 RequestId 在 endpoint lifetime 內唯一：

- 相同 Version／SessionId／Operation／PayloadJson 重試：回傳已保存結果，不再執行。
- 相同 ID 不同內容：request.conflict；JSON whitespace 也算內容不同。
- 去重先於新 session／control 檢查：Reset 的原請求重試仍回原新 session ID，不 Reset 第二次。
- 讀取也受去重：想取得新 observation 必須使用新 RequestId。
- handler exception／response.too_large 也會記住，防止已發生 side effect 後重試再執行。
- 只保證本 endpoint／client instance lifetime 內至多執行一次，不是跨程序 crash 的 exactly-once。

控制權以 session ID 綁定，同一 session 只有一個 controller；多個 observers 可並存。
Session identity 改變後，舊控制權失效，新 session 必須重新領取。
Acquire/Release 與 Drain 限 owner thread；Drain 禁止 reentry。Caller 不可同步等待未 Drain 的 Task，避免主執行緒 deadlock。

## 有界負載與失敗

預設等待 128 筆、記住 4096 筆、request payload 64 KiB、response payload 1 MiB、history accounting 16 MiB。
History accounting 是 UTF-8 payload + 每筆 2048 bytes 預算，不是精準 managed heap 大小。
執行 handler 前預留回應空間；容量用完回 history.full，不淘汰舊紀錄。既有 request 重試仍可取得結果。
ingress.full／history.full／基本 envelope 驗證錯誤未執行且不佔 request history，可以之後重試。

Endpoint 不自行 timeout/cancel 已排隊請求。Client 等待逾時只代表未知，不代表未執行；
用同一 client instance／相同 request 重試取回結果。這一版沒有取消、斷線重連或 durable request log。
容量滿時須由可信 host 結束該 endpoint 工作階段，不能在原 session 偷清 history 並宣稱仍可安全重試。
Handler 必須有界；Drain 的數量上限不是時間 watchdog，也不能中斷卡住的 handler。
Transport 還必須在反序列化前限制整個 frame 大小，補上 authentication、rate limit、連線與 shutdown lifecycle。

## 本輪範圍

已完成核心與 `Assets/game/gameplay-protocol` adapter、JSON round trip／主執行緒／去重測試。
尚未開啟任何 socket 或修改 Demo 場景；沒有外部 client、Unity pump MonoBehaviour、HTTP/WebSocket。
Replay/failure 分頁下載、建立測試 session 的 factory route、連線關閉／重連與權限撤銷留待 transport 切片。

## 驗證

2026-08-30：Unity 編譯無錯誤，EditMode **123/123 通過**（framework 10、adapter 6 項新增測試）。
包含背景 ingress／owner-thread pump、pending/completed 重試、request conflict、權限／控制權、
session reset 重試、request/response/history 上限、JSON envelope round trip、ulong 字串精度、
protocol 與直接 gameplay 的逐 tick hash 一致性。純 .NET checks 同樣通過。
