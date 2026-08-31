# Switch 手把體感對戰中介程式

這是一個用 C# 建立的 Switch 手把體感轉鍵盤中介程式範例。

## 主要功能

- 讀取 Nintendo Switch Joy-Con / Pro Controller HID 裝置
- 啟用 IMU（加速度 / 陀螺儀）
- 解析簡單體感動作
- 將動作對應為按鍵模擬
- 以 Windows 系統列常駐形式運行
- 使用顏色圖示與右鍵選單顯示控制器連線狀態
- 右鍵選單會顯示左/右搖桿連線狀態，不再使用氣球提示
- 右鍵選單提供「體感轉按鍵」勾選項，可即時開關功能，無需重新編譯

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

	- 預設「體感轉按鍵」為關閉狀態，此時只會在主控台輸出 IMU 數值供除錯。
	- 確認體感方向與閾值後，於系統列右鍵選單勾選「體感轉按鍵」即可啟用鍵盤按鍵輸出。
	- 加入命令列參數 `single` 可切換為單人模式（例如 `dotnet run -- single`），預設為雙人模式的左搖桿玩家。

4. 程式會最小化到系統列，右鍵點選圖示可查看連線狀態、開關體感轉按鍵與結束選項

## 專案檔案

- `SwitchMotionBridge.csproj`：.NET 8.0 `net8.0-windows` WinForms 專案，使用 `HidSharp`。
- `Program.cs`：程式進入點，啟動系統列應用程式。
- `AppConfig.cs`：設定常數（廠商/產品 ID、動作閾值）與命令列模式判斷。
- `PlayerMode.cs` / `ConnectionState.cs`：玩家模式與控制器連線狀態列舉。
- `ControllerWorker.cs`：背景執行緒，負責偵測控制器、讀取 HID 報告並啟用 IMU。
- `MotionParser.cs`：解析 HID 報告中的加速度計與陀螺儀數值。
- `MotionKeyMapper.cs`：將體感數值轉換為方向鍵按住與跳躍/攻擊按鍵。
- `KeyboardSender.cs`：透過 Windows `SendInput` API 模擬鍵盤輸入。
- `TrayIconManager.cs`：系統列圖示、狀態選單與「體感轉按鍵」勾選項。
- `TrayApplicationContext.cs`：串接系統列 UI 與控制器偵測執行緒的生命週期。

## 說明

程式範例中，依玩家模式將體感動作對應到不同按鍵：

| 動作 | 單人模式 (`single`) | 雙人模式左搖桿玩家（預設） |
| --- | --- | --- |
| 向右移動 | → | G |
| 向左移動 | ← | D |
| 下蹲 | ↓ | V |
| 跳躍 | ↑ | R |
| 攻擊/揮擊 | Enter | Z |

程式會辨識 Joy-Con L / R 以及 Pro Controller，並在系統列右鍵選單中顯示左/右搖桿是否已連線。

> 注意：這個範例提供觀念與架構，IMU 資料位置與報文格式可能需要根據實際手把而微調。若要直接切換成虛擬 XInput，請改用 `ViGEm` 建立虛擬手把。

