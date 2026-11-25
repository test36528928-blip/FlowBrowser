"""Optional development launcher. The main browser is a C# WPF + WebView2 app."""
from pathlib import Path
import shutil
import subprocess
import sys

PROJECT = Path(__file__).resolve().with_name("FlowBrowser.csproj")

def main() -> int:
    if sys.platform != "win32":
        print("FlowBrowser runs on Windows.")
        return 1
    if shutil.which("dotnet") is None:
        print(".NET SDK not found. Use the included GitHub Actions workflow to build the EXE.")
        return 1
    if not PROJECT.exists():
        print(f"Project file not found: {PROJECT}")
        return 1
    return subprocess.run(["dotnet", "run", "--project", str(PROJECT)], cwd=PROJECT.parent, check=False).returncode

if __name__ == "__main__":
    raise SystemExit(main())
