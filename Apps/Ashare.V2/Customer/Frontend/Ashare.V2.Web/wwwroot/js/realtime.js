// Ashare V2 — SignalR realtime module
// Loaded lazily via JS interop import; not bundled.

let _connection = null;

export async function start(hubUrl, token, dotnetRef) {
    if (_connection) return;

    _connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .build();

    _connection.on("ReceiveMessage", (json) => {
        playBeep(440, 0.15);
        dotnetRef.invokeMethodAsync("OnMessage", typeof json === "string" ? json : JSON.stringify(json));
    });

    // chat.message — sent on chat:conv:{id} group when the recipient has the
    // ChatRoom open. Routed to IChatClient via Ashare2RealtimeService.
    _connection.on("chat.message", (data) => {
        dotnetRef.invokeMethodAsync("OnChatMessage", typeof data === "string" ? data : JSON.stringify(data));
    });

    _connection.on("ReceiveNotification", (json) => {
        playBeep(520, 0.2);
        dotnetRef.invokeMethodAsync("OnNotification", typeof json === "string" ? json : JSON.stringify(json));
    });

    _connection.onreconnected(() => dotnetRef.invokeMethodAsync("OnReconnected"));

    try {
        await _connection.start();
        dotnetRef.invokeMethodAsync("OnConnected");
    } catch (err) {
        console.error("[Realtime] connection failed:", err);
        _connection = null;
    }
}

export async function stop() {
    if (_connection) {
        await _connection.stop();
        _connection = null;
    }
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

// Fire-and-forget HTTP POST that survives page unload. Used to call the
// backend chat-leave endpoint when the tab closes.
export function leaveChatBeacon(path, token) {
    try {
        if (token) {
            fetch(path, {
                method: 'POST', keepalive: true,
                headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },
                body: '{}'
            }).catch(() => {});
        } else {
            const blob = new Blob([JSON.stringify({})], { type: 'application/json' });
            navigator.sendBeacon(path, blob);
        }
    } catch (_) { /* unload — nothing more we can do */ }
}

function playBeep(frequency, duration) {
    try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.frequency.value = frequency;
        osc.type = "sine";
        gain.gain.setValueAtTime(0.3, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + duration);
        osc.start(ctx.currentTime);
        osc.stop(ctx.currentTime + duration);
    } catch (_) { /* audio blocked by browser policy — silently ignore */ }
}
