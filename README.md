# HomeRecall

![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet)
![MudBlazor](https://img.shields.io/badge/UI-MudBlazor-7e6fff?style=flat-square&logo=blazor)
![Home Assistant](https://img.shields.io/badge/Home%20Assistant-Addon-41bdf5?style=flat-square&logo=home-assistant)
![Docker](https://img.shields.io/badge/Container-Docker-2496ed?style=flat-square&logo=docker)
![AI Assisted](https://img.shields.io/badge/🤖%20AI-Co--Authored-success?style=flat-square)

> **Development Status**: This project is in an early development phase.

**HomeRecall** is a centralized backup solution for Smart Home IoT devices. It is engineered to run natively as a **Home Assistant Add-on** (supporting Ingress and auto-theming) but operates equally well as a standalone container or .NET application.

Its primary goal is to ensure configuration persistence for a wide range of DIY and commercial IoT devices without reliance on cloud services.

---

## Key Features

### 🔌 Device Support
HomeRecall supports configuration backup for a variety of popular firmware and devices:
*   **Tasmota:** Backs up the main configuration (`Config.dmp`) and automatically includes Berry scripts (`.be` files) from the UFS for firmware 14.6.0 or newer.
*   **Shelly:** Backs up the main configuration (`settings.json`/`config.json`) and additionally downloads all user scripts (`.js` files) for Gen 2/3 devices.
*   **WLED:** Backs up the primary configuration (`cfg.json`) and all user presets (`presets.json`).
*   **Awtrix Light:** Recursively backs up all files and folders stored on the device's internal flash storage.
*   **openHASP:** Backs up all files stored on the root filesystem (e.g., UI `.jsonl` configurations, fonts).
*   **OpenDTU** (untested alpha): Backs up the configuration (`config.json`).
*   **AI-on-the-Edge** (untested alpha): Backs up the configuration (`config.ini`) and reference frames (`ref0.jpg`, `ref1.jpg`, `reference.jpg`).
*   **AhoyDTU** (untested alpha)


### 💾 Intelligent Backup System
*   **Versioning & History:** Keeps a history of configuration changes.
*   **Deduplication:** Uses content-based hashing to store only unique backups, saving storage space while maintaining a full history.
*   **Visual Diff:** The UI highlights consecutive identical backups to easily identify when a configuration actually changed.
*   **Mass Operations:** Trigger backups for individual devices or the entire network with a single action.

### 🔎 Network Discovery
*   **Scanner:** Integrated multi-threaded network scanner to find devices within IP ranges.
*   **Auto-Detection:** Automatically identifies device types, firmware versions, and hardware models (e.g., distinguishing a "Sonoff Basic" from a generic Tasmota device).
*   **Live Feedback:** Real-time visibility of found devices during the scan process.
*   **Multi-Interface Support:** Discovers and tracks devices with multiple network interfaces (e.g. Wi-Fi and Ethernet) and intelligently switches interfaces during backup if one fails.
*   **MQTT Auto-Discovery:** Automatically detects and adds devices announcing themselves on your MQTT broker (supports Tasmota for now).

### 🛠 Integration & Deployment
*   **Home Assistant:** First-class citizen support via Add-on. Supports **Ingress** for seamless UI integration 
*   **Portable:** Runs on any platform supporting Docker or .NET 10 (Windows, Linux, macOS, Raspberry Pi).
*   **Localization:** Interface available in English and German.
*   **Configurable Logging:** Advanced log tracking capabilities configured seamlessly through Home Assistant Add-on options.

---

## Technical Architecture

This project utilizes a modern technology stack to ensure performance and maintainability:

*   **Framework:** .NET 10 (LTS)
*   **UI:** Blazor Server with MudBlazor (Material Design)
*   **Data:** SQLite with Entity Framework Core
*   **Runtime:** Docker (Alpine Linux base)

## Installation

### Home Assistant Add-on
1.  Add this repository URL to your **Home Assistant Add-on Store**.
2.  Install **HomeRecall**.
3.  Start the Add-on and click "Open Web UI".

### Docker (Standalone)
Ensure the data directories exist and are accessible:

```bash
mkdir -p data backups
```

Run the container using the official multi-arch image from GitHub Container Registry:

```bash
docker run -d \
  --name homerecall \
  -p 5000:8080 \
  -v $(pwd)/data:/config \
  -v $(pwd)/backups:/backup \
  ghcr.io/dasdandre/homerecall:latest
```

*Automatically pulls the correct image for your architecture (amd64, arm64, arm/v7).*

### Local Development (Docker)

To build and run the container locally from source:

```bash
# Build the image locally
docker build -t homerecall ./homerecall

# Run the container (mounting data volume)
docker run -d -p 5000:8080 -v $(pwd)/data:/config -v $(pwd)/backups:/backup homerecall
```

### Local Development (.NET)
Requires .NET 10 SDK.

```bash
cd homerecall
dotnet watch run
```

## Project Background

**HomeRecall** represents a modern approach to software engineering. The entire codebase—from the backend architecture to the UI components—was developed through an iterative co-authoring process between a human developer and an AI Agent. This methodology emphasizes rapid prototyping, clean code standards, and robust architectural decisions.

## License

Distributed under the [MIT License](LICENSE).
