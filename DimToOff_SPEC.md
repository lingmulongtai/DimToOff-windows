# DimToOff 仕様書

## 1. 概要

**DimToOff** は、Windows ラップトップ向けの常駐ユーティリティアプリである。  
MacBook のように「画面輝度を最低まで下げると、PC はスリープせずに画面だけOFFになる」体験を Windows で再現する。

ユーザーが輝度キーやWindowsの輝度スライダーで画面輝度をしきい値以下まで下げると、アプリがそれを検知してディスプレイのみをOFFにする。  
その後、キーボード・マウス・トラックパッド操作があったら画面を復帰させ、消灯前に保存していた輝度へ自動で戻す。

## 2. 目的

- Windows ラップトップで、Mac の「輝度ゼロで画面だけOFF」に近い体験を実現する。
- PC本体はスリープさせず、処理・通信・音楽再生・ダウンロードなどは継続させる。
- OLED搭載ラップトップの焼き付き対策や、短時間離席時の画面保護に使えるようにする。
- 外部ツールを開かず、普段の輝度キー操作だけで自然に使えるようにする。

## 3. 対象環境

### 必須

- OS: Windows 10 / Windows 11
- 対象: 輝度制御に対応した Windows ラップトップ内蔵ディスプレイ
- 実装言語: C#
- 推奨フレームワーク: .NET 8
- UI: WinForms のタスクトレイ常駐アプリ

### 推奨理由

- WPF / WinUI よりも、トレイ常駐の小型ユーティリティは WinForms の方が実装が単純。
- .NET 8 で単一ファイル配布、自己完結ビルド、将来的なインストーラー化がしやすい。
- Windows API / WMI / 低レベル入力フックとの相性が良い。

## 4. 基本動作

### 4.1 通常フロー

1. アプリ起動後、タスクトレイに常駐する。
2. 現在の画面輝度を取得する。
3. 輝度変更イベントを監視する。
4. 輝度が設定されたしきい値以下になったら、一定時間デバウンスする。
5. デバウンス後も輝度がしきい値以下なら、現在の復帰用輝度を保存してから画面だけOFFにする。
6. 画面OFF状態中、キーボード・マウス・トラックパッドの操作を待つ。
7. 入力を検知したら、Windowsが画面を復帰させた直後に保存済み輝度へ戻す。
8. 一定時間のクールダウンを入れ、復帰直後の輝度変更イベントで再度OFFにならないようにする。

### 4.2 画面OFF条件

デフォルトでは以下の条件で画面OFFにする。

```text
brightness <= 1%
```

設定画面または設定ファイルで以下を変更できるようにする。

- OFFしきい値: 0%, 1%, 2%, 5%, 10%
- デフォルト値: 1%
- デバウンス時間: デフォルト 800ms
- 復帰後クールダウン: デフォルト 1500ms

### 4.3 輝度復帰条件

画面OFF後、以下のいずれかを検知したら輝度を復帰する。

- キーボード入力
- マウス移動
- マウスクリック
- トラックパッド操作
- 可能なら、ディスプレイの電源状態がONに戻った通知

復帰時は、最後に保存していた `lastUsableBrightness` に戻す。

## 5. 輝度保存ロジック

### 5.1 基本方針

単純に「OFF直前の輝度」を保存すると、最低輝度の 0% / 1% を保存してしまう。  
そのため、アプリは常に **最後に使っていた通常輝度** を保存する。

### 5.2 変数

```csharp
int offThreshold = 1;
int lastUsableBrightness = 40;
bool isDisplayOffByApp = false;
bool isRestoringBrightness = false;
DateTime lastOffTime;
DateTime lastRestoreTime;
```

### 5.3 保存ルール

- アプリ起動時、現在輝度が `offThreshold` より大きければ `lastUsableBrightness` に保存する。
- 輝度変更イベントを受け取ったとき、輝度が `offThreshold` より大きければ `lastUsableBrightness` を更新する。
- 輝度が `offThreshold` 以下になった場合は、`lastUsableBrightness` を更新しない。
- `lastUsableBrightness` が未設定、またはしきい値以下の場合は、復帰輝度として `defaultRestoreBrightness` を使う。
- `defaultRestoreBrightness` の初期値は 40%。設定で変更可能にする。

