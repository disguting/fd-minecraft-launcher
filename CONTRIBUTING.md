# Contributing to the Minecraft Launcher

First off, thank you for considering contributing to this project! This launcher is built with C# and WPF, and we welcome any help—from fixing bugs to adding support for new mod loaders or skins.

## 🐛 Bug Reports

If you've encountered a bug, please open an Issue and provide as much detail as possible:
* **Description:** What happened?
* **Steps to Reproduce:** How can we see the bug ourselves?
* **Environment:** Your Windows version and .NET runtime version.
* **Logs:** Attach any log files generated in the `%appdata%` or application folder.

## 💡 Feature Requests

We are always looking for ways to improve the user experience.
* Before submitting, check the existing Issues to see if your idea has already been suggested.
* Clearly describe the benefit of the feature and how it should work.

## 🛠 Local Development Setup

To work on this project, you will need:
1. **IDE:** [Visual Studio 2022](https://visualstudio.microsoft.com/) (with .NET desktop development workload) or [JetBrains Rider](https://www.jetbrains.com/rider/).
2. **SDK:** .NET 8.0 SDK (or the version specified in the `.csproj` file).
3. **Installer Tools:** [Inno Setup 6](https://jrsoftware.org/isinfo.php) if you plan to modify the installation script.

### Getting Started:
1. Fork the repository.
2. Clone your fork: `git clone https://github.com/YOUR_USERNAME/launcher-repo.git`
3. Open the `.sln` file in your IDE.
4. Restore NuGet packages and build the solution.

## 🔄 Pull Request Process

1. **Branching:** Create a new branch for your feature or fix (e.g., `fix/auth-issue` or `feat/mod-support`).
2. **Standards:**
   - Follow PascalCase for method and class names.
   - Use camelCase for local variables.
   - Keep the UI logic in ViewModels (MVVM pattern); avoid writing heavy code in `MainWindow.xaml.cs`.
3. **Commits:** Write descriptive commit messages.
4. **Submission:** Open a PR against the `main` (or `dev`) branch. Describe your changes and link any related Issues.

## 📝 Code Style & Architecture

* **XAML:** Keep styles in `Resources` or separate `ResourceDictionaries` where possible to maintain a clean UI code.
* **Async/Await:** Use asynchronous programming for network calls (API requests) and file I/O to keep the UI responsive.
* **Documentation:** Add XML comments (`///`) to public methods if the logic is complex.

Thank you for helping us make a better Minecraft experience! 🚀
