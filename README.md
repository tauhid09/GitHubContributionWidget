# 📈 GitHub Contribution Desktop Widget

A sleek, modern, and fully interactive desktop widget that brings your GitHub contribution graph directly to your Windows desktop. Built with a beautiful frosted glass-morphism aesthetic, it syncs live with GitHub's GraphQL API to provide rich, animated visualizations of your coding activity.

---

## ✨ Features

- **Four Dynamic Views:**
  - **Heatmap:** The classic GitHub contribution calendar, highly optimized to densely fill the widget space.
  - **Bar Chart (Overview):** Monthly aggregation of your active days and contributions.
  - **Line Chart (Trend):** See your coding momentum and growth over the year.
  - **Pie Chart (Breakdown):** A proportional split of your Commits, Pull Requests, Issues, and Code Reviews.
- **Dynamic Timeframes:** Instantly filter your data by **1 Year (1Y)**, **6 Months (6M)**, **3 Months (3M)**, or **1 Month (1M)** segments.
- **Historical Year Selector:** Dynamically queries GitHub to find the exact years you've been active, generating a custom dropdown to browse your history.
- **Live Avatar & Profile:** Automatically fetches and displays your live GitHub profile picture.
- **Premium Desktop Integration:** 
  - Custom glass-morphism transparent background.
  - Floats seamlessly on your desktop (ignores the standard Windows "Minimize All Application" gesture so it stays visible).
  - Draggable, pinnable, and resizable interface.
- **Secure Credentials:** Safely encrypts and stores your GitHub Token locally using the Windows DPAPI (Data Protection API). You only need to log in once!

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 or Windows 11
- .NET Framework 4.8.1
- Visual Studio (for building from source)

### 🔑 How to get your GitHub API Key (Personal Access Token)
To fetch your contribution data, the widget needs a GitHub Personal Access Token. Here is how to create one:

1. Go to [GitHub Settings](https://github.com/settings/profile).
2. Scroll down to the bottom of the left sidebar and click **Developer settings**.
3. Go to **Personal access tokens** > **Tokens (classic)**.
4. Click **Generate new token** -> **Generate new token (classic)**.
5. Give it a descriptive note (e.g., `Desktop Contribution Widget`).
6. Set an expiration date (or set to *No expiration* if you prefer).
7. **Select Scopes (Permissions):**
   - Check `read:user` *(Required to fetch your avatar and basic profile info)*.
   - Check `repo` *(Optional, but highly recommended if you want the widget to count your contributions inside your **private** repositories)*.
8. Click **Generate token** at the bottom of the page.
9. **Copy the generated token.** *(Note: GitHub will only show this token once, so copy it immediately!)*

### Installation & Usage
1. Clone or download this repository to your local machine.
2. Open `GitHubContributionWidget.csproj` in Visual Studio.
3. Build and Run the application.
4. On the first launch, a sleek login window will appear. Enter your **GitHub Username** and paste your **Personal Access Token**.
5. Hit login, and your live dashboard will instantly render on your desktop!

---

## 🎨 Widget Controls
- **Drag:** Click and hold anywhere on the empty space of the widget to move it around your screen.
- **Resize:** Use the bottom right corner grip to resize the widget. The Heatmap will automatically stretch to fully utilize the space.
- **Lock/Pin:** Click the pin icon in the top right to lock the widget in place so it cannot be accidentally moved.
- **Three-Dot Menu:** Access additional settings.
- **Close:** Click the 'X' to close the widget completely.

---

## 🛠️ Tech Stack
- **C# / WPF** (Windows Presentation Foundation)
- **GitHub GraphQL API** (for fetching high-performance, single-query contribution data)
- **Newtonsoft.Json / System.Linq** (for parsing nested JSON API responses)
- **System.Security.Cryptography** (for secure, hardware-bound credential storage)