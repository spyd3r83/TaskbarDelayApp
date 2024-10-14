# Taskbar Hider

A C# application that automatically hides the Windows taskbar(s) when not in use, supporting multiple monitors. The taskbar reappears when the mouse cursor hovers over it. This tool helps maximize screen real estate by allowing open windows to use the space previously occupied by the taskbar.

## Table of Contents

- [Features](#features)
- [How It Works](#how-it-works)
- [Prerequisites](#prerequisites)
- [Building the Project](#building-the-project)
- [Usage](#usage)
- [Customization](#customization)
- [License](#license)

## Features

- **Automatic Taskbar Hiding**: Hides all taskbars after a 5-second delay when the cursor is not over any taskbar.
- **Multiple Monitor Support**: Detects and manages taskbars on all connected monitors.
- **Auto-Hide Adjustment**: Adjusts the taskbar's auto-hide setting to allow open windows to use the full screen space.
- **System Tray Icon**: Runs silently in the background with a system tray icon for easy access.
- **Graceful Exit**: Provides an option to exit the application gracefully, restoring original taskbar settings.
- **Customizable**: Free to modify and adapt to your needs.

## How It Works

- **Startup**:
  - The application hides its console window to run in the background.
  - Enumerates all taskbars and saves their original auto-hide settings.
  - Disables auto-hide on all taskbars to ensure they are visible initially.
- **Main Loop**:
  - Continuously checks the mouse cursor position.
  - If the cursor is over any taskbar:
    - Cancels any pending hide actions.
    - Ensures all taskbars are visible.
  - If the cursor is not over any taskbar:
    - Starts a 5-second timer.
    - After 5 seconds, sets all taskbars to auto-hide mode.
- **Exiting**:
  - Right-click the system tray icon and select **Exit** to close the application.
  - Restores all taskbars to their original auto-hide settings upon exit.

## Prerequisites

- **Operating System**: Windows 10 or later
- **.NET SDK**: .NET 8.0 SDK installed
- **Permissions**: May require administrative privileges to modify taskbar settings

## Building the Project

1. **Clone the Repository**:

   ```bash
   git clone https://gitlab.com/yourusername/taskbar-hider.git
   cd taskbar-hider
   ```

2. **Open the Project**:

   - Use your preferred IDE (e.g., Visual Studio, Visual Studio Code).

3. **Restore Dependencies**:

   ```bash
   dotnet restore
   ```

4. **Build the Project**:

   ```bash
   dotnet build -c Release
   ```

5. **Publish as a Single Executable**:

   ```bash
   dotnet publish -c Release -r win-x64 --self-contained
   ```

   - The executable will be located in `bin\Release\net8.0-windows\win-x64\publish\`.

## Usage

1. **Run the Application**:

   - Double-click `TaskbarHider.exe` to start the application.
   - The application will run silently in the background, and a system tray icon will appear.

2. **Behavior**:

   - **Taskbar Hiding**:
     - Move your cursor away from all taskbars.
     - After 5 seconds, all taskbars will enter auto-hide mode.
     - Open windows will use the full screen space.
   - **Taskbar Showing**:
     - Move your cursor over any taskbar area.
     - All taskbars will reappear immediately.

3. **Exiting the Application**:

   - Right-click the system tray icon (looks like the application icon).
   - Select **Exit** from the context menu.
   - The application will close, and all taskbars will restore to their original auto-hide settings.

## Customization

Feel free to modify and adapt the application to suit your needs.

### Adjusting the Hide Delay

- To change the delay before taskbars are hidden (default is 5 seconds):

  1. Open `Program.cs`.
  2. Find the line:

     ```csharp
     hideTime = DateTime.Now.AddSeconds(5);
     ```

  3. Change `5` to your desired number of seconds.
  4. Rebuild and republish the application.

### Changing the System Tray Icon

- To use a custom icon:

  1. Add your `.ico` file to the project.
  2. Set its **Build Action** to `Content` and **Copy to Output Directory** to `Copy if newer`.
  3. Modify the `SetupTrayIcon` method in `Program.cs`:

     ```csharp
     notifyIcon.Icon = new Icon("youricon.ico");
     ```

### Modifying the Code

- The code is well-commented to help you understand how it works.
- You can adjust the behavior, add new features, or integrate it into a larger application.

## License

This project is provided **free for modification**. You are free to use, modify, and distribute the code as you see fit.

---

**Disclaimer**: Use this application at your own risk. Modifying system settings may have unintended consequences. Ensure you understand the code and test thoroughly before deploying in a production environment.