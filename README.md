# 🎮 Idle Master Game Picker

**Idle Master Game Picker** is a Windows tool that fetches your Steam games and lets you pick which ones to idle.  
It generates ready-to-use scripts (`games.ps1`, `start.bat`, and `selected_games.csv`)  
for use with **[Idle Master Extended](https://github.com/JonasNilson/idle_master_extended)** or **steam-idle.exe**.

---
<img width="1910" height="1033" alt="image" src="https://github.com/user-attachments/assets/a7434bea-2116-4ea4-9380-a6864382622f" />


## 📥 Download

👉 **Latest release:** [https://github.com/Grypto-kali/idle-master-game-picker/releases](https://github.com/Grypto-kali/idle-master-game-picker/releases)

Just download the `.zip`, extract it anywhere, and run `Idle Master Game Picker.exe`.

---

## ⚙️ How to Use

1. **Download Idle Master Extended**  
   → [https://github.com/JonasNilson/idle_master_extended](https://github.com/JonasNilson/idle_master_extended)  
   Extract or install it — you’ll need its `steam-idle.exe`.

2. **Get your Steam Web API key:**  
   → [https://steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)  
   (Login and copy the key shown after registering any domain name.)

3. **Find your SteamID64:**  
   - Go to your Steam profile → right-click → *Copy Page URL*  
   - Examples:  
     - `https://steamcommunity.com/id/YourName` → vanity name  
     - `https://steamcommunity.com/profiles/7656119xxxxxxxxxx` → SteamID64  
   - The app automatically resolves vanity names to SteamID64.

4. **Enter your API key + Steam ID / URL → Click “Fetch games”**

5. **Select the games you want → Export**

6. **Choose the same folder where `steam-idle.exe` is located**  
   The app outputs:
   - `games.ps1`  
   - `start.bat`  
   - `selected_games.csv`

7. **Run `start.bat`**  
   It opens PowerShell, launches `steam-idle.exe` for your selected games, and idles them automatically.

---

## 📦 What It Generates

| File | Purpose |
|------|----------|
| `games.ps1` | PowerShell script that runs `steam-idle.exe` in batches |
| `start.bat` | Simple launcher for the PowerShell script |
| `selected_games.csv` | List of selected games (appid,name) |

> ⚠️ These files must be in the same folder as `steam-idle.exe`.

---

## 🧾 Requirements

- Windows 10/11  
- .NET 8.0 or newer  
- Steam Web API key  
- [Idle Master Extended](https://github.com/JonasNilson/idle_master_extended)  
