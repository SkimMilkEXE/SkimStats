# Third-party notices

SkimStats bundles the following third-party software.

## PresentMon 2.6.0

Used to measure FPS. Bundled unmodified inside `SkimStats.exe` and copied to `%LocalAppData%\SkimStats\PresentMon.exe` when FPS is turned on.
Source: https://github.com/GameTechDev/PresentMon

```
Copyright (C) 2017-2024 Intel Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom
the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included
in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL
THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES
OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
OR OTHER DEALINGS IN THE SOFTWARE.
```

## LibreHardwareMonitorLib 0.9.6 and its dependencies

Used to read the CPU temperature (only when that setting is on). Bundled unmodified.
The CPU sensor is read through the separate **PawnIO** driver (https://pawnio.eu/),
which is not bundled with SkimStats and must be installed by the user.

| Package | License | Source |
|---|---|---|
| LibreHardwareMonitorLib 0.9.6 | MPL-2.0 | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| DiskInfoToolkit 1.1.2 | MPL-2.0 | https://github.com/Blacktempel/DiskInfoToolkit |
| RAMSPDToolkit-NDD 1.4.2 | MPL-2.0 | https://github.com/Blacktempel/RAMSPDToolkit |
| HidSharp 2.6.4 | Apache-2.0 | https://software.seekye.com/hidsharp |
| Mono.Posix.NETStandard 1.0.0 | MIT | https://github.com/mono/mono |

The MPL-2.0 licensed source code for these components is available at the links above.
Full license texts: MPL-2.0 https://www.mozilla.org/MPL/2.0/ ·
Apache-2.0 https://www.apache.org/licenses/LICENSE-2.0 · MIT https://opensource.org/license/mit
