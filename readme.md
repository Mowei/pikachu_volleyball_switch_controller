# Switch 手把體感對戰中介程式

這是一個用 C# 建立的 Switch 手把體感轉鍵盤中介程式範例，適合用於《皮卡丘打排球》之類的雙人體感對戰遊戲。

## 主要功能

- 讀取 Nintendo Switch Joy-Con / Pro Controller HID 裝置
- 啟用 IMU（加速度 / 陀螺儀）
- 解析簡單體感動作
- 將動作對應為按鍵模擬（A/D、Space、J）

## 建置與執行

1. 開啟命令列，切換到本專案目錄：

```powershell
cd c:\Users\mowei\Desktop\GITHUB\switch_controller
dotnet restore
dotnet build
dotnet run
```

2. 連接 Switch 控制器

- `VendorID = 0x057E`
- 支援 `ProductID` 包含 Joy-Con L / R、Pro Controller

3. 讓程式讀到 Controller 並開始輸出按鍵

## 專案檔案

- `SwitchMotionBridge.csproj`：.NET 8.0 專案，使用 `HidSharp`。
- `Program.cs`：HID 裝置偵測、IMU 啟動、體感解析與 SendInput 按鍵模擬。

## 說明

程式範例中，簡單地把：

- 左右傾斜映射為左右移動（A / D）
- 向上抬手或向前加速映射為跳躍（Space）
- 快速橫向 / 轉動映射為擊球（J）

> 注意：這個範例提供觀念與架構，IMU 資料的位置可能會因報文格式而需要微調。若要直接切換成虛擬 XInput，請改用 `ViGEm` 建立虛擬手把。
