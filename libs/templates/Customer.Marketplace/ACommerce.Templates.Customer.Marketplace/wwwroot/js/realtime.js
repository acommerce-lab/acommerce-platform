// Ejar realtime client — wraps @microsoft/signalr loaded from CDN.
// Called from EjarRealtimeService via JS interop.

let connection = null;
let dotnetRef = null;

// انتظر تحميل signalR العالميّ — على شبكة الهاتف البطيئة قد يبدأ Blazor
// تنفيذ start() قبل ما ينتهي تحميل signalr.min.js من CDN، فيخرج
// "signalR is not defined". ننتظر حتى ١٠ ثوانٍ بفحص كلّ ١٠٠ms.
function waitForSignalR(timeoutMs) {
    return new Promise((resolve, reject) => {
        const t0 = Date.now();
        (function check() {
            if (typeof signalR !== 'undefined') return resolve();
            if (Date.now() - t0 >= (timeoutMs || 10000))
                return reject(new Error('signalR script load timeout'));
            setTimeout(check, 100);
        })();
    });
}

// Tiny notification sound (short beep) using Web Audio API — no file needed.
function playBeep() {
    try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.type = "sine";
        osc.frequency.value = 880;
        gain.gain.setValueAtTime(0.3, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.4);
        osc.start(ctx.currentTime);
        osc.stop(ctx.currentTime + 0.4);
    } catch (_) { /* AudioContext blocked — silent fail */ }
}

export async function start(hubUrl, token, ref) {
    if (connection) return;
    dotnetRef = ref;

    try { await waitForSignalR(10000); }
    catch (e) {
        console.warn("[Ejar Realtime] signalR لم يُحمَّل في الوقت المناسب:", e.message);
        return;
    }

    const builder = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
            accessTokenFactory: () => token,
            // نُسقط WebSockets — runasp.net (IIS مشترك) لا يدعمها بشكل موثوق
            // ولو أعلنها الخادم متاحة، client signalr 8.x يحاولها أوّلاً ويفشل
            // بلا fallback تلقائيّ. SSE + LongPolling كلاهما HTTP عاديّ يعمل
            // على أيّ مستضيف. عندما ننقل إلى Linux/Kestrel نُعيد WebSockets.
            transport: signalR.HttpTransportType.ServerSentEvents |
                       signalR.HttpTransportType.LongPolling
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    builder.on("ReceiveMessage", (data) => {
        playBeep();
        dotnetRef.invokeMethodAsync("OnMessage", JSON.stringify(data));
    });

    // chat.message — sent on the chat:conv:{id} group (when the recipient has
    // an open ChatRoom for that conversation). Routed to IChatClient via the
    // realtime service.
    builder.on("chat.message", (data) => {
        dotnetRef.invokeMethodAsync("OnChatMessage", JSON.stringify(data));
    });

    builder.on("ReceiveNotification", (data) => {
        playBeep();
        dotnetRef.invokeMethodAsync("OnNotification", JSON.stringify(data));
    });

    builder.onreconnected(() => {
        dotnetRef.invokeMethodAsync("OnReconnected");
    });

    builder.start()
        .then(() => {
            console.info("[Ejar Realtime] connected via",
                builder.connection?.transport?.constructor?.name || "unknown transport");
            dotnetRef.invokeMethodAsync("OnConnected");
        })
        .catch(err => console.warn("[Ejar Realtime] connect error:", err));

    connection = builder;
}

export function stop() {
    if (connection) {
        connection.stop();
        connection = null;
    }
}

export function updateToken(token) {
    // SignalR reconnects automatically and will call accessTokenFactory again.
    // No action needed here — token closure is updated on next reconnect.
}

// Subscribe to the browser's beforeunload event so the chat client can fire
// a synchronous "leave current chat" beacon to the backend before the tab
// closes. The Blazor circuit may be torn down before async cleanup completes;
// sendBeacon is fire-and-forget and survives that.
//   ref.invokeMethodAsync('OnBeforeUnload') is called via DotNet.
let _unloadHandler = null;
export function registerBeforeUnload(ref) {
    if (_unloadHandler) return;
    _unloadHandler = () => { ref.invokeMethodAsync('OnBeforeUnload'); };
    window.addEventListener('beforeunload', _unloadHandler);
}
export function unregisterBeforeUnload() {
    if (_unloadHandler) window.removeEventListener('beforeunload', _unloadHandler);
    _unloadHandler = null;
}

// Fire-and-forget HTTP POST that survives page unload. Used to call the
// backend chat-leave endpoint when the tab closes.
export function leaveChatBeacon(path, token) {
    try {
        const blob = new Blob([JSON.stringify({})], { type: 'application/json' });
        // sendBeacon doesn't allow custom headers — fall back to fetch with keepalive.
        if (token) {
            fetch(path, {
                method: 'POST', keepalive: true,
                headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },
                body: '{}'
            }).catch(() => {});
        } else {
            navigator.sendBeacon(path, blob);
        }
    } catch (_) { /* nothing more we can do at unload */ }
}