### 5.4 例

```text
現在輝度 65%
↓
ユーザーが輝度を下げる
↓
20% → lastUsableBrightness = 20
10% → lastUsableBrightness = 10
5%  → lastUsableBrightness = 5
1%  → しきい値以下なので保存しない
↓
画面OFF
↓
キーボード入力
↓
輝度を 5% に戻す
```

ただし、復帰輝度が低すぎて使いづらい場合があるため、以下のオプションも実装する。

```text
minimumRestoreBrightness = 20%
```

有効時は、復帰輝度が 20% 未満なら 20% に補正する。

## 6. 状態遷移

### 6.1 State

```csharp
enum AppState
{
    Idle,
    PendingDisplayOff,
    DisplayOffByApp,
    RestoringBrightness,
    Cooldown
}
```

### 6.2 状態説明

#### Idle

通常状態。輝度変更を監視する。

#### PendingDisplayOff

輝度がしきい値以下になり、デバウンスタイマー中の状態。  
この間に輝度がしきい値より上がった場合は `Idle` に戻る。

#### DisplayOffByApp

アプリが画面OFFコマンドを送信した状態。  
入力フックまたは電源状態通知で復帰を検知する。

#### RestoringBrightness

入力または画面ON通知を検知し、保存済み輝度へ戻している状態。

#### Cooldown

復帰直後の誤作動を避ける状態。  
この期間中は輝度がしきい値以下でも画面OFFしない。

## 7. Windows API / 技術要件

## 7.1 輝度変更イベント監視

WMI の `WmiMonitorBrightnessEvent` を使う。  
名前空間は `root\wmi`。

```sql
SELECT * FROM WmiMonitorBrightnessEvent
```

このイベントには `Brightness` プロパティがあり、モニター輝度の変化を表す。

C# イメージ:

```csharp
var scope = new ManagementScope("root\\wmi");
var query = new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent");
var watcher = new ManagementEventWatcher(scope, query);

watcher.EventArrived += OnBrightnessChanged;
watcher.Start();
```

## 7.2 現在輝度の取得

WMI の `WmiMonitorBrightness` を使う。

```sql
SELECT * FROM WmiMonitorBrightness WHERE Active = TRUE
```

取得した `CurrentBrightness` を現在輝度として扱う。

## 7.3 輝度の設定

WMI の `WmiMonitorBrightnessMethods.WmiSetBrightness` を使う。  
`Brightness` はパーセント値。

C# イメージ:

```csharp
foreach (ManagementObject mObj in searcher.Get())
{
    mObj.InvokeMethod("WmiSetBrightness", new object[] { 0, brightness });
}
```

## 7.4 画面OFF

`user32.dll` の `SendMessage` を使い、`WM_SYSCOMMAND` / `SC_MONITORPOWER` を送信する。

```csharp
[DllImport("user32.dll")]
private static extern IntPtr SendMessage(
    IntPtr hWnd,
    int Msg,
    IntPtr wParam,
    IntPtr lParam
);

private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
private const int WM_SYSCOMMAND = 0x0112;
private const int SC_MONITORPOWER = 0xF170;

public static void TurnOffDisplay()
{
    SendMessage(HWND_BROADCAST, WM_SYSCOMMAND,
        new IntPtr(SC_MONITORPOWER), new IntPtr(2));
}
```

`lParam = 2` はディスプレイをOFFにする指定。  
必要な場合のみ `lParam = -1` で画面ONを試すが、基本的にはユーザー入力でWindowsが画面を復帰する前提にする。

## 7.5 入力検知

画面OFF後の復帰検知には、低レベル入力フックを使う。

- `SetWindowsHookEx`
- `WH_KEYBOARD_LL`
- `WH_MOUSE_LL`

要件:

- キーボード入力を検知する。
- マウス移動・クリック・ホイールを検知する。
- トラックパッドは通常マウスイベントとして検知される想定。
- 入力フックはアプリ終了時に必ず解除する。
- 画面OFF中だけ入力フックを有効化する設計でもよい。
- 実装が簡単なら常時有効でもよいが、不要な処理は最小限にする。

## 7.6 電源状態通知

