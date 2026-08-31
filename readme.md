# Switch 手把體感對戰中介程式

這是一個用 C# 建立的 Switch 手把體感轉鍵盤中介程式範例。

## 主要功能

- 讀取 Nintendo Switch Joy-Con / Pro Controller HID 裝置
- 啟用 IMU（加速度 / 陀螺儀）
- 解析簡單體感動作
- 將動作對應為按鍵模擬（A/D、Space、J）
- 以 Windows 系統列常駐形式運行
- 使用顏色圖示與右鍵選單顯示控制器連線狀態
- 右鍵選單會顯示左/右搖桿連線狀態，不再使用氣球提示

## 建置與執行

1. 開啟命令列，切換到本專案目錄：

```powershell
dotnet build
dotnet run
```

2. 連接 Switch 控制器

- `VendorID = 0x057E`
- 支援 `ProductID` 包含 Joy-Con L / R、Pro Controller

3. 讓程式讀到 Controller 並開始輸出按鍵

	- `Program.cs` 的 `MotionKeyMappingEnabled` 預設為 `false`，此時只會輸出 IMU 數值供除錯。
	- 確認體感方向與閾值後，改為 `true` 即可啟用鍵盤按鍵輸出。

4. 程式會最小化到系統列，右鍵點選圖示可查看連線狀態與結束選項

## 專案檔案

- `SwitchMotionBridge.csproj`：.NET 8.0 `net8.0-windows` WinForms 專案，使用 `HidSharp`。
- `Program.cs`：HID 裝置偵測、IMU 啟動、體感解析、鍵盤模擬與系統列 UI。

## 說明

程式範例中，簡單地把：

- 左右傾斜映射為左右移動（A / D）
- 向上抬手或向前加速映射為跳躍（Space）
- 快速橫向 / 轉動映射為擊球（J）

程式會辨識 Joy-Con L / R 以及 Pro Controller，並在系統列右鍵選單中顯示左/右搖桿是否已連線。

> 注意：這個範例提供觀念與架構，IMU 資料位置與報文格式可能需要根據實際手把而微調。若要直接切換成虛擬 XInput，請改用 `ViGEm` 建立虛擬手把。
