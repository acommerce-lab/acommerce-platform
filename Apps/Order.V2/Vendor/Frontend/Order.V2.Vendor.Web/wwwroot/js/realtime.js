// Order V2 realtime client — thin wrapper over SignalR loaded from CDN.
// Called from OrderV2RealtimeService via JS interop.

let _connection = null;
let _dotnetRef = null;

function playBeep(freq, dur) {
    try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.type = "sine";
        osc.frequency.value = freq;
        gain.gain.setValueAtTime(0.25, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + dur);
        osc.start(ctx.currentTime);
        osc.stop(ctx.currentTime + dur);
    } catch (_) { /* audio blocked — silent */ }
}

export async function start(hubUrl, token, ref) {
    if (_connection) return;
    _dotnetRef = ref;

    _connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    _connection.on("ReceiveMessage", (data) => {
        playBeep(440, 0.15);
        _dotnetRef.invokeMethodAsync("OnMessage", typeof data === "string" ? data : JSON.stringify(data));
    });

    _connection.on("chat.message", (data) => {
        _dotnetRef.invokeMethodAsync("OnChatMessage", typeof data === "string" ? data : JSON.stringify(data));
    });

    _connection.on("ReceiveNotification", (data) => {
        playBeep(520, 0.2);
        _dotnetRef.invokeMethodAsync("OnNotification", typeof data === "string" ? data : JSON.stringify(data));
    });

    _connection.onreconnected(() => _dotnetRef.invokeMethodAsync("OnReconnected"));

    try {
        await _connection.start();
        _dotnetRef.invokeMethodAsync("OnConnected");
    } catch (err) {
        console.error("[Order V2 Vendor Realtime] start failed:", err);
        _connection = null;
    }
}

export async function stop() {
    if (_connection) { await _connection.stop(); _connection = null; }
}

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

export function leaveChatBeacon(path, token, bodyJson) {
    try {
        if (token) {
            fetch(path, {
                method: 'POST', keepalive: true,
                headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },
                body: bodyJson ?? '{}'
            }).catch(() => {});
        } else {
            const blob = new Blob([bodyJson ?? '{}'], { type: 'application/json' });
            navigator.sendBeacon(path, blob);
        }
    } catch (_) { /* unload — best-effort */ }
}
