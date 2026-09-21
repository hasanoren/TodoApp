namespace TodoApp.Api.Extensions;

public static class SignalRTestPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>TodoApp - SignalR Canlı Bildirim Test Paneli</title>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js"></script>
    <style>
        :root {
            --bg-primary: #0f172a;
            --bg-secondary: #1e293b;
            --bg-card: #334155;
            --text-primary: #f8fafc;
            --text-secondary: #94a3b8;
            --accent: #38bdf8;
            --accent-hover: #0284c7;
            --success: #22c55e;
            --danger: #ef4444;
            --warning: #f59e0b;
            --border: #475569;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; }
        body { background-color: var(--bg-primary); color: var(--text-primary); padding: 24px; min-height: 100vh; }
        .container { max-width: 900px; margin: 0 auto; }
        header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid var(--border); }
        h1 { font-size: 1.5rem; display: flex; align-items: center; gap: 10px; }
        .status-badge { display: inline-flex; align-items: center; gap: 8px; padding: 6px 14px; border-radius: 9999px; font-size: 0.875rem; font-weight: 600; }
        .status-disconnected { background-color: rgba(239, 68, 68, 0.2); color: var(--danger); border: 1px solid var(--danger); }
        .status-connecting { background-color: rgba(245, 158, 11, 0.2); color: var(--warning); border: 1px solid var(--warning); }
        .status-connected { background-color: rgba(34, 197, 94, 0.2); color: var(--success); border: 1px solid var(--success); }
        .dot { width: 10px; height: 10px; border-radius: 50%; background-color: currentColor; }
        
        .card { background-color: var(--bg-secondary); border-radius: 12px; padding: 20px; border: 1px solid var(--border); margin-bottom: 20px; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.2); }
        .card-title { font-size: 1.1rem; font-weight: 600; margin-bottom: 14px; color: var(--accent); }
        
        .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
        .form-group { margin-bottom: 14px; }
        label { display: block; font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 6px; }
        input { width: 100%; padding: 10px 14px; border-radius: 8px; border: 1px solid var(--border); background-color: var(--bg-card); color: var(--text-primary); font-size: 0.95rem; }
        input:focus { outline: none; border-color: var(--accent); }
        
        .btn-group { display: flex; gap: 10px; margin-top: 10px; }
        button { cursor: pointer; padding: 10px 18px; border-radius: 8px; border: none; font-weight: 600; font-size: 0.9rem; transition: background-color 0.2s; display: inline-flex; align-items: center; justify-content: center; gap: 8px; }
        .btn-primary { background-color: var(--accent); color: #0f172a; }
        .btn-primary:hover { background-color: var(--accent-hover); }
        .btn-danger { background-color: rgba(239, 68, 68, 0.2); color: var(--danger); border: 1px solid var(--danger); }
        .btn-danger:hover { background-color: var(--danger); color: white; }
        .btn-secondary { background-color: var(--bg-card); color: var(--text-primary); border: 1px solid var(--border); }
        .btn-secondary:hover { background-color: var(--border); }

        .feed-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
        .notifications-list { display: flex; flex-direction: column; gap: 12px; min-height: 200px; max-height: 480px; overflow-y: auto; padding-right: 4px; }
        .empty-state { text-align: center; padding: 48px 16px; color: var(--text-secondary); font-style: italic; }
        
        .notif-card { background-color: var(--bg-card); border-left: 4px solid var(--accent); border-radius: 8px; padding: 14px 16px; animation: slideDown 0.3s ease-out; }
        .notif-card.success { border-left-color: var(--success); }
        .notif-card.warning { border-left-color: var(--warning); }
        .notif-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px; }
        .notif-title { font-weight: 700; font-size: 0.98rem; color: #fff; }
        .notif-time { font-size: 0.75rem; color: var(--text-secondary); }
        .notif-message { font-size: 0.9rem; color: var(--text-primary); line-height: 1.4; }

        @keyframes slideDown {
            from { opacity: 0; transform: translateY(-12px); }
            to { opacity: 1; transform: translateY(0); }
        }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <h1>⚡ TodoApp SignalR Canlı Bildirim</h1>
            <div id="statusBadge" class="status-badge status-disconnected">
                <span class="dot"></span>
                <span id="statusText">Bağlantı Kesildi</span>
            </div>
        </header>

        <!-- Bağlantı / Giriş Bölümü -->
        <div class="card">
            <div class="card-title">1. Adım: Kullanıcı Olarak Bağlan</div>
            <div class="grid-2">
                <div class="form-group">
                    <label>E-Posta Adresi</label>
                    <input type="email" id="emailInput" value="tester2@todoapp.com">
                </div>
                <div class="form-group">
                    <label>Şifre</label>
                    <input type="password" id="passwordInput" value="SuperGizli123!">
                </div>
            </div>
            <div class="form-group" id="totpGroup" style="display: none;">
                <label style="color: var(--warning);">2FA Doğrulama Kodu (Google Authenticator 6 Haneli Kod)</label>
                <input type="text" id="totpInput" placeholder="123456" maxlength="6">
            </div>
            <div class="form-group">
                <label>Veya Doğrudan JWT Token Yapıştırın</label>
                <input type="text" id="tokenInput" placeholder="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...">
            </div>
            <div class="btn-group">
                <button id="btnLoginConnect" class="btn-primary" onclick="loginAndConnect()">🔐 Giriş Yap & Bağlan</button>
                <button id="btnTokenConnect" class="btn-secondary" onclick="connectWithToken()">🔗 Token ile Bağlan</button>
                <button id="btnDisconnect" class="btn-danger" style="display: none;" onclick="disconnect()">❌ Bağlantıyı Kes</button>
            </div>
            <div id="authMessage" style="margin-top: 10px; font-size: 0.85rem;"></div>
        </div>

        <!-- Bildirim Akışı -->
        <div class="card">
            <div class="feed-header">
                <div class="card-title" style="margin-bottom: 0;">2. Adım: Gelen Canlı Bildirimler</div>
                <button class="btn-secondary" style="padding: 4px 10px; font-size: 0.8rem;" onclick="clearNotifications()">Temizle</button>
            </div>
            <p style="font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 14px;">
                💡 <i>İpucu: Bağlantı kurulduktan sonra Swagger'dan başka bir kullanıcıyla görev paylaşın veya tamamlayın; bildirim anında burada belirecektir.</i>
            </p>
            <div id="notificationsList" class="notifications-list">
                <div class="empty-state" id="emptyState">Henüz bildirim alınmadı. Soket dinleniyor...</div>
            </div>
        </div>
    </div>

    <script>
        let connection = null;

        function playSound() {
            try {
                const ctx = new (window.AudioContext || window.webkitAudioContext)();
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
                osc.frequency.setValueAtTime(880, ctx.currentTime + 0.1); // A5
                gain.gain.setValueAtTime(0.1, ctx.currentTime);
                gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.3);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start();
                osc.stop(ctx.currentTime + 0.3);
            } catch(e) {}
        }

        function updateStatus(state, text) {
            const badge = document.getElementById('statusBadge');
            const statusText = document.getElementById('statusText');
            badge.className = 'status-badge status-' + state;
            statusText.innerText = text;
            
            const btnLogin = document.getElementById('btnLoginConnect');
            const btnToken = document.getElementById('btnTokenConnect');
            const btnDisc = document.getElementById('btnDisconnect');
            
            if (state === 'connected') {
                btnLogin.style.display = 'none';
                btnToken.style.display = 'none';
                btnDisc.style.display = 'inline-flex';
            } else {
                btnLogin.style.display = 'inline-flex';
                btnToken.style.display = 'inline-flex';
                btnDisc.style.display = 'none';
            }
        }

        async function loginAndConnect() {
            const email = document.getElementById('emailInput').value.trim();
            const password = document.getElementById('passwordInput').value;
            const totpInput = document.getElementById('totpInput');
            const msg = document.getElementById('authMessage');

            msg.style.color = 'var(--text-secondary)';
            msg.innerText = 'Giriş yapılıyor...';

            try {
                let res = await fetch('/api/Auth/login', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ email, password })
                });

                let data = await res.json();

                if (!res.ok) {
                    msg.style.color = 'var(--danger)';
                    msg.innerText = data.detail || 'Giriş başarısız oldu.';
                    return;
                }

                if (data.requiresTwoFactor) {
                    document.getElementById('totpGroup').style.display = 'block';
                    const totpCode = totpInput.value.trim();

                    if (!totpCode || totpCode.length !== 6) {
                        msg.style.color = 'var(--warning)';
                        msg.innerText = 'Lütfen Google Authenticator üzerindeki 6 haneli kodu girip tekrar butona basın.';
                        totpInput.focus();
                        return;
                    }

                    // 2FA login call
                    const res2fa = await fetch('/api/Auth/login-2fa', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ userId: data.userId, code: totpCode })
                    });
                    const data2fa = await res2fa.json();

                    if (!res2fa.ok) {
                        msg.style.color = 'var(--danger)';
                        msg.innerText = data2fa.detail || '2FA kodu geçersiz.';
                        return;
                    }

                    data = data2fa;
                }

                document.getElementById('tokenInput').value = data.token;
                msg.style.color = 'var(--success)';
                msg.innerText = 'Giriş başarılı! Hub bağlantısı başlatılıyor...';
                await startSignalR(data.token);

            } catch (err) {
                msg.style.color = 'var(--danger)';
                msg.innerText = 'Hata: ' + err.message;
            }
        }

        async function connectWithToken() {
            const token = document.getElementById('tokenInput').value.trim();
            const msg = document.getElementById('authMessage');
            if (!token) {
                msg.style.color = 'var(--warning)';
                msg.innerText = 'Lütfen geçerli bir JWT Token girin.';
                return;
            }
            msg.innerText = 'Hub bağlantısı başlatılıyor...';
            await startSignalR(token);
        }

        async function startSignalR(token) {
            if (connection) {
                await connection.stop();
            }

            updateStatus('connecting', 'Bağlanıyor...');

            connection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/todo', {
                    accessTokenFactory: () => token
                })
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.Information)
                .build();

            connection.on("ReceiveNotification", (title, message) => {
                playSound();
                addNotificationCard(title, message);
            });

            connection.onreconnecting(() => {
                updateStatus('connecting', 'Yeniden Bağlanıyor...');
            });

            connection.onreconnected(() => {
                updateStatus('connected', 'Bağlandı (Canlı)');
            });

            connection.onclose(() => {
                updateStatus('disconnected', 'Bağlantı Kesildi');
            });

            try {
                await connection.start();
                updateStatus('connected', 'Bağlandı (Canlı)');
                const msg = document.getElementById('authMessage');
                msg.style.color = 'var(--success)';
                msg.innerText = 'SignalR Hub bağlantısı aktif! Dinleniyor...';
            } catch (err) {
                updateStatus('disconnected', 'Bağlantı Başarısız');
                const msg = document.getElementById('authMessage');
                msg.style.color = 'var(--danger)';
                msg.innerText = 'Bağlantı hatası: ' + err.message;
            }
        }

        async function disconnect() {
            if (connection) {
                await connection.stop();
                updateStatus('disconnected', 'Bağlantı Kesildi');
                document.getElementById('authMessage').innerText = 'Bağlantı manuel olarak kapatıldı.';
            }
        }

        function addNotificationCard(title, message) {
            const emptyState = document.getElementById('emptyState');
            if (emptyState) emptyState.style.display = 'none';

            const list = document.getElementById('notificationsList');
            const now = new Date();
            const timeStr = now.toLocaleTimeString('tr-TR');

            const card = document.createElement('div');
            card.className = 'notif-card';
            if (title.toLowerCase().includes('tamamlandı')) {
                card.classList.add('success');
            } else if (title.toLowerCase().includes('güncellendi')) {
                card.classList.add('warning');
            }

            card.innerHTML = `
                <div class="notif-top">
                    <span class="notif-title">🔔 ${escapeHtml(title)}</span>
                    <span class="notif-time">${timeStr}</span>
                </div>
                <div class="notif-message">${escapeHtml(message)}</div>
            `;

            list.prepend(card);
        }

        function clearNotifications() {
            const list = document.getElementById('notificationsList');
            list.innerHTML = '<div class="empty-state" id="emptyState">Henüz bildirim alınmadı. Soket dinleniyor...</div>';
        }

        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }
    </script>
</body>
</html>
""";
}

