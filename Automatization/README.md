# Automatization

An automation tool for ProTanki built with WPF.

## Overview

Automatization is a versatile desktop application designed to help you automate repetitive tasks on ProTanki. It provides a simple interface to configure and trigger actions like mouse clicks at specific coordinates, all manageable through global hotkeys.

## Features

- **Auto-Clicker:** Configure and run an automatic clicker at specified screen coordinates.
- **Global Hotkeys:** Set up and manage global hotkeys to start and stop automation tasks from anywhere in the OS.
- **Coordinate Picker:** An easy-to-use tool to pick and save screen coordinates for your automation tasks.
- **Theming:** Switch between light and dark themes to suit your preference.
- **Settings:** A dedicated settings window to configure the application's behavior.
- **Log Viewer:** View logs of the application's activity.

## How to Use

1.  **Launch the application:** Start ``Automatization.exe``.
2.  **Configure Settings:** ``Open the settings window to set up your preferences, including hotkeys and clicker settings``.
3.  **Pick Coordinates:** ``Use the coordinate picker tool to select a point on your screen for the auto-clicker``.
4.  **Start/Stop:** ``Use the configured global hotkeys to start and stop the automation``.

## Building from Source

To build and run this project from source, you will need:

-   Visual Studio (with .NET desktop development workload)
-   Latest .NET Framework or check the `.csproj` file for the exact version that it runs on

1.  Clone the repository:
    ```sh
    git clone <repository-url>
    ```
2.  Open `Automatization.sln` in Visual Studio.
3.  Restore the NuGet packages.
4.  Build and run the project (F5).

## Technologies Used

-   **.NET / C#:** The core application logic is written in C#.
-   **WPF:** The user interface is built using Windows Presentation Foundation.
-   **XAML:** Used for defining the UI layout and themes.