可能なら `RegisterPowerSettingNotification` も使い、ディスプレイ状態変化を補助的に検知する。

候補:

- `GUID_MONITOR_POWER_ON`
- `GUID_CONSOLE_DISPLAY_STATE`

これにより、入力フックで取りこぼした場合でも画面復帰後に輝度を戻せる可能性が上がる。

## 7.7 注意: GetDevicePowerState は使わない

`GetDevicePowerState` はディスプレイデバイスの電源状態確認には使えないため、画面ON/OFF判定の主手段にしない。

## 8. UI仕様

### 8.1 タスクトレイ

アプリ起動後、通知領域に常駐する。

トレイアイコン右クリックメニュー:

```text
DimToOff
────────────
Enabled: On/Off
Turn Off Display Now
Restore Brightness Now
Settings...
Start with Windows: On/Off
About
Exit
```

### 8.2 設定画面

最初のMVPでは設定画面なしでもよい。  
ただし、最終版では以下を設定可能にする。

- Enable DimToOff
- Off threshold: 0 / 1 / 2 / 5 / 10%
- Debounce duration: 300〜2000ms
- Restore brightness mode:
  - Last usable brightness
  - Fixed brightness
  - Max of last usable and minimum brightness
- Minimum restore brightness: 0〜100%
- Default restore brightness: 0〜100%
- Cooldown after restore: 500〜5000ms
- Start with Windows
- Show notification when display turns off
- Disable while fullscreen app is active
- Disable while on external monitor

### 8.3 通知

初期版では通知は任意。  
通知を出す場合:

- 画面OFF直前の通知は不要。画面が消えるため意味が薄い。
- 起動時に「DimToOff is running」程度の通知を出すのは可。
- エラー時のみ通知するのが望ましい。

## 9. 設定保存

設定はJSONで保存する。

保存場所:

```text
%APPDATA%\DimToOff\settings.json
```

設定例:

```json
{
  "enabled": true,
  "offThreshold": 1,
  "debounceMs": 800,
  "cooldownMs": 1500,
  "restoreMode": "LastUsableWithMinimum",
  "minimumRestoreBrightness": 20,
  "defaultRestoreBrightness": 40,
  "startWithWindows": false,
  "showErrorNotifications": true,
  "disableWhileFullscreen": false,
  "disableWhenExternalMonitorConnected": false
}
```

## 10. 起動時自動実行

MVPでは未実装でも可。  
実装する場合は以下のどちらか。

### 方法A: レジストリ Run キー

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

### 方法B: スタートアップフォルダにショートカット作成

ユーザーが理解しやすく、削除もしやすい。  
まずは方法Bを推奨。

## 11. エッジケース

### 11.1 輝度イベントが取得できないPC

一部メーカーや一部外部モニターでは WMI の輝度イベントが取れない場合がある。

対応:

- 起動時に対応チェックを行う。
- イベントが取れない場合は、数秒ごとに現在輝度をポーリングする fallback を用意する。
- ポーリング間隔は 500ms〜2000ms。デフォルトは 1000ms。
- エラー通知または設定画面に「WMI event unavailable, polling mode active」と表示する。

### 11.2 復帰直後に再度画面OFFしてしまう

輝度復帰前に、まだ輝度がしきい値以下として検出されることがある。

対応:

- `isRestoringBrightness = true` 中は画面OFFしない。
- 復帰後 `cooldownMs` の間は画面OFFしない。
- アプリ自身が `WmiSetBrightness` で輝度を変更したイベントは無視する。

### 11.3 画面OFF直後に即復帰する

タッチパッドやマウスが微小入力を出す場合がある。

対応:

- 画面OFF直後 `ignoreInputMs` を設ける。
- デフォルトは 300ms。
- この間の入力では輝度復帰処理を実行しない。

### 11.4 外部モニター接続時

`SC_MONITORPOWER` は複数ディスプレイ全体に影響する可能性がある。

対応:

- MVPでは外部モニター個別制御は対象外。
- 外部モニター接続時は設定で無効化できるようにする。
- 将来的には DDC/CI による外部モニター制御を検討してもよい。

### 11.5 フルスクリーンゲーム中

ゲーム中に輝度変更や入力フックが悪影響になる可能性がある。

対応:

