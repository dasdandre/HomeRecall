# 🏠 HomeRecall

![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet)
![MudBlazor](https://img.shields.io/badge/UI-MudBlazor-7e6fff?style=flat-square&logo=blazor)
![Home Assistant](https://img.shields.io/badge/Home%20Assistant-Addon-41bdf5?style=flat-square&logo=home-assistant)
![Docker](https://img.shields.io/badge/Container-Docker-2496ed?style=flat-square&logo=docker)
![AI Assisted](https://img.shields.io/badge/🤖%20AI-Co--Authored-success?style=flat-square)

**HomeRecall** is a modern, lightweight backup solution for your IoT devices, designed to run seamlessly as a **Home Assistant Add-on**.

Never lose your Tasmota, WLED, or Shelly configurations again.

---

## ✨ AI-Powered Development

**This project is a showcase of modern software engineering with Artificial Intelligence.**

The entire codebase—from the backend architecture in .NET 10 to the responsive MudBlazor UI and the complex Home Assistant Ingress integration—was developed through an iterative dialogue between a human developer and an AI Agent. It demonstrates how AI can accelerate development, solve complex infrastructure challenges (like reverse proxy handling), and deliver production-ready code.

---

## 🚀 Features

*   **📱 Device Management:** Easily manage your IoT devices (Tasmota, WLED, Shelly) in a clean list view.
*   **💾 One-Click Backups:** Create backups of your device configurations instantly.
*   **🔄 Mass Backup:** Backup all your devices at once with a single click.
*   **🎨 Seamless Integration:** 
    *   Runs directly within Home Assistant via **Ingress**.
    *   **Auto-Theming:** Automatically syncs with your Home Assistant theme (Light/Dark mode and colors).
*   **📦 History & Versioning:** Keep multiple versions of backups for every device.
*   **🔒 Local Storage:** Your data stays on your drive. No cloud required.

## 🖼️ Screenshots

*(Add your screenshots here, e.g., `![Dashboard](docs/dashboard.png)`)*

## 🛠️ Tech Stack

*   **Framework:** [.NET 10](https://dotnet.microsoft.com/) (Stable/LTS)
*   **UI Component Library:** [MudBlazor](https://mudblazor.com/) (v8)
*   **Architecture:** Blazor Server (Interactive Server Side Rendering)
*   **Database:** SQLite with Entity Framework Core
*   **Containerization:** Docker (Alpine Linux base)

## 📦 Installation

### As Home Assistant Add-on

1.  Add this repository to your Home Assistant Add-on Store.
2.  Install **HomeRecall**.
3.  Start the Add-on and click "Open Web UI".

### Local Development (Docker)

```bash
# Build the image
docker build -t homerecall .

# Run the container (mounting data volume)
docker run -d -p 5000:8080 -v $(pwd)/data:/config -v $(pwd)/backups:/backup homerecall
```

### Local Development (.NET)

Prerequisites: .NET 10 SDK

```bash
cd homerecall
dotnet watch run
```
*Note: Local development runs in a simulated environment using `launchSettings.json` to mock Home Assistant paths.*

## 🤝 Contributing

Contributions are welcome! Whether you are a human or an AI, feel free to open a pull request.

## 📄 License

[MIT](LICENSE)
