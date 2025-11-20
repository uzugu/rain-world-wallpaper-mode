# Cloudflared SSH from Termux

Document the repeatable flow for remoting into the Windows desktop from an Android phone (Termux), then hopping into WSL.

## Prerequisites
- Windows box already running OpenSSH server (`sshd` service) and on-demand Cloudflare quick tunnel.
- `cloudflared` installed on Windows (MSI or `winget install Cloudflare.cloudflared`).
- Termux on Android with `cloudflared` and `openssh` packages installed (`pkg install tur-repo cloudflared openssh`).

## Windows: launch the quick tunnel
1. Open PowerShell.
2. Start the tunnel and keep the window open:
   ```powershell
   cloudflared tunnel --url ssh://localhost:22
   ```
3. Copy the generated hostname (`https://<random>.trycloudflare.com`). You will reuse it on the phone until you stop the tunnel.

## Windows: expose ComfyUI (port 8188)
1. Open another PowerShell window.
2. Launch a quick tunnel for the HTTP service:
   ```powershell
   cloudflared tunnel --url http://localhost:8188
   ```
3. Use the emitted `https://<random>.trycloudflare.com` address in a mobile browser to reach ComfyUI remotely. Leave the window open while you need remote access.
4. Each run generates a fresh hostname; bookmark the current one if you plan to reuse it during the same session.

## Termux: establish the Cloudflare TCP bridge
1. In Termux, start the local forwarder (replace the hostname):
   ```bash
   cloudflared access tcp --hostname <random>.trycloudflare.com --url localhost:2222
   ```
   Leave this running; it binds `localhost:2222` inside Termux to your Windows SSH endpoint.
2. Open a second Termux session and connect through SSH:
   ```bash
   ssh uzuik@localhost -p 2222
   ```
   - First-time connections will prompt to accept the host key.
   - Password or key auth works depending on how `sshd` is configured on Windows.

## Jump into WSL
Once you have the Windows shell, drop into Ubuntu:
```powershell
wsl -d Ubuntu-24.04
```

## Session shutdown
- Exit WSL (`exit`), then the Windows shell, then stop the Cloudflare tunnel (Ctrl+C in the PowerShell window) and the Termux forwarder (Ctrl+C).
- Re-run the same steps with the new trycloudflare hostname next time.

## Troubleshooting
- **“missing port in address” errors**: ensure the Windows command includes `ssh://localhost:22` and the Termux TCP bridge uses `--url localhost:2222`.
- **Authentication failures**: confirm the Windows account has the right password or authorized SSH key (`C:\Users\<you>\.ssh\authorized_keys`).
- **Connection refused**: verify `sshd` service is running (`Get-Service sshd`) before launching the tunnel.
- **ComfyUI not loading**: confirm the ComfyUI server is running locally on port `8188` and that the quick tunnel PowerShell window stays open.
