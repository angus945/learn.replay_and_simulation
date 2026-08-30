using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameplayProtocol
{
    /// <summary>Bounded ingress; only Drain executes handlers on the creating thread, between simulation ticks.</summary>
    public sealed class ProtocolEndpoint : IProtocolIngress, IProtocolPump
    {
        private sealed class Route
        {
            internal ProtocolOperation Description;
            internal ProtocolHandler Handler;
        }
        private sealed class Work
        {
            internal ProtocolClient Client;
            internal ProtocolRequest Request;
            internal TaskCompletionSource<ProtocolResponse> Completion;
        }
        private sealed class Remembered
        {
            internal ProtocolRequest Request;
            internal ProtocolResponse Response;
        }
        private readonly object gate = new object();
        private readonly Queue<Work> pending = new Queue<Work>();
        private readonly Dictionary<string, Route> routes = new Dictionary<string, Route>(StringComparer.Ordinal);
        private readonly Dictionary<ProtocolClient, Dictionary<string, Remembered>> remembered = new Dictionary<ProtocolClient, Dictionary<string, Remembered>>();
        private readonly Func<string> sessionId;
        private readonly ProtocolLimits limits;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private ProtocolClient controller;
        private string controlledSession;
        private bool sealedRoutes;
        private bool draining;
        private int rememberedCount;
        private long historyBytes;

        public ProtocolEndpoint(Func<string> sessionId, ProtocolLimits limits = null)
        { this.sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId)); this.limits = limits ?? new ProtocolLimits(); }

        public void Register(ProtocolOperation operation, ProtocolHandler handler)
        {
            CheckThread();
            if (sealedRoutes) throw new InvalidOperationException("Routes are sealed.");
            if (operation == null || string.IsNullOrWhiteSpace(operation.Name) || handler == null) throw new ArgumentException("Invalid route.");
            if (operation.RequiresControl && !operation.RequiresSession) throw new ArgumentException("Control requires a session.");
            routes.Add(operation.Name, new Route { Description = operation, Handler = handler });
        }
        public void Seal() { CheckThread(); sealedRoutes = true; }
        public IReadOnlyList<ProtocolOperation> Describe()
        {
            CheckThread();
            List<ProtocolOperation> result = new List<ProtocolOperation>();
            foreach (Route route in routes.Values) result.Add(route.Description);
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            return result.AsReadOnly();
        }
        public void AcquireControl(ProtocolClient client, string expectedSession)
        {
            CheckThread();
            if (client == null || (client.Permissions & (ProtocolPermission.Act | ProtocolPermission.Drive | ProtocolPermission.Admin)) == 0)
                throw new ProtocolFault("permission.denied");
            if (string.IsNullOrEmpty(expectedSession) || expectedSession != sessionId()) throw new ProtocolFault("session.stale");
            if (controlledSession == expectedSession && controller != null && !ReferenceEquals(controller, client)) throw new ProtocolFault("control.owned");
            controlledSession = expectedSession; controller = client;
        }
        public void ReleaseControl(ProtocolClient client)
        {
            CheckThread();
            if (!ReferenceEquals(controller, client)) throw new ProtocolFault("control.not_owner");
            controller = null; controlledSession = null;
        }

        public Task<ProtocolResponse> Enqueue(ProtocolClient client, ProtocolRequest request)
        {
            string code = Validate(client, request);
            if (code != null) return Task.FromResult(new ProtocolResponse(request?.RequestId, request?.SessionId, code));
            lock (gate)
            {
                if (pending.Count >= limits.Pending) return Task.FromResult(new ProtocolResponse(request.RequestId, request.SessionId, "ingress.full"));
                Work work = new Work { Client = client, Request = request, Completion = new TaskCompletionSource<ProtocolResponse>(TaskCreationOptions.RunContinuationsAsynchronously) };
                pending.Enqueue(work); return work.Completion.Task;
            }
        }
        public int Drain(int maxRequests)
        {
            CheckThread();
            if (!sealedRoutes || draining) throw new InvalidOperationException("Seal routes and do not reenter Drain.");
            if (maxRequests < 1) throw new ArgumentOutOfRangeException(nameof(maxRequests));
            draining = true;
            int count = 0;
            try
            {
                while (count < maxRequests)
                {
                    Work work;
                    lock (gate) { if (pending.Count == 0) break; work = pending.Dequeue(); }
                    work.Completion.SetResult(Execute(work.Client, work.Request)); count++;
                }
            }
            finally { draining = false; }
            return count;
        }
        private ProtocolResponse Execute(ProtocolClient client, ProtocolRequest request)
        {
            if (!remembered.TryGetValue(client, out Dictionary<string, Remembered> records)) records = null;
            if (records != null && records.TryGetValue(request.RequestId, out Remembered previous))
            {
                bool same = previous.Request.Version == request.Version && previous.Request.SessionId == request.SessionId &&
                    previous.Request.Operation == request.Operation && previous.Request.PayloadJson == request.PayloadJson;
                return same ? previous.Response : new ProtocolResponse(request.RequestId, request.SessionId, "request.conflict");
            }
            // Reserve enough space before invoking a potentially mutating handler. Never execute without room to remember its outcome.
            if (rememberedCount >= limits.Remembered || historyBytes + limits.RequestBytes + limits.ResponseBytes + 2048 > limits.HistoryBytes)
                return new ProtocolResponse(request.RequestId, request.SessionId, "history.full");
            ProtocolResponse response;
            try
            {
                if (!routes.TryGetValue(request.Operation, out Route route)) throw new ProtocolFault("operation.unknown");
                if ((client.Permissions & route.Description.Permission) != route.Description.Permission) throw new ProtocolFault("permission.denied");
                if (route.Description.RequiresSession && (string.IsNullOrEmpty(request.SessionId) || request.SessionId != sessionId())) throw new ProtocolFault("session.stale");
                if (route.Description.RequiresControl && (controlledSession != request.SessionId || !ReferenceEquals(controller, client))) throw new ProtocolFault("control.required");
                string payload = route.Handler(client, request) ?? "{}";
                if (Encoding.UTF8.GetByteCount(payload) > limits.ResponseBytes) throw new ProtocolFault("response.too_large");
                response = new ProtocolResponse(request.RequestId, sessionId(), "ok", payload);
            }
            catch (ProtocolFault fault) { response = new ProtocolResponse(request.RequestId, request.SessionId, fault.Code); }
            catch (Exception) { response = new ProtocolResponse(request.RequestId, request.SessionId, "operation.failed"); }
            if (records == null) { records = new Dictionary<string, Remembered>(StringComparer.Ordinal); remembered.Add(client, records); }
            records.Add(request.RequestId, new Remembered { Request = request, Response = response }); rememberedCount++;
            historyBytes += Encoding.UTF8.GetByteCount(request.PayloadJson) + Encoding.UTF8.GetByteCount(response.PayloadJson ?? "") + 2048;
            return response;
        }
        private string Validate(ProtocolClient client, ProtocolRequest request)
        {
            if (client == null || request == null) return "request.invalid";
            if (request.Version != 1) return "version.unsupported";
            if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 128 || string.IsNullOrWhiteSpace(request.Operation) ||
                request.Operation.Length > 128 || request.SessionId == null || request.SessionId.Length > 128 || request.PayloadJson == null) return "request.invalid";
            if (request.PayloadJson.Length > limits.RequestBytes || Encoding.UTF8.GetByteCount(request.PayloadJson) > limits.RequestBytes) return "request.too_large";
            return null;
        }
        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Protocol pump/control must run on its owner thread.");
        }
    }
}
