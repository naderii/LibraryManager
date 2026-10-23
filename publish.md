# انتشار
در PowerShell، از مسیر پروژه اجرا کنید:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```
خروجی قابل توزیع در `bin/Release/net8.0-windows/win-x64/publish` قرار می‌گیرد. بسته‌بندی یک فایل setup.exe (برای نمونه با Inno Setup) به مرحلهٔ نهایی و پس از تکمیل قابلیت‌ها موکول شده است.
