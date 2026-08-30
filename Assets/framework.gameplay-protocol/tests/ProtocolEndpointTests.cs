using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace GameplayProtocol.Tests
{
    public sealed class ProtocolEndpointTests
    {
        private static ProtocolClient Client() => new ProtocolClient("test", ProtocolPermission.Observe | ProtocolPermission.Act | ProtocolPermission.Drive | ProtocolPermission.Admin);
        private static ProtocolRequest Request(string id = "1", string payload = "{}", string session = "s") => new ProtocolRequest(1, id, session, "test", payload);
        private static ProtocolEndpoint Endpoint(ProtocolHandler handler, ProtocolLimits limits = null)
        {
            ProtocolEndpoint endpoint = new ProtocolEndpoint(() => "s", limits);
            endpoint.Register(new ProtocolOperation("test", ProtocolPermission.Observe, true, false), handler); endpoint.Seal(); return endpoint;
        }
        [Test]
        public void DuplicatePendingAndCompletedRequestsExecuteOnlyOnce()
        {
            int calls = 0;
            ProtocolEndpoint endpoint = Endpoint((c, r) => (++calls).ToString());
            ProtocolClient client = Client();
            Task<ProtocolResponse> first = endpoint.Enqueue(client, Request());
            Task<ProtocolResponse> duplicate = endpoint.Enqueue(client, Request());
            Assert.That(first.IsCompleted, Is.False);
            endpoint.Drain(10);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(duplicate.Result, Is.SameAs(first.Result));
            Task<ProtocolResponse> retry = endpoint.Enqueue(client, Request()); endpoint.Drain(1);
            Assert.That(retry.Result, Is.SameAs(first.Result));
        }
        [Test]
        public void ReusingRequestIdWithDifferentContentConflicts()
        {
            ProtocolEndpoint endpoint = Endpoint((c, r) => "{}"); ProtocolClient client = Client();
            endpoint.Enqueue(client, Request()); endpoint.Drain(1);
            Task<ProtocolResponse> changed = endpoint.Enqueue(client, Request(payload: "{ }")); endpoint.Drain(1);
            Assert.That(changed.Result.Code, Is.EqualTo("request.conflict"));
        }
        [Test]
        public void PermissionAndStaleSessionAreCheckedBeforeHandler()
        {
            int calls = 0; ProtocolEndpoint endpoint = Endpoint((c, r) => (++calls).ToString());
            Task<ProtocolResponse> denied = endpoint.Enqueue(new ProtocolClient("reader", ProtocolPermission.None), Request());
            Task<ProtocolResponse> stale = endpoint.Enqueue(Client(), Request(session: "old")); endpoint.Drain(2);
            Assert.That(denied.Result.Code, Is.EqualTo("permission.denied"));
            Assert.That(stale.Result.Code, Is.EqualTo("session.stale")); Assert.That(calls, Is.Zero);
        }
        [Test]
        public void IngressAndHistoryLimitsNeverSilentlyEvict()
        {
            ProtocolEndpoint endpoint = Endpoint((c, r) => "{}", new ProtocolLimits(pending: 1, remembered: 1));
            ProtocolClient client = Client(); Task<ProtocolResponse> first = endpoint.Enqueue(client, Request());
            Assert.That(endpoint.Enqueue(client, Request("2")).Result.Code, Is.EqualTo("ingress.full")); endpoint.Drain(1);
            Task<ProtocolResponse> full = endpoint.Enqueue(client, Request("2")); endpoint.Drain(1);
            Assert.That(full.Result.Code, Is.EqualTo("history.full"));
            Task<ProtocolResponse> retry = endpoint.Enqueue(client, Request()); endpoint.Drain(1);
            Assert.That(retry.Result, Is.SameAs(first.Result));
        }
        [Test]
        public void InvalidVersionAndOversizeRequestAreRejectedWithoutQueueing()
        {
            ProtocolEndpoint endpoint = Endpoint((c, r) => "{}", new ProtocolLimits(requestBytes: 4));
            Assert.That(endpoint.Enqueue(Client(), new ProtocolRequest(2, "1", "s", "test")).Result.Code, Is.EqualTo("version.unsupported"));
            Assert.That(endpoint.Enqueue(Client(), Request(payload: "12345")).Result.Code, Is.EqualTo("request.too_large"));
            Assert.That(endpoint.Drain(1), Is.Zero);
        }
        [Test]
        public void BackgroundIngressDoesNotExecuteAndBackgroundDrainFails()
        {
            int owner = Thread.CurrentThread.ManagedThreadId; int ran = 0;
            ProtocolEndpoint endpoint = Endpoint((c, r) => { ran = Thread.CurrentThread.ManagedThreadId; return "{}"; });
            Task<ProtocolResponse> response = null;
            Thread worker = new Thread(() => { response = endpoint.Enqueue(Client(), Request()); }); worker.Start(); worker.Join();
            Assert.That(response.IsCompleted, Is.False); endpoint.Drain(1); Assert.That(ran, Is.EqualTo(owner));
            Exception failure = null;
            Thread invalid = new Thread(() => { try { endpoint.Drain(1); } catch (Exception e) { failure = e; } }); invalid.Start(); invalid.Join();
            Assert.That(failure, Is.InstanceOf<InvalidOperationException>());
        }
        [Test]
        public void HandlerFailureIsRememberedAndDoesNotLeakExceptionDetails()
        {
            int calls = 0;
            ProtocolEndpoint endpoint = Endpoint((c, r) => { calls++; throw new Exception("secret stack detail"); });
            ProtocolClient client = Client(); Task<ProtocolResponse> first = endpoint.Enqueue(client, Request()); endpoint.Drain(1);
            endpoint.Enqueue(client, Request()); endpoint.Drain(1);
            Assert.That(first.Result.Code, Is.EqualTo("operation.failed")); Assert.That(first.Result.PayloadJson, Is.Null); Assert.That(calls, Is.EqualTo(1));
        }
        [Test]
        public void ControlIsExclusiveAndInvalidatedBySessionChange()
        {
            string session = "s"; int calls = 0;
            ProtocolEndpoint endpoint = new ProtocolEndpoint(() => session);
            endpoint.Register(new ProtocolOperation("test", ProtocolPermission.Act, true, true), (c, r) => (++calls).ToString()); endpoint.Seal();
            ProtocolClient a = Client(), b = Client(); endpoint.AcquireControl(a, "s");
            Assert.Throws<ProtocolFault>(() => endpoint.AcquireControl(b, "s"));
            Task<ProtocolResponse> denied = endpoint.Enqueue(b, Request()); endpoint.Drain(1);
            Assert.That(denied.Result.Code, Is.EqualTo("control.required"));
            session = "new"; Task<ProtocolResponse> staleLease = endpoint.Enqueue(a, Request(session: "new")); endpoint.Drain(1);
            Assert.That(staleLease.Result.Code, Is.EqualTo("control.required"));
            endpoint.AcquireControl(b, "new"); Assert.That(calls, Is.Zero);
        }
        [Test]
        public void HistoryByteBudgetIsReservedBeforeExecution()
        {
            int calls = 0;
            ProtocolEndpoint endpoint = Endpoint((c, r) => { calls++; return "{}"; }, new ProtocolLimits(requestBytes: 4, responseBytes: 4, historyBytes: 2056));
            ProtocolClient client = Client(); endpoint.Enqueue(client, Request()); endpoint.Drain(1);
            Task<ProtocolResponse> full = endpoint.Enqueue(client, Request("2")); endpoint.Drain(1);
            Assert.That(full.Result.Code, Is.EqualTo("history.full")); Assert.That(calls, Is.EqualTo(1));
        }
        [Test]
        public void OversizeResponseIsRememberedAfterSideEffects()
        {
            int calls = 0; ProtocolEndpoint endpoint = Endpoint((c, r) => { calls++; return "12345"; }, new ProtocolLimits(responseBytes: 4));
            ProtocolClient client = Client(); Task<ProtocolResponse> first = endpoint.Enqueue(client, Request()); endpoint.Drain(1);
            endpoint.Enqueue(client, Request()); endpoint.Drain(1);
            Assert.That(first.Result.Code, Is.EqualTo("response.too_large")); Assert.That(calls, Is.EqualTo(1));
        }
    }
}
