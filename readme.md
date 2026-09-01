# Switch Motion Bridge

這是一個以 C# 撰寫的 Windows 系統匣應用程式，用於將 Nintendo Switch Joy-Con / Pro Controller 的 IMU（加速度計與陀螺儀）資料轉換成鍵盤輸入。它會在背景持續偵測控制器、啟用 IMU、解析體感資料，並依設定將動作映射到鍵盤按鍵。

## 主要功能

- 偵測 Nintendo Switch 控制器（Vendor ID: `0x057E`）
- 支援 Joy-Con L、Joy-Con R、Pro Controller 產品 ID
- 啟用控制器 IMU（加速度與陀螺儀）
- 解析左右移動、下蹲、跳躍、揮擊等動作
- 解析控制器實體按鈕（A/B/X/Y、L/R/ZL/ZR、方向鍵、搖桿按壓等）並可獨立對應鍵盤
- 使用 Windows `SendInput` API 模擬鍵盤輸入
- 以系統匣圖示方式常駐執行
- 可從選單分別切換「啟用體感」與「啟用按鍵」開關，預設皆為啟用
- 提供體感校正、按鍵設定與敏感度設定的編輯入口
- 支援 `single` 命令列參數切換單人模式

## 專案結構

- `Program.cs`：應用程式進入點，啟動系統匣主程式
- `AppConfig.cs`：定義廠商 ID、支援控制器產品 ID、動作閾值與啟動模式判斷
- `PlayerMode.cs`：玩家模式列舉（單人／雙人 1P / 2P）
- `ConnectionState.cs`：連線狀態列舉（未連線／單邊連線／雙邊連線）
- `ControllerWorker.cs`：背景監控與裝置讀取執行緒，處理 HID 讀取與 IMU 事件
- `MotionParser.cs`：解析加速度計、陀螺儀數值與實體按鈕狀態
- `MotionCalibrator.cs`：進行體感零點校正
- `MotionKeyMapper.cs`：將 IMU 資料轉成方向鍵 / 動作鍵
- `ButtonKeyMapper.cs`：將實體按鈕的按下/放開狀態同步為鍵盤按鍵（非體感）
- `KeyboardSender.cs`：鍵盤按壓與放開的實作
- `MotionSettings.cs`：讀取/建立 `motionsettings.json`
- `KeyBindings.cs`：讀取/建立 `keybindings.json`
- `TrayApplicationContext.cs`：系統匣 UI 與控制器工作者的生命週期控制
- `TrayIconManager.cs`：通知圖示、狀態顯示與右鍵選單
- `SwitchMotionBridge.csproj`：.NET 8.0 Windows Forms 專案設定，依賴 `HidSharp`

## 建置與執行

在 Windows 環境中執行：

```powershell
dotnet restore
dotnet build
dotnet run
```

若要啟動單人模式：

```powershell
dotnet run -- single
```

未帶參數時，預設為雙人模式中的「1P（左控制器）」；若有兩個控制器，則 2P 會對應右控制器。

## 控制器與設定

### 支援的控制器

- `VendorID = 0x057E`
- `ProductID = 0x2006, 0x2007, 0x2009, 0x2017`

### 內建動作對應

雙人模式中，1P 由左控制器操作，2P 由右控制器操作。

| 動作 | 單人模式 | 1P（左控制器） | 2P（右控制器） |
| --- | --- | --- | --- |
| 向右移動 | 方向鍵右 | `G` | `D` |
| 向左移動 | 方向鍵左 | `D` | `G` |
| 下蹲 | 方向鍵下 | `V` | `R` |
| 跳躍 | 方向鍵上 | `R` | `Z` |
| 揮擊 / 攻擊 | `Enter` | `Z` | `V` |

## 設定檔

專案會在執行時自動建立以下設定檔，並在輸出目錄中複製到執行檔旁：

- `motionsettings.json`：調整加速度與陀螺儀門檻值
- `keybindings.json`：調整每個動作對應的鍵位

這兩個檔案都允許直接編輯，修改後需重新啟動程式才會生效。

### `motionsettings.json` 內容重點

- `MoveThreshold`：左右移動的觸發門檻
- `MoveReleaseThreshold`：移動放開的回饋門檻
- `JumpThreshold`：跳躍觸發閾值
- `DownThreshold`：下蹲觸發閾值
- `HitThreshold`：揮擊觸發閾值
- `MotionCooldownMs`：跳躍 / 攻擊的冷卻時間

### `keybindings.json` 內容重點

- `SinglePlayer`：單人模式的按鍵綁定
- `LeftPlayer`：雙人模式中的 1P（左控制器）按鍵綁定
- 2P（右控制器）則依實際控制器配置對應另一組設定
- 每個模式下的 `Buttons` 物件可額外設定實體按鈕對鍵盤按鍵的對應（非體感），鍵名例如 `A`、`B`、`X`、`Y`、`L`、`R`、`ZL`、`ZR`、`Plus`、`Minus`、`Home`、`Capture`、`LStick`、`RStick`、`SL`、`SR`、`DPadUp`、`DPadDown`、`DPadLeft`、`DPadRight`，預設為空物件（不綁定任何按鈕）

按鍵名稱必須對應 `VirtualKeyShort` 列舉成員名稱。

## 系統匣功能

程式啟動後會縮到系統匣，右鍵點選圖示可以：

- 查看目前連線狀態
- 開啟 / 關閉「啟用體感」（IMU 動作轉鍵盤，預設開啟）
- 開啟 / 關閉「啟用按鍵」（實體按鈕轉鍵盤，預設開啟）
- 啟動體感校正
- 編輯按鍵設定
- 編輯體感參數設定
- 離開程式

## 注意事項

- 這個專案主要是用來匯出鍵盤輸入的概念驗證範例，不保證能直接適用於所有控制器型號。
- IMU 讀數與實際感測器方向可能因 Joy-Con 型號或藍芽/USB 連線方式而略有差異。
- 如需將動作映射到遊戲控制器模擬器，可能需搭配 `ViGEm` 等工具進一步轉成 XInput / 虛擬手把。

## 需求

- Windows 10 / 11
- .NET 8 SDK
- Nintendo Switch 控制器（Joy-Con / Pro Controller）
- `HidSharp` 套件