- MVPでは未対応でよい。
- 将来的に foreground window のフルスクリーン判定を追加し、無効化できるようにする。

## 12. ログ

MVPではファイルログを実装する。

保存場所:

```text
%LOCALAPPDATA%\DimToOff\logs\dimtooff.log
```

ログ内容:

- アプリ起動 / 終了
- 現在輝度取得結果
- 輝度変更イベント
- 画面OFF実行
- 入力検知
- 輝度復帰実行
- WMIエラー
- hook登録/解除エラー

個人情報やキー入力内容は保存しない。  
低レベルキーボードフックを使っても、押されたキーの内容をログに保存してはいけない。

## 13. セキュリティ / プライバシー

- 管理者権限を要求しない。
- 入力フックは「入力があった事実」の検知のみに使う。
- キー内容、マウス座標、入力履歴は保存しない。
- ネットワーク通信は行わない。
- テレメトリは実装しない。
- 自動アップデートはMVPでは実装しない。

## 14. MVP要件

最初の完成版では、以下を満たせばよい。

### 必須

- .NET 8 / C# でビルドできる。
- Windows 10 / 11 で起動できる。
- タスクトレイに常駐する。
- 輝度変更を検知できる。
- 輝度が 1% 以下になったら、800ms後に画面OFFできる。
- キーボード・マウス・トラックパッド操作後に画面が復帰したら、保存済み輝度へ戻せる。
- トレイメニューから終了できる。
- アプリ終了時にWMI watcherと入力hookを解放できる。

### MVPでは任意

- 設定画面
- スタートアップ登録
- 外部モニター対応
- フルスクリーン判定
- インストーラー
- 自動アップデート

## 15. 推奨プロジェクト構成

```text
DimToOff/
├─ DimToOff.sln
├─ src/
│  └─ DimToOff/
│     ├─ DimToOff.csproj
│     ├─ Program.cs
│     ├─ AppContext.cs
│     ├─ Services/
│     │  ├─ BrightnessService.cs
│     │  ├─ DisplayPowerService.cs
│     │  ├─ InputHookService.cs
│     │  ├─ SettingsService.cs
│     │  ├─ StartupService.cs
│     │  └─ LogService.cs
│     ├─ Models/
│     │  ├─ AppSettings.cs
│     │  └─ AppState.cs
│     ├─ UI/
│     │  ├─ TrayIconManager.cs
│     │  └─ SettingsForm.cs
│     └─ Native/
│        ├─ User32.cs
│        └─ NativeConstants.cs
├─ README.md
├─ LICENSE
└─ docs/
   └─ TECHNICAL_NOTES.md
```

## 16. 実装方針

### 16.1 Program.cs

- single-instance guard を実装する。
- WinForms ApplicationContext を起動する。
- 例外発生時はログに記録する。

### 16.2 AppContext.cs

- アプリ全体の状態管理。
- 起動時に SettingsService, BrightnessService, DisplayPowerService, InputHookService, TrayIconManager を初期化。
- 状態遷移をここに集約する。

### 16.3 BrightnessService.cs

責務:

- 現在輝度取得
- 輝度設定
- WMIイベント監視
- polling fallback

公開イベント:

```csharp
public event EventHandler<int>? BrightnessChanged;
```

公開メソッド:

```csharp
int? GetCurrentBrightness();
void SetBrightness(int brightness);
void StartWatching();
void StopWatching();
```

### 16.4 DisplayPowerService.cs

責務:

- 画面OFFコマンド送信
- 可能なら画面ONコマンド送信
- 電源状態通知の登録

公開メソッド:

```csharp
void TurnOffDisplay();
void TurnOnDisplay(); // optional
```

公開イベント:

```csharp
public event EventHandler? DisplayWoke;
```

### 16.5 InputHookService.cs

責務:

- キーボード / マウス入力の検知
- キー内容や入力内容は保持しない

公開イベント:

```csharp
public event EventHandler? UserInputDetected;
```

公開メソッド:

```csharp
void Start();
void Stop();
```

### 16.6 TrayIconManager.cs

責務:

- タスクトレイアイコン表示
- 右クリックメニュー
- Enabled切り替え
- 手動の画面OFF
- 終了

## 17. 主要疑似コード

