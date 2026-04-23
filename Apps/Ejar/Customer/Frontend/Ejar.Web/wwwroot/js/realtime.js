// Ejar realtime client — wraps @microsoft/signalr loaded from CDN.
// Called from EjarRealtimeService via JS interop.

let connection = null;
let dotnetRef = null;

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

export function start(hubUrl, token, ref) {
    if (connection) return;
    dotnetRef = ref;

    const builder = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
            accessTokenFactory: () => token,
            transport: signalR.HttpTransportType.WebSockets |
                       signalR.HttpTransportType.ServerSentEvents |
                       signalR.HttpTransportType.LongPolling
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    builder.on("ReceiveMessage", (data) => {
        playBeep();
        dotnetRef.invokeMethodAsync("OnMessage", JSON.stringify(data));
    });

    builder.on("ReceiveNotification", (data) => {
        playBeep();
        dotnetRef.invokeMethodAsync("OnNotification", JSON.stringify(data));
    });

    builder.onreconnected(() => {
        dotnetRef.invokeMethodAsync("OnReconnected");
    });

    builder.start()
        .then(() => dotnetRef.invokeMethodAsync("OnConnected"))
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
