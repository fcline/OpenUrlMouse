# OpenUrlHotkey

[Русский](#русский) | [English](#english)

---

## Русский

Windows-приложение для открытия заданной веб-страницы при нажатии настраиваемой комбинации клавиш. Работает в системном трее и потребляет минимальное количество ресурсов (~8–12 МБ ОЗУ).

### Основные возможности

- **Фоновый режим**: Работает исключительно из системного трея (notification area).
- **Настраиваемые горячие клавиши**: Поддерживается любая комбинация клавиш (по умолчанию `RightAlt + RightControl`), включая запись через интерактивное окно ввода или выбор готовых пресетов.
- **Смена URL**: Изменение целевого веб-сайта прямо из меню трея.
- **Поддержка двух языков (RU / EN)**: Автоматическое определение языка интерфейса Windows с возможностью ручного переключения в трее.
- **Автозапуск**: Включение/выключение автозапуска вместе с Windows через реестр.
- **Низкое потребление ресурсов**: Реализовано с использованием вызовов Win32 API и `ApplicationContext` на .NET Framework 4.8.

### Запуск и использование

1. Запустите файл `OpenUrlHotkey.exe`.
2. В системном трее появится иконка приложения.
3. По нажатию настроенной комбинации клавиш целевой URL откроется в браузере по умолчанию.
4. Нажмите правой кнопкой мыши по иконке в трее для доступа к настройкам:
   - **Открыть сайт** — откроет текущий веб-сайт.
   - **Изменить URL...** — откроет диалог ввода ссылки.
   - **Записать новый хоткей...** — откроет окно записи комбинации клавиш.
   - **Быстрые варианты** — меню выбора стандартных комбинаций.
   - **Язык / Language** — переключение между русским и английским языками.
   - **Автозапуск с Windows** — управление автозагрузкой.
   - **Выход** — завершение работы программы.

### Сборка из исходного кода

Для сборки проекта откройте PowerShell или командную строку в директории проекта и выполните:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll /out:OpenUrlHotkey.exe Program.cs
```

Конфигурационные данные сохраняются в `%APPDATA%\OpenUrlHotkey\config.json`.

---

## English

A Windows background system tray application that opens a specified URL upon pressing a customizable global hotkey combination. Operates with minimal memory footprint (~8–12 MB RAM).

### Key Features

- **System Tray Operation**: Runs in the background without persistent open windows.
- **Customizable Hotkeys**: Supports arbitrary global hotkey combinations (default: `RightAlt + RightControl`), recorder dialog, and preset selections.
- **Configurable URL**: Update the target URL at any time via the tray menu.
- **Bilingual Interface (EN / RU)**: Automatically detects Windows OS UI language with manual language switching options in the tray menu.
- **Startup Integration**: Optional Windows autorun toggled via HKCU registry.
- **Low Resource Usage**: Built using Win32 API calls and .NET Framework `ApplicationContext`.

### Usage

1. Launch `OpenUrlHotkey.exe`.
2. The application icon will appear in the system tray.
3. Press the configured hotkey to open the target website in the default browser.
4. Right-click the tray icon to open the context menu:
   - **Open Website**: Opens the target URL immediately.
   - **Change Website URL...**: Opens a prompt to change the target URL.
   - **Record New Hotkey...**: Opens a key recorder dialog.
   - **Quick Presets**: Select from common hotkey combinations.
   - **Language / Язык**: Switch interface language.
   - **Run at Windows Startup**: Toggle startup behavior.
   - **Exit**: Close the application.

### Build Instructions

To compile the project from source using the built-in Windows C# compiler:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll /out:OpenUrlHotkey.exe Program.cs
```

Settings are stored in `%APPDATA%\OpenUrlHotkey\config.json`.