```csharp
void OnBrightnessChanged(int brightness)
{
    if (!settings.Enabled) return;
    if (state == AppState.Cooldown || state == AppState.RestoringBrightness) return;

    if (brightness > settings.OffThreshold)
    {
        lastUsableBrightness = brightness;
        CancelPendingOff();
        state = AppState.Idle;
        return;
    }

    if (brightness <= settings.OffThreshold && state == AppState.Idle)
    {
        state = AppState.PendingDisplayOff;
        StartDebounceTimer(settings.DebounceMs, () =>
        {
            int? current = brightnessService.GetCurrentBrightness();
            if (current.HasValue && current.Value <= settings.OffThreshold)
            {
                TurnDisplayOffByApp();
            }
            else
            {
                state = AppState.Idle;
            }
        });
    }
}

void TurnDisplayOffByApp()
{
    isDisplayOffByApp = true;
    lastOffTime = DateTime.Now;
    state = AppState.DisplayOffByApp;
    inputHookService.Start();
    displayPowerService.TurnOffDisplay();
}

void OnUserInputDetected()
{
    if (state != AppState.DisplayOffByApp) return;

    if ((DateTime.Now - lastOffTime).TotalMilliseconds < settings.IgnoreInputMs)
        return;

    RestoreBrightnessAfterWake();
}

async void RestoreBrightnessAfterWake()
{
    state = AppState.RestoringBrightness;
    isRestoringBrightness = true;

    await Task.Delay(200); // wait for Windows/display wake

    int target = CalculateRestoreBrightness();
    brightnessService.SetBrightness(target);

    isRestoringBrightness = false;
    isDisplayOffByApp = false;
    inputHookService.Stop();

    state = AppState.Cooldown;
    await Task.Delay(settings.CooldownMs);
    state = AppState.Idle;
}

int CalculateRestoreBrightness()
{
    int target = lastUsableBrightness;

    if (target <= settings.OffThreshold)
        target = settings.DefaultRestoreBrightness;

    if (settings.RestoreMode == "LastUsableWithMinimum")
        target = Math.Max(target, settings.MinimumRestoreBrightness);

    return Math.Clamp(target, 0, 100);
}
```

## 18. 完成条件

- `dotnet build` が成功する。
- `dotnet run` で起動する。
- タスクトレイにアイコンが出る。
- 輝度を最低まで下げると画面だけOFFになる。
- PCはスリープしない。
- キーボード・マウス・トラックパッド操作で画面が復帰する。
- 復帰後、輝度が保存済みの値へ戻る。
- 終了時に常駐プロセスが残らない。
- 管理者権限なしで動作する。
- キーログや個人情報保存をしない。

## 19. READMEに書く内容

READMEには以下を含める。

- アプリ概要
- 使い方
- 対応環境
- インストール方法
- 起動方法
- 終了方法
- 既知の制限
- トラブルシューティング
- 開発者向けビルド方法

## 20. 既知の制限として明記すること

- すべてのWindowsラップトップで輝度WMIイベントが取得できるとは限らない。
- 一部PCでは画面OFF後の復帰挙動がメーカー・GPUドライバ・Modern Standby設定に依存する。
- 外部モニターの個別OFF/ONはMVP対象外。
- 低レベル入力フックを使うが、キー内容は記録しない。
- 画面OFFの直後にタッチパッドが微小入力を出すPCでは、即復帰する可能性がある。

## 21. 参考APIドキュメント

- Microsoft Learn: `WmiMonitorBrightnessEvent`  
  https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightnessevent
- Microsoft Learn: `WmiMonitorBrightnessMethods`  
  https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightnessmethods
- Microsoft Learn: `WmiSetBrightness`  
  https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmisetbrightness-method-in-class-wmimonitorbrightnessmethods
- Microsoft Learn: `WM_SYSCOMMAND` / `SC_MONITORPOWER`  
  https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand
- Microsoft Learn: `RegisterPowerSettingNotification`  
  https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerpowersettingnotification
- Microsoft Learn: Power Setting GUIDs  
  https://learn.microsoft.com/en-us/windows/win32/power/power-setting-guids
- Microsoft Learn: `GetDevicePowerState` limitation  
  https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getdevicepowerstate
