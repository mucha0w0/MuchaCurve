## ゆっくりMovieMaker4 (YMM4) プラグイン開発仕様書

この文書は、YukkuriMovieMaker4（以下YMM4）のプラグイン内部構造を定義した仕様書です。コーディングAIがYMM4の拡張機能を正確に理解し、実装コードを生成するためのガイドラインとして機能します。

---

### 1\. 概要とアーキテクチャ

YMM4のプラグインシステムは、**.NETアーキテクチャ**に基づいた動的リンクライブラリ（DLL）形式で構成されています。YMM4本体の起動時に、特定のディレクトリ（ポータブル版では `user/plugin/`）にあるDLLがスキャンされ、特定のインターフェースを実装したクラスが自動的にロードされます。

#### プラグイン読み込みの仕組み

1. **アセンブリのロード**: YMM4 内部の `PluginAssemblyLoader` がDLLファイルを読み込みます。
2. **型の列挙**: `PluginLoader` が、読み込まれたアセンブリ内から各種プラグインインターフェース（`IToolPlugin`, `IVideoFileSourcePlugin` 等）を実装するすべての型を抽出します。
3. **リスト保存**: 抽出された型は `PluginLoader.Plugins` 等にリストとして保持され、機能ごとに呼び出されます。

#### 重要な注意点

* プラグインDLLが参照するYMM4本体のDLLは、YMM4の実行時にすでにロード済みです。そのため、ビルド時の参照には `<Private>false</Private>` を指定し、出力フォルダーへのコピーを抑制する必要があります。
* プラグインの名前空間やアセンブリ名はYMM4本体や他のプラグインと衝突しない固有の名前にしてください。

---

### 2\. 技術要件と開発環境

#### ターゲットフレームワーク

* 最新のYMM4（v4.47.0.0以降）では **`net10.0-windows10.0.19041.0`** を指定する必要があります。
* 旧バージョンでは .NET 8/9 が使用されていました。**YMM4本体のバージョンに合わせたTFMを正確に指定する必要があります**（ミスマッチではプラグインがロードされません）。

#### 必須設定項目（.csproj）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>             <!-- UI要素の実装に必須 -->
    <Nullable>enable</Nullable>       <!-- 推奨 -->
    <ImplicitUsings>enable</ImplicitUsings>  <!-- 推奨 -->
  </PropertyGroup>
</Project>
```

> `AllowUnsafeBlocks` は unsafe コードを実際に使用する場合のみ追加してください。通常の映像エフェクトプラグインでは不要です。

#### 参照アセンブリ

プラグインの種類に応じて、必要なDLLのみを参照します。すべてのプラグインが全DLLを参照する必要はありません。

> **重要**: `YukkuriMovieMaker.Commons.dll` という単独のDLLは**存在しません**。`YukkuriMovieMaker.Commons` / `YukkuriMovieMaker.Player` / `YukkuriMovieMaker.Plugin` / `YukkuriMovieMaker.Exo` / `YukkuriMovieMaker.Settings` の各名前空間はすべて **`YukkuriMovieMaker.Plugin.dll`** に含まれています。

|DLL名|含まれる名前空間・主な型|用途|必須度|
|-|-|-|-|
|`YukkuriMovieMaker.Plugin.dll`|`YukkuriMovieMaker.Plugin.*`、`YukkuriMovieMaker.Commons.*`、`YukkuriMovieMaker.Player.*`、`YukkuriMovieMaker.Exo.*`、`YukkuriMovieMaker.Settings.*`|プラグイン基盤、共通クラス、プレイヤー、エクスポート、設定|**全プラグインで必須**|
|`YukkuriMovieMaker.Controls.dll`|`YukkuriMovieMaker.Controls.*`|WPFコントロール属性（`AnimationSliderAttribute`、`FileSelectorAttribute` 等）|アイテム編集パネルにUIを自動生成する場合|
|`Vortice.Direct2D1.dll`|`Vortice.Direct2D1.*`、`Vortice.Direct2D1.Effects.*`|Direct2D エフェクト（`ID2D1Image`、`ID2D1DeviceContext`、各種エフェクトクラス）|映像エフェクト・映像ソース系|
|`Vortice.DirectX.dll`|`Vortice.DirectX.*`|DirectX 共通型（`BufferPrecision` 等の列挙型）|映像エフェクト・映像ソース系|
|`SharpGen.Runtime.dll`|`SharpGen.Runtime.*`|Vortice の基底型（`ComObject`、`DisposeCollector` 等）|Vortice を使用する場合|
|`System.Drawing.Common.dll`|`System.Drawing.*`|GDI+ 描画（`Bitmap`、`Graphics` 等）|GDI+ 描画を使用する場合|

**参照の記述方法**: YMM4インストールフォルダからの相対パスで指定し、`<Private>false</Private>` を必ず付与します。

```xml
<ItemGroup>
  <!-- 全プラグインで必須 (全名前空間を含む) -->
  <Reference Include="YukkuriMovieMaker.Plugin">
    <HintPath>$(YMM4DirPath)\YukkuriMovieMaker.Plugin.dll</HintPath>
    <Private>false</Private>
  </Reference>
  <!-- UI属性を使用する場合 -->
  <Reference Include="YukkuriMovieMaker.Controls">
    <HintPath>$(YMM4DirPath)\YukkuriMovieMaker.Controls.dll</HintPath>
    <Private>false</Private>
  </Reference>
  <!-- 映像エフェクト系で追加が必要 -->
  <Reference Include="Vortice.Direct2D1">
    <HintPath>$(YMM4DirPath)\Vortice.Direct2D1.dll</HintPath>
    <Private>false</Private>
  </Reference>
  <Reference Include="Vortice.DirectX">
    <HintPath>$(YMM4DirPath)\Vortice.DirectX.dll</HintPath>
    <Private>false</Private>
  </Reference>
  <Reference Include="SharpGen.Runtime">
    <HintPath>$(YMM4DirPath)\SharpGen.Runtime.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

#### Directory.Build.propsパターン（推奨）

YMM4のインストールパスは開発者の環境ごとに異なります。**`Directory.Build.props`** を使って環境依存のパスを外部化し、`.gitignore` で除外するのが推奨パターンです。

**`Directory.Build.props`**（各開発者が自環境に合わせて作成、Gitに含めない）:

```xml
<Project>
  <PropertyGroup>
    <YMM4DirPath>C:\Tools\YukkuriMovieMaker_v4</YMM4DirPath>
  </PropertyGroup>
</Project>
```

**`Directory.Build.props.sample`**（リポジトリに含めるテンプレート）:

```xml
<Project>
  <PropertyGroup>
    <!-- YMM4のインストールフォルダのパスを環境に合わせて変更してください -->
    <YMM4DirPath>C:\YukkuriMovieMaker4</YMM4DirPath>
  </PropertyGroup>
</Project>
```

このパターンにより、`.csproj` 内で `$(YMM4DirPath)` をビルド変数として使用でき、環境差異を吸収できます。

---

### 3\. プラグインのカテゴリーとインターフェース

プラグインはその目的に応じて、以下のインターフェースや基底クラスを継承し、必要に応じて属性（Attribute）を付与します。

|カテゴリー|インターフェース / 基底クラス|属性 / 役割|
|-|-|-|
|**映像エフェクト**|`VideoEffectBase`|`\[VideoEffect]` 属性が必要|
|**音声エフェクト**|`AudioEffectBase`|`\[AudioEffect]` 属性が必要|
|**映像読み込み**|`IVideoFileSourcePlugin`|映像ソースの生成（Factory）|
|**音声読み込み**|`IAudioFileSourcePlugin`|音声ソースの生成|
|**図形**|`IShapePlugin`|独自の図形アイテムの追加|
|**立ち絵**|`ITachiePlugin`|立ち絵表示機能の拡張|
|**音声合成**|`IVoicePlugin`|外部音声合成エンジンとの連携|
|**ツール/ユーティリティ**|`IToolPlugin`|ツールメニューに項目を追加（MVVM構造）|

#### 共通プロパティ

すべてのプラグインインターフェースは、以下の共通プロパティを持ちます（実装が必要です）：

|プロパティ|型|説明|
|-|-|-|
|`Name`|`string`|プラグインの表示名。メニューやUIに表示される|
|`Details`|`PluginDetailsAttribute`|作者名等のメタ情報。`new() { AuthorName = "..." }` で生成|
|`Updater`|`IPluginUpdater?`|自動更新機能。不要なら `null` を返す|

---

### 4\. ツールプラグイン（IToolPlugin）の詳細

ツールプラグインは、YMM4のツールメニューに独自のウィンドウ/パネルを追加する仕組みです。**MVVM（Model-View-ViewModel）パターン**の3クラス構成をとります。

#### A. プラグイン定義クラス（IToolPlugin を実装）

YMM4がプラグインを認識するためのエントリーポイントです。

```csharp
public class MyToolPlugin : IToolPlugin
{
    public string Name => "ツール名";
    public PluginDetailsAttribute Details => new() { AuthorName = "作者名" };
    public IPluginUpdater? Updater => null;

    // ViewModel と View の型をYMM4に通知
    public Type ViewModelType => typeof(MyToolViewModel);
    public Type ViewType => typeof(MyToolView);

    // 同時に複数インスタンスを開けるか
    public bool AllowMultipleInstances => false;
}
```

|プロパティ|型|説明|
|-|-|-|
|`ViewModelType`|`Type`|`IToolViewModel` を実装したViewModelクラスの型|
|`ViewType`|`Type`|WPF `UserControl` のViewクラスの型|
|`AllowMultipleInstances`|`bool`|`true` なら複数ウィンドウを同時に開ける|

#### B. ViewModelクラス（IToolViewModel を実装）

UIのロジックと状態を管理します。`INotifyPropertyChanged` と `IDisposable` を実装する必要があります。

```csharp
public class MyToolViewModel : IToolViewModel, IDisposable
{
    public string Title => "ウィンドウタイトル";
    public bool CanSuspend => true;  // バックグラウンド時に一時停止可能か

    // IToolViewModel イベント
    public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    // 状態の保存・復元（YMM4が自動的に呼び出す）
    public ToolState SaveState() => new();
    public void LoadState(ToolState state) { }

    public void Dispose()
    {
        // タイマー停止、リソース解放等
    }
}
```

|メンバー|説明|
|-|-|
|`Title`|ツールウィンドウに表示されるタイトル文字列|
|`CanSuspend`|`true` の場合、バックグラウンドに移動した際にYMM4がサスペンド可能|
|`SaveState()`|ツールの状態をYMM4が永続化する際に呼び出される|
|`LoadState(ToolState)`|保存された状態を復元する際に呼び出される|
|`CreateNewToolViewRequested`|新しいビューの生成をリクエストするイベント|

#### C. Viewクラス（WPF UserControl）

XAML + コードビハインドで構成されます。DataContextにはYMM4が自動的にViewModelをバインドします。

**XAML（`MyToolView.xaml`）**:

```xml
<UserControl x:Class="MyNamespace.MyToolView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             MinWidth="200" MinHeight="150">
    <!-- UI定義 -->
</UserControl>
```

**コードビハインド（`MyToolView.xaml.cs`）**:

```csharp
public partial class MyToolView : UserControl
{
    public MyToolView()
    {
        InitializeComponent();
    }
}
```

#### ツールプラグインでのUI更新パターン

定期的にUIを更新する場合（モニタリング系ツール等）、`DispatcherTimer` を使用します。WPFのUIスレッドで安全に更新するためです。

```csharp
private readonly DispatcherTimer timer;

public MyToolViewModel()
{
    timer = new DispatcherTimer(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(150)
    };
    timer.Tick += OnTimerTick;
    timer.Start();
}
```

**重要**: `Dispose()` でタイマーを確実に停止し、イベントハンドラを解除してください。

---

### 5\. 内部構造の詳細：映像読み込みプラグインを例に

メディアソース系プラグインは、一般に「プラグイン管理クラス」と「データ処理クラス」の2層構造をとります。

#### A. プラグイン管理クラス (IVideoFileSourcePlugin)

* **役割**: 特定のファイルパスに対して、自身が処理可能かを判断し、ソースインスタンスを生成します。
* **主要メソッド**: `CreateVideoFileSource(IGraphicsDevicesAndContext devices, string filePath)`

  * 処理可能なファイル（例：拡張子が `.txt`）であれば `IVideoFileSource` を返し、そうでなければ `null` を返します。

#### B. 映像データクラス (IVideoFileSource)

* **プロパティ**:

  * `Duration`: 動画の総再生時間を `TimeSpan` で返します。
  * `Output`: 現在のフレーム画像（**`ID2D1Image`** 型）を返します。

* **メソッド**:

  * `Update(TimeSpan time)`: 指定された再生時間に基づき、内部状態を更新します。
  * `GetFrameIndex(TimeSpan time)`: 指定時間のフレーム番号を返します（exo出力用）。
  * **`Dispose()`**: 使用済みの `ID2D1Bitmap` や `ID2D1Image` などのDirectXリソースを明示的に解放する必要があります。

---

### 6\. UIとパラメータシステム

エフェクト系プラグインのパラメータは、プロパティに属性を付与することで、YMM4のアイテム編集パネルにUIが自動生成されます。

#### Display 属性

`System.ComponentModel.DataAnnotations.Display` 属性でラベル、説明文、折りたたみグループを制御します。

```csharp
[Display(GroupName = "グループ名", Name = "パラメータ名", Description = "ツールチップに表示される説明文")]
```

|  プロパティ  |                      効果                      |
| --------- | ---------------------------------------- |
| `Name`      | パラメータのラベルテキスト                   |
| `Description`| マウスホバー時のツールチップ                 |
| `GroupName` | 折りたたみセクションのヘッダーテキスト。未指定時は「その他」と表示される |

> **ノウハウ**: 同じ `GroupName` を持つパラメータは同一の折りたたみセクションにグルーピングされます。プラグイン名を `GroupName` に設定すると、デフォルトの「その他」を置き換えられます。

#### AnimationSlider 属性（アニメーション対応スライダー）

`Animation` 型プロパティに付与し、キーフレームアニメーション対応のスライダーUIを生成します。名前空間: `YukkuriMovieMaker.Controls`。

```csharp
[AnimationSlider("F1", "%", 0, 100)]
```

| コンストラクタ引数 | 型       |                        説明                         |
| -------------- | -------- | --------------------------------------------------- |
| `stringFormat` | `string` | 数値の表示形式。`"F0"`=整数、`"F1"`=小数点1桁、`"F2"`=小数点2桁 |
| `unitText`     | `string` | 数値の右に表示される単位文字列（`"%"`、`"px"`、`"°"` 等）       |
| `defaultMin`   | `double` | スライダーの最小値                                        |
| `defaultMax`   | `double` | スライダーの最大値                                        |

#### FileSelector 属性（ファイル選択ダイアログ）

`string` 型プロパティに付与し、ファイル選択ボタン付きのパス入力UIを生成します。名前空間: `YukkuriMovieMaker.Controls`。

```csharp
[FileSelector(FileGroupType.None, CustomFilterName = "CUBE LUT", CustomFilterValue = "*.cube;*.CUBE")]
```

| プロパティ            |   型   |                  説明                   |
| ------------------- | ------ | --------------------------------------- |
| コンストラクタ第1引数 | `FileGroupType` | ファイルグループ。独自フィルタの場合は `FileGroupType.None` |
| `CustomFilterName`  | `string` | ファイルダイアログのフィルター表示名       |
| `CustomFilterValue` | `string` | 拡張子フィルター。複数指定は `;` 区切り（例: `"*.cube;*.CUBE"`） |

`FileGroupType` には `Video`, `Audio`, `Image` 等のプリセットがありますが、独自の拡張子を指定する場合は `None` を使用し、`CustomFilterName` / `CustomFilterValue` でフィルターを定義します。

#### DefaultValue 属性

`System.ComponentModel.DefaultValue` でパラメータの初期値を宣言します。`Animation` プロパティの場合、コンストラクタの第1引数と一致させてください。

```csharp
[DefaultValue(30d)]  // double 型のリテラルで指定
public Animation Strength { get; } = new(30, 0, 100, 0);
```

#### Animation クラス（アニメーション対応パラメータ）

> **重要**: YMM4 の `Animation` クラスは**非ジェネリック**です。`Animation<T>` ではありません。名前空間: `YukkuriMovieMaker.Commons`。

```csharp
// コンストラクタ: Animation(double 初期値, double 最小値, double 最大値, int イージング種別)
public Animation Strength { get; } = new(30, 0, 100, 0);
```

値の取得には `GetValue` メソッドを使用します。戻り値は `double` です。

```csharp
// シグネチャ: double GetValue(long frame, long totalFrame, int fps)
double value = animation.GetValue(
    effectDescription.ItemPosition.Frame,  // 現在フレーム
    effectDescription.ItemDuration.Frame,  // アイテム総フレーム数
    effectDescription.FPS);                // FPS
```

YMM4 がキーフレーム管理に使用するため、`GetAnimatables()` で全ての `Animation` プロパティを列挙する必要があります。

**注意**: ツールプラグイン（`IToolPlugin`）では、このパラメータ自動生成UIは使用しません。ツールプラグインは独自のWPF UserControlでUIを構築します。

#### カスタムプロパティエディタ（PropertyEditorAttribute2）

YMM4 の標準UI属性（`AnimationSlider`、`FileSelector` 等）では対応できない複雑なUIが必要な場合、**カスタムプロパティエディタ**を作成できます。`PropertyEditorAttribute2` を継承した属性クラスを定義し、任意の WPF `UserControl` をプロパティパネルに埋め込みます。

##### 属性クラスの実装

```csharp
using System.Windows;
using YukkuriMovieMaker.Commons;

public class MyEditorAttribute : PropertyEditorAttribute2
{
    // エディタの幅。FullWidth でパネル全幅を使用
    public override PropertyEditorSize PropertyEditorSize => PropertyEditorSize.FullWidth;

    public MyEditorAttribute()
    {
        MinHeight = 280;  // 最小高さ（ピクセル）
    }

    // エディタ UserControl のインスタンスを生成
    public override FrameworkElement Create()
    {
        return new MyEditorControl();
    }

    // YMM4 から ItemProperty[] が渡され、バインドを設定
    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is MyEditorControl editor && itemProperties.Length > 0)
            editor.SetBinding(itemProperties);
    }

    // バインド解除（エディタ非表示時に呼ばれる）
    public override void ClearBindings(FrameworkElement control)
    {
        if (control is MyEditorControl editor)
            editor.ClearBinding();
    }
}
```

| メンバー | 説明 |
|-|-|
| `PropertyEditorSize` | `FullWidth` = パネル全幅使用、`Default` = 標準幅 |
| `MinHeight` | エディタ領域の最小高さ（ピクセル単位） |
| `Create()` | WPF UserControl インスタンスを返す。YMM4 が呼び出す |
| `SetBindings()` | プロパティのバインドを設定。`ItemProperty[]` で対象プロパティの参照を受け取る |
| `ClearBindings()` | アイテム選択解除時等にバインドを解除。リソースリークを防ぐために確実に実装する |

##### エフェクト定義での使用

属性をプロパティに付与するだけで、YMM4 が自動的にカスタムエディタを表示します。

```csharp
[Display(GroupName = "Custom Curves", Name = "", Description = "説明テキスト")]
[MyEditor]  // ← カスタム属性
public string MyDataJson
{
    get => myDataJson;
    set => Set(ref myDataJson, value);  // VideoEffectBase.Set で変更通知
}
```

> **ノウハウ**: `Display` の `Name` を空文字列 `""` にすると、ラベルが表示されずエディタのみが全幅で表示されます。カーブエディタやプレビューパネル等の大型UIに適しています。

##### IPropertyEditorControl2 インターフェース

カスタムエディタの UserControl は `IPropertyEditorControl2` を実装する必要があります。名前空間: `YukkuriMovieMaker.Commons`。

```csharp
public partial class MyEditorControl : UserControl, IPropertyEditorControl2
{
    // 編集開始・終了イベント（Undo/Redo 管理に必須）
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    // YMM4 がフォーカスを当てる際に呼び出す
    public void SetFocus() => MyCanvas.Focus();

    // エディタ情報（通常は空実装）
    public void SetEditorInfo(IEditorInfo editorInfo) { }
}
```

| メンバー | 説明 |
|-|-|
| `BeginEdit` | 編集操作の**開始前**に発火。YMM4 が Undo ポイントを記録する |
| `EndEdit` | 編集操作の**完了後**に発火。YMM4 が Undo ポイントを確定する |
| `SetFocus()` | YMM4 がエディタにフォーカスを移す際に呼び出す |
| `SetEditorInfo()` | 追加のエディタメタ情報。通常は空実装で問題ない |

> **重要: BeginEdit / EndEdit の呼び出しパターン**
>
> **すべてのデータ変更操作**で `BeginEdit` → データ変更 → `CommitData` → `EndEdit` の順序を守ってください。このイベントペアを欠くと、YMM4 の Undo/Redo が正しく機能しません。
>
> ```csharp
> // ポイント追加の例
> BeginEdit?.Invoke(this, EventArgs.Empty);
> channelData.Points.Insert(index, newPoint);
> CommitData();  // JSON シリアライズして ItemProperty に書き戻し
> EndEdit?.Invoke(this, EventArgs.Empty);
> ```
>
> **ドラッグ操作の場合**: `MouseDown` で `BeginEdit`、`MouseUp` で `EndEdit` を呼び出します。ドラッグ中の `MouseMove` では `CommitData` のみを呼び、`BeginEdit`/`EndEdit` は呼びません。これにより、ドラッグ全体が1つの Undo 単位になります。

##### ItemProperty を介したデータの読み書き

`SetBindings` で受け取った `ItemProperty[]` を保持し、プロパティのget/setに使用します。

```csharp
private ItemProperty[]? boundProperties;

public void SetBinding(ItemProperty[] itemProperties)
{
    ClearBinding();
    boundProperties = itemProperties;

    // 現在の値を読み取り
    if (boundProperties.Length > 0)
    {
        var prop = boundProperties[0];
        var json = prop.PropertyInfo.GetValue(prop.PropertyOwner) as string;
        myData = MyData.Deserialize(json ?? string.Empty);
    }
    Redraw();
}

public void ClearBinding()
{
    boundProperties = null;
}

// データをエフェクトに書き戻す
private void CommitData()
{
    if (boundProperties == null) return;
    var json = myData.Serialize();
    foreach (var prop in boundProperties)
        prop.PropertyInfo.SetValue(prop.PropertyOwner, json);
}
```

| メンバー | 型 | 説明 |
|-|-|-|
| `ItemProperty.PropertyInfo` | `PropertyInfo` | 対象プロパティのリフレクション情報 |
| `ItemProperty.PropertyOwner` | `object` | プロパティを持つオブジェクト（エフェクトインスタンス等） |

> **ノウハウ**: `boundProperties` を `foreach` で全要素に書き込むのは、YMM4 で複数アイテムが同時選択されている場合に対応するためです。各 `ItemProperty` は異なるアイテムの同一プロパティを指しています。

##### JSON シリアライズ（AOT / Trimming 対応）

カスタムエディタで複雑なデータ構造を `string` プロパティに格納する場合、JSON シリアライズが一般的です。.NET の Source Generator を使用すると AOT / Trimming セーフなシリアライズが可能です。

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

public class MyData
{
    public List<MyPoint> Points { get; set; } = [];

    public string Serialize()
        => JsonSerializer.Serialize(this, MyJsonContext.Default.MyData);

    public static MyData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new MyData();
        try
        {
            return JsonSerializer.Deserialize(json, MyJsonContext.Default.MyData)
                   ?? new MyData();
        }
        catch { return new MyData(); }
    }
}

// Source Generator コンテキスト
[JsonSerializable(typeof(MyData))]
[JsonSerializable(typeof(MyPoint))]
[JsonSerializable(typeof(List<MyPoint>))]
internal partial class MyJsonContext : JsonSerializerContext;
```

> **ノウハウ**: `JsonSerializerContext` を使用する Source Generator 方式は、リフレクションベースの `JsonSerializer.Serialize<T>()` よりも高速で、.NET の Trimming 警告が発生しません。`[JsonSerializable]` にはルート型だけでなく、内部で使用される型（リスト型を含む）もすべて列挙してください。

---

### 6.5\. 映像エフェクトプラグインの詳細実装

映像エフェクトは「エフェクト定義クラス」と「D2Dプロセッサクラス」の2層構造で実装します。

#### A. エフェクト定義クラス (VideoEffectBase 継承)

UIパラメータの宣言とプロセッサの生成を担当します。

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Settings;

// VideoEffect 属性: (表示名, カテゴリ配列, タグ配列, bool, bool)
[VideoEffect("エフェクト名", new[] { "色調補正" }, new string[0], false, false)]
public class MyVideoEffect : VideoEffectBase
{
    // Label: アイテム編集パネルでの表示名
    public override string Label => "エフェクト名";

    // --- UI パラメータ ---
    [Display(GroupName = "グループ名", Name = "ファイル", Description = "ファイルを選択")]
    [FileSelector(FileGroupType.None, CustomFilterName = "フィルター名", CustomFilterValue = "*.ext")]
    public string FilePath
    {
        get => filePath;
        set => Set(ref filePath, value);  // VideoEffectBase.Set で変更通知
    }
    private string filePath = string.Empty;

    [Display(GroupName = "グループ名", Name = "適用率", Description = "0%〜100%")]
    [AnimationSlider("F1", "%", 0, 100)]
    [DefaultValue(100d)]
    public Animation Strength { get; } = new(100, 0, 100, 0);

    // --- VideoEffectBase 実装 ---
    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        => new MyVideoEffectProcessor(devices, this);

    // exo エクスポート非対応の場合は空を返す
    public override IEnumerable<string> CreateExoVideoFilters(
        int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    // 全 Animation プロパティを列挙（キーフレーム管理用）
    protected override IEnumerable<IAnimatable> GetAnimatables() => [Strength];
}
```

**`VideoEffect` 属性の引数**:

| 引数 | 型 | 説明 |
|-|-|-|
| 第1 | `string` | エフェクト名（エフェクト一覧に表示） |
| 第2 | `string[]` | カテゴリー（例: `"色調補正"`, `"動き"`） |
| 第3 | `string[]` | タグ（検索用、通常空配列） |
| 第4 | `bool` | 不明（通常 `false`） |
| 第5 | `bool` | 不明（通常 `false`） |

#### B. D2D プロセッサクラス (VideoEffectProcessorBase 継承)

Direct2D のエフェクトチェーンを構築・更新・破棄するクラスです。名前空間: `YukkuriMovieMaker.Player.Video.Effects`。

> **重要**: `VideoEffectProcessorBase` のメンバーには **sealed（オーバーライド不可）** と **abstract（実装必須）** があります。sealed メンバーをオーバーライドしようとするとコンパイルエラーになります。

**sealed メンバー（オーバーライド不可・内部利用専用）**:

| メンバー | 型 | 説明 |
|-|-|-|
| `Output` | `ID2D1Image` (property) | エフェクトの出力画像。YMM4 が直接参照する |
| `SetInput(ID2D1Image)` | method | 入力画像の設定。YMM4 が呼び出す |
| `ClearInput()` | method | 入力のクリア。YMM4 が呼び出す |
| `Dispose()` | method | リソース解放。YMM4 が呼び出す |

**abstract メンバー（実装必須）**:

| メンバー | シグネチャ | 説明 |
|-|-|-|
| `CreateEffect` | `ID2D1Image CreateEffect(IGraphicsDevicesAndContext)` | D2D エフェクトチェーンを構築し、最終出力の `ID2D1Image` を返す |
| `setInput` | `void setInput(ID2D1Image)` | 入力画像をエフェクトチェーンに接続する |
| `ClearEffectChain` | `void ClearEffectChain()` | エフェクトチェーンの入力を切断する |
| `Update` | `DrawDescription Update(EffectDescription)` | フレームごとの更新処理。パラメータの反映を行う |

> **注意**: `setInput` は**小文字の `s`** で始まります。C# の命名規則に反しますが、ベースクラスの定義がそうなっているため、必ず `setInput` と記述してください。`SetInput` と書くと sealed の `SetInput` と衝突しコンパイルエラーになります。

**protected フィールド（サブクラスからアクセス可能）**:

| フィールド | 型 | 説明 |
|-|-|-|
| `disposer` | `DisposeCollector` | リソースの自動破棄管理。`disposer.Collect(effect)` で登録 |
| `effectOutput` | `ID2D1Image` | エフェクトの最終出力画像 |
| `input` | `ID2D1Image` | 現在の入力画像 |

**実装例**:

```csharp
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

public class MyVideoEffectProcessor : VideoEffectProcessorBase
{
    private readonly MyVideoEffect effect;
    private SomeEffect? someEffect;

    public MyVideoEffectProcessor(IGraphicsDevicesAndContext devices, MyVideoEffect effect)
        : base(devices)  // ← ベースコンストラクタが CreateEffect を呼び出す
    {
        this.effect = effect;
    }

    protected override ID2D1Image CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var dc = devices.DeviceContext;

        someEffect = new SomeEffect(dc);
        disposer.Collect(someEffect);  // ← disposer に登録することで自動破棄される

        return someEffect.Output;  // 最終出力を返す
    }

    protected override void setInput(ID2D1Image input)  // ← 小文字の s
    {
        someEffect?.SetInput(0, input, true);
    }

    protected override void ClearEffectChain()
    {
        someEffect?.SetInput(0, null!, true);
    }

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        // パラメータ反映
        double value = effect.Strength.GetValue(
            effectDescription.ItemPosition.Frame,
            effectDescription.ItemDuration.Frame,
            effectDescription.FPS);

        // エフェクトにパラメータを適用
        // ...

        return effectDescription.DrawDescription;
    }

    protected override void Dispose(bool disposing)
    {
        // 独自リソースの解放（disposer に登録していないもの）
        base.Dispose(disposing);  // ← disposer に登録済みリソースが解放される
    }
}
```

**エフェクトチェーンの設計パターン**:

D2D エフェクトは入力スロット番号で接続します。`SetInput(index, image, invalidate)` で入力を設定し、`.Output` で出力を取得します。

例: LUT適用率をコントロールするチェーン:

```
Input → LookupTable3D ──────┐
                              ├→ CrossFade → Output
Input ─────────────────┘
         (weight = 適用率)
```

`CrossFade` エフェクトの計算式: `output = Weight × Input0 + (1 - Weight) × Input1`

このため、Input0 に「エフェクト適用済み」、Input1 に「元映像」を接続すると、Weight=1.0 で完全適用、Weight=0.0 でパススルーになります。

#### C. D2D エフェクトクラス一覧 (Vortice.Direct2D1.Effects)

YMM4 が利用している Vortice ラッパー経由で、Direct2D の組み込みエフェクトを使用できます。以下は代表例です。

| クラス名 | 用途 | 主なプロパティ |
|-|-|-|
| `LookupTable3D` | 3D LUT 適用 | `.LUT` (ID2D1LookupTable3D) |
| `CrossFade` | 2入力のブレンド | `.Weight` (float, 0.0〜1.0) |
| `GaussianBlur` | ガウシアンブラー | `.StandardDeviation` (float) |
| `ColorMatrix` | 色行列変換 | `.ColorMatrix` (Matrix5x4) |
| `Brightness` | 明るさ調整 | `.WhitePoint`, `.BlackPoint` |

各エフェクトは `new EffectClass(ID2D1DeviceContext)` で生成し、必ず `disposer.Collect()` で登録してください。

#### D. ID2D1LookupTable3D の生成

3D LUT テクスチャを生成する場合、`ID2D1DeviceContext.CreateLookupTable3D` を使用します。

```csharp
var dc = devices.DeviceContext;
int size = 33; // LUTの一辺のサイズ
const int bytesPerTexel = 4 * sizeof(float); // RGBA × float32 = 16バイト

// extents: 各軸のサイズ [X, Y, Z]
int[] extents = [size, size, size];
// strides: 行ストライドとスライスストライド
int[] strides = [
    size * bytesPerTexel,          // 1行分のバイト数
    size * size * bytesPerTexel    // 1スライス分のバイト数
];

byte[] byteData = /* RGBA float32 バイト配列 */;

ID2D1LookupTable3D lut = dc.CreateLookupTable3D(
    BufferPrecision.PerChannel32Float,  // ← PerChannel32Float が正しい。Float32 ではない
    extents,
    byteData,
    byteData.Length,  // dataCount: バイト配列の総バイト数
    strides);
```

> **重要: データの軸順序**
>
> `CreateLookupTable3D` のメモリレイアウトは **B(X)軸が最速変化**（メモリ上で隣接）、G(Y)が中間、**R(Z)軸が最遅変化** です。
> これは .cube ファイルの軸順序（B最速、G中間、R最遅）と**同一**です。
>
> | | 最速変化（メモリ隣接） | 中間 | 最遅変化 |
> |---|---|---|---|
> | **.cube ファイル** | **B** | G | R |
> | **D2D CreateLookupTable3D** | **B (X軸)** | G (Y軸) | R (Z軸) |
>
> したがって .cube ファイルのデータをそのままのメモリ順序で `CreateLookupTable3D` に渡せば、軸の並べ替えは不要です。
>
> **独自に3D LUTデータを生成する場合**は、最内ループ（最速変化軸）を B、中間を G、最外ループを R としてください:
> ```csharp
> for (int rIdx = 0; rIdx < size; rIdx++)      // Z軸 = Red（最遅）
>     for (int gIdx = 0; gIdx < size; gIdx++)   // Y軸 = Green（中間）
>         for (int bIdx = 0; bIdx < size; bIdx++) // X軸 = Blue（最速）
>             WriteLutTexel(rIdx, gIdx, bIdx);
> ```
>
> **注意**: この軸順序を逆（R=最速）にすると、R軸とB軸が入れ替わり、青が黄色になる等の重大な色化けが発生します。

> **注意: `BufferPrecision` 列挙型**
>
> `BufferPrecision.PerChannel32Float` が正しい値です。`BufferPrecision.Float32` は存在しません。また、`extents` と `strides` の型は `int[]`（`uint[]` ではない）です。

---

### 7\. リソース管理とメモリ

#### DirectX リソース（エフェクト・映像ソース系）

YMM4はグラフィックス処理に **Direct2D (Vortice.Direct2D1)** を利用しています。

* **DirectXオブジェクトの解放**: プラグイン内で生成した `ID2D1Image` や `ID2D1Bitmap` などのエフェクト出力は、`Dispose` メソッドで必ず解放しなければなりません。
* **未解放オブジェクトの検出**: YMM4の「開発者モード」を有効にすると、リークしているDirectXオブジェクトを起動・終了時に検出可能です。

#### マネージドリソース（ツール系プラグイン等）

ツールプラグインのように WPF の `WriteableBitmap` や `System.Drawing.Bitmap` を使用する場合も、適切な破棄が必要です。

* `IDisposable` を実装し、`Dispose()` でタイマー停止・イベントハンドラ解除・バッファ参照の切り離しを行います。
* `System.Drawing.Bitmap` は `using` 文で囲み、確実に破棄してください。
* 再利用可能なバッファ（`byte\[]` 等）は使い回すことで GC 負荷を軽減できます。

#### System.Drawing.Common の参照

`System.Drawing.Common`（GDI+）を使用する場合、NuGetパッケージではなく**YMM4に同梱されているDLL**を参照します。.NET 7以降、`System.Drawing.Common` はWindowsでのみサポートされるプラットフォーム固有ライブラリとなっているため、YMM4本体と同じバージョンを使用することで互換性問題を避けられます。

```xml
<Reference Include="System.Drawing.Common">
  <HintPath>$(YMM4DirPath)\\System.Drawing.Common.dll</HintPath>
  <Private>false</Private>
</Reference>
```

---

### 7.5\. D2D リソースの動的管理パターン

映像エフェクトでは、`CreateEffect` で構築したエフェクトチェーン自体は `disposer.Collect()` で管理しますが、**パラメータ変更に応じて動的に生成・破棄するリソース**（LUT テクスチャ等）は自前で管理する必要があります。

#### パラメータ変更検知と条件付き再生成

`Update` メソッドはフレームごとに呼ばれるため、不要な再生成を避ける仕組みが重要です。

```csharp
private string lastParamJson = string.Empty;
private bool resourceReady;

public override DrawDescription Update(EffectDescription effectDescription)
{
    // 変更検知: 前回の値と比較して変更があった場合のみ再生成
    string currentJson = effect.DataJson;
    if (currentJson != lastParamJson)
    {
        lastParamJson = currentJson;
        RebuildResource();
    }

    // リソース未設定時はパススルー
    double strength = resourceReady
        ? effect.Strength.GetValue(...) / 100.0
        : 0.0;

    if (crossFade != null)
        crossFade.Weight = (float)Math.Clamp(strength, 0.0, 1.0);

    return effectDescription.DrawDescription;
}
```

> **ノウハウ**: `string` の比較（`!=` 演算子）は参照の等値性ではなく値の等値性で比較されるため、JSON 文字列の変更検知に有効です。大きなデータでは比較コストが気になりますが、通常のUI操作頻度（数十ms〜）では問題になりません。

#### 恒等パラメータの早期スキップ

エフェクトのパラメータがデフォルト値（変化なし）の場合、LUT 生成等の重い処理自体をスキップすることで、不要なGPUリソースの消費を避けられます。

```csharp
if (curveData.IsIdentity())
{
    // 恒等 → LUT不要。CrossFade.Weight=0 でパススルー
    DisposeLut();
    return;
}
```

#### D2D リソースの独立管理（disposer 外）

`ID2D1LookupTable3D` のように**パラメータ変更ごとに再生成するリソース**は、`disposer` に登録せず独立して管理します。`disposer` はエフェクト全体の終了時に一括破棄するためのものであり、途中で再生成するリソースの管理には向きません。

```csharp
private ID2D1LookupTable3D? lutResource;

private void RebuildLut()
{
    // 旧リソースを先に破棄（GPU メモリリーク防止）
    DisposeLut();

    // 新しい LUT リソースを生成
    lutResource = dc.CreateLookupTable3D(
        BufferPrecision.PerChannel32Float, extents, lutBytes, lutBytes.Length, strides);

    if (lutEffect != null && lutResource != null)
    {
        lutEffect.LUT = lutResource;
        lutReady = true;
    }
}

private void DisposeLut()
{
    if (lutEffect != null)
        lutEffect.LUT = null!;  // エフェクトからの参照を先に切断
    lutResource?.Dispose();      // その後リソースを破棄
    lutResource = null;
    lutReady = false;
}

protected override void Dispose(bool disposing)
{
    DisposeLut();  // 独自リソースを先に破棄
    base.Dispose(disposing);  // disposer 登録分はここで破棄される
}
```

> **重要**: `lutEffect.LUT = null!` で参照を切断してから `lutResource.Dispose()` を呼ぶ順序を守ってください。逆にすると、エフェクトが破棄済みリソースを参照してクラッシュする可能性があります。

---

### 7.6\. パフォーマンス最適化のベストプラクティス

YMM4 はリアルタイムプレビューを行うため、プラグインのパフォーマンスはユーザー体験に直結します。以下は実装で得た最適化テクニックです。

#### WPF 描画の最適化

##### DrawingVisual の活用

WPF の `Canvas.Children` に `Shape` や `Line` を大量追加すると、レイアウトパスとビジュアルツリー管理のオーバーヘッドが大きくなります。代わりに `DrawingVisual` を使用すると、1つのビジュアルオブジェクトで複雑な描画を完結できます。

```csharp
var visual = new DrawingVisual();
using (var dc = visual.RenderOpen())
{
    dc.DrawLine(pen, point1, point2);
    dc.DrawEllipse(brush, pen, center, rx, ry);
    dc.DrawGeometry(null, pen, geometry);
}
// Canvas.Children に VisualHost (FrameworkElement) 経由で追加
```

**VisualHost のパターン**: `DrawingVisual` を `Canvas.Children` に追加するには、`FrameworkElement` のラッパーが必要です。

```csharp
private sealed class VisualHost : FrameworkElement
{
    private readonly Visual visual;
    public VisualHost(Visual v) { visual = v; AddVisualChild(v); }
    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => visual;
}
```

> **ノウハウ**: `DrawingVisual` と `VisualHost` は再利用可能です。毎フレーム `new` するのではなく、フィールドに保持して `RenderOpen()` で描画内容を更新する方が、ビジュアルツリーの組み替えコストを削減できます。

##### Brush / Pen の Freeze

`SolidColorBrush` や `Pen` を `Freeze()` するとスレッドセーフになり、WPF の変更追跡オーバーヘッドが消失します。描画で繰り返し使用するブラシ・ペンは必ず Freeze して `static readonly` フィールドに保持してください。

```csharp
private static readonly SolidColorBrush MyBrush = Freeze(new(Color.FromRgb(0xCC, 0xCC, 0xCC)));
private static readonly Pen MyPen = FreezePen(new(MyBrush, 2.0));

private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
private static Pen FreezePen(Pen p) { p.Freeze(); return p; }
```

##### StreamGeometry の活用

`PathGeometry` よりも `StreamGeometry` の方が描画パフォーマンスが高い（ミニ言語パーサーがなく、直接描画コマンドを発行するため）。カーブや多角形の描画に最適です。

```csharp
var geometry = new StreamGeometry();
using (var ctx = geometry.Open())
{
    ctx.BeginFigure(startPoint, isFilled: false, isClosed: false);
    for (int i = 1; i < pointCount; i++)
        ctx.LineTo(points[i], isStroked: true, isSmoothJoin: false);
}
geometry.Freeze();  // 必ず Freeze
dc.DrawGeometry(null, pen, geometry);
```

#### メモリ割り当ての最適化

##### stackalloc の活用

小さな一時配列は `stackalloc` でスタック上に確保することで、GC 負荷をゼロにできます。

```csharp
// 固定サイズの LUT バッファ（256要素以下が推奨）
Span<double> lut = stackalloc double[256];

// サイズが可変の場合は閾値で切り替え
bool useStack = count <= 64;
Span<double> buffer = useStack ? stackalloc double[count] : new double[count];
```

> **注意**: `stackalloc` はスタックサイズ（通常 1MB）を消費するため、大きすぎるサイズ（数万要素以上）は避けてください。64〜256 要素程度が安全な範囲です。

##### MemoryMarshal.Cast によるゼロコピー変換

`float[]` → `byte[]` の変換で中間コピーを避けるパターンです。

```csharp
// 最初から byte[] を確保し、float ビューで書き込む
var result = new byte[totalFloats * sizeof(float)];
Span<float> floatView = MemoryMarshal.Cast<byte, float>(result.AsSpan());
// floatView[i] に書き込むと result に直接反映される
```

> **注意**: `MemoryMarshal.Cast<byte, float>(result)` と書くと `ReadOnlySpan<float>` が返される場合があります。`result.AsSpan()` を明示的に渡して `Span<float>` を取得してください。

##### CollectionsMarshal.AsSpan の活用

`List<T>` の内部配列に直接アクセスすることで、`IReadOnlyList<T>` のインターフェース仮想ディスパッチを回避できます。補間処理等のホットパスで効果的です。

```csharp
using System.Runtime.InteropServices;

// List<CurvePoint> の内部バッファに直接アクセス（コピーなし）
ReadOnlySpan<CurvePoint> span = CollectionsMarshal.AsSpan(points);
// span[i] は points[i] と同じデータだが、バウンドチェック以外のオーバーヘッドがない
```

#### カスタムエディタUI のインタラクション最適化

##### ダブルクリック判定

WPF 標準の `MouseButtonEventArgs.ClickCount` はシステム設定（通常500ms）に依存しており、エディタUI には遅すぎる場合があります。独自の短いタイマーで判定すると、シングルクリック操作（追加等）の反応速度が向上します。

```csharp
private DateTime lastClickTime = DateTime.MinValue;
private const double DoubleClickMaxMs = 250;

private void OnMouseDown(...)
{
    var now = DateTime.UtcNow;
    bool isDoubleClick = (now - lastClickTime).TotalMilliseconds < DoubleClickMaxMs;
    lastClickTime = now;
    if (isDoubleClick) { /* ダブルクリック処理 */ }
    else { /* シングルクリック処理 */ }
}
```

##### マウスドラッグの判定

シングルクリック（種類切替等）とドラッグ（座標移動等）を区別するには、`MouseUp` 時にマウスの移動距離を確認します。

```csharp
private Point dragStartPos;
private const double ClickMoveThreshold = 4.0;

// MouseDown で dragStartPos を記録
// MouseUp で判定:
double dx = pos.X - dragStartPos.X;
double dy = pos.Y - dragStartPos.Y;
bool isClick = (dx * dx + dy * dy) < ClickMoveThreshold * ClickMoveThreshold;
```

##### 近接ポイントの自動統合

ドラッグ完了時に近くのポイントを自動統合すると、誤操作で密集したポイントが残る問題を防げます。

```csharp
private void MergeClosePoints(ChannelData ch, int idx)
{
    // 端点（始点・終点）は統合しない
    if (idx <= 0 || idx >= ch.Points.Count - 1) return;
    var ptC = NormToCanvas(ch.Points[idx]);
    double threshold2 = MergePixelThreshold * MergePixelThreshold;
    // 右隣（終端除く）→ 左隣（始端除く）の順でチェック・削除
}
```

---

### 8\. YMM4内部へのアクセス（リフレクション）

YMM4の公開APIでは提供されない内部機能にアクセスする必要がある場合、リフレクションを利用できます。ただし以下の点に注意してください。

* **YMM4のバージョンアップで内部構造が変わる可能性が高い**ため、キャッシュとフォールバックを必ず実装すること。
* `Application.Current.MainWindow.DataContext` からメインウィンドウのViewModelにアクセスできます。
* メソッド探索は初回のみ行い、結果（`MethodInfo`等）をキャッシュして2回目以降の呼び出しを高速化すること。
* 例外が発生した場合はキャッシュを無効化し、次回呼び出し時に再探索する設計が堅牢です。

```csharp
// リフレクションキャッシュのパターン例
private MethodInfo? cachedMethod;
private bool reflectionResolved;

private void TryCallInternal()
{
    try
    {
        if (!reflectionResolved)
        {
            // 初回のみ探索
            reflectionResolved = true;
            cachedMethod = FindTargetMethod();
        }
        cachedMethod?.Invoke(target, args);
    }
    catch
    {
        // 失敗時はキャッシュ無効化 → 次回再探索
        reflectionResolved = false;
        cachedMethod = null;
    }
}
```

---

### 9\. デバッグとテスト

#### 開発サイクル

1. プロジェクトをビルド（`dotnet build` または Visual Studio のビルド）
2. 出力DLLを YMM4 の `user/plugin/` フォルダにコピー
3. YMM4を起動してプラグインが読み込まれることを確認

#### よくあるトラブルと対処

|症状|原因|対処|
|-|-|-|
|プラグインがメニューに表示されない|TFMの不一致|`.csproj` の `TargetFramework` がYMM4本体と一致しているか確認|
|プラグインがメニューに表示されない|インターフェース未実装|`IToolPlugin` 等の必要なメンバーがすべて実装されているか確認|
|ビルドエラー（DLL参照）|`HintPath` のパス誤り|`Directory.Build.props` のパスを確認|
|ビルドエラー（`YukkuriMovieMaker.Commons` が見つからない）|存在しないDLLを参照|`YukkuriMovieMaker.Commons.dll` は存在しない。全名前空間は `YukkuriMovieMaker.Plugin.dll` に含まれる|
|ビルドエラー（`SetInput` がオーバーライドできない）|sealed メンバーとの衝突|`VideoEffectProcessorBase` の `SetInput` は sealed。実装するのは小文字の `setInput`|
|ビルドエラー（`Animation<T>` が見つからない）|型名誤り|`Animation` は非ジェネリック。`Animation<double>` ではなく `Animation` を使用|
|ビルドエラー（`MemoryMarshal.Cast` で ReadOnly が返る）|暗黙の型変換|`MemoryMarshal.Cast<byte, float>(array)` は `ReadOnlySpan<float>` を返す。`array.AsSpan()` を渡して `Span<float>` を取得する|
|実行時に NullReferenceException（`ClearEffectChain`）|入力切断漏れ|エフェクトチェーンの全入力スロットに `null!` を設定する。`SetInput(0, null!, true)` のみでなく、`CrossFade` 等の第2入力もクリアする|
|実行時にTypeLoadException|参照DLLバージョン不一致|YMM4のインストールフォルダにあるDLLを参照しているか確認|
|UIが固まる|UIスレッドでの重い処理|バックグラウンド処理後に `Dispatcher` でUI更新|
|エフェクトの色が化ける（青→黄等）|LUTデータのR/B軸逆転|D2D LUT は B(X)=最速, G(Y)=中間, R(Z)=最遅。ループ順序: R外側→G中間→B内側|
|`Math.Clamp` で `ArgumentException`|`min > max` の可能性|入力値の計算で `min > max` になるケース（隣接ポイントが近接等）をガードする。呼び出し前に条件チェックするか `return` する|
|カスタムエディタの Undo/Redo が効かない|`BeginEdit`/`EndEdit` の呼び忘れ|データ変更の前後で必ず `BeginEdit`→変更→`CommitData`→`EndEdit` を呼ぶ|
|カスタムエディタのデータが他アイテムに反映されない|`ItemProperty` の単一書き込み|`boundProperties` の全要素に `SetValue` する（複数アイテム同時選択対応）|
|エフェクトの折りたたみが「その他」と表示される|`GroupName` 未指定|`[Display(GroupName = "プラグイン名", ...)]` で折りたたみヘッダーを制御|

---

### 10\. 配布パッケージング

* **ファイル形式**: `.ymme` 形式。
* **構造**: プラグインのDLLおよび依存ライブラリをZIP圧縮し、拡張子を `.ymme` に変更したものです。

  * YMM4本体に含まれるDLL（`YukkuriMovieMaker.Plugin.dll` 等）はパッケージに含めないでください（`<Private>false</Private>` で出力から除外済みのはず）。

* **インストール**: ユーザーが `.ymme` ファイルをダブルクリックすることで、YMM4が自動的に `user/plugin/` 以下に展開・インストールします。

---

### 11\. 最小構成のテンプレート（ツールプラグイン）

以下は、ツールプラグインの最小限の動作コードです。

#### ファイル構成

```
MyPlugin/
├── Directory.Build.props       ← 環境依存（Gitに含めない）
├── Directory.Build.props.sample ← テンプレート（Gitに含める）
├── MyPlugin.csproj
├── MyToolPlugin.cs             ← IToolPlugin 実装
├── MyToolViewModel.cs          ← IToolViewModel 実装
├── MyToolView.xaml             ← WPF UserControl
└── MyToolView.xaml.cs          ← コードビハインド
```

#### MyPlugin.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="YukkuriMovieMaker.Plugin">
      <HintPath>$(YMM4DirPath)\YukkuriMovieMaker.Plugin.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <!-- ツールプラグインの場合は上記のみで十分。
         エフェクト系プラグインの場合は Controls, Vortice, SharpGen も追加 -->
  </ItemGroup>
</Project>
```

#### MyToolPlugin.cs

```csharp
using YukkuriMovieMaker.Plugin;

namespace MyPlugin;

public class MyToolPlugin : IToolPlugin
{
    public string Name => "ツール名";
    public PluginDetailsAttribute Details => new() { AuthorName = "作者名" };
    public IPluginUpdater? Updater => null;
    public Type ViewModelType => typeof(MyToolViewModel);
    public Type ViewType => typeof(MyToolView);
    public bool AllowMultipleInstances => false;
}
```

#### MyToolViewModel.cs

```csharp
#pragma warning disable CS0067
using System.ComponentModel;
using YukkuriMovieMaker.Plugin;

namespace MyPlugin;

public class MyToolViewModel : IToolViewModel, IDisposable
{
    public string Title => "ツール名";
    public bool CanSuspend => true;

    public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ToolState SaveState() => new();
    public void LoadState(ToolState state) { }

    public void Dispose() { }
}
```

#### MyToolView.xaml

```xml
<UserControl x:Class="MyPlugin.MyToolView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             MinWidth="200" MinHeight="150">
    <Grid>
        <TextBlock Text="Hello, YMM4!" />
    </Grid>
</UserControl>
```

#### MyToolView.xaml.cs

```csharp
using System.Windows.Controls;

namespace MyPlugin;

public partial class MyToolView : UserControl
{
    public MyToolView()
    {
        InitializeComponent();
    }
}
```

---

**アナロジー**: YMM4のプラグイン構造は、\*\*「規格化されたスロットを持つマザーボード（YMM4本体）」に差し込む「拡張カード（DLL）」\*\*のようなものです。開発者は特定のコネクタ（インターフェース）に適合するカードを作ることで、マザーボードの基本機能を汚すことなく、新しいポート（ファイル形式対応）や処理能力（エフェクト）を追加できます。

---

### 12\. 最小構成のテンプレート（映像エフェクトプラグイン）

以下は、映像エフェクトプラグインの最小限の動作コードです。

#### ファイル構成

```
MyEffect/
├── Directory.Build.props       ← 環境依存（Gitに含めない）
├── Directory.Build.props.sample ← テンプレート（Gitに含める）
├── MyEffect.csproj
├── MyEffect.sln
├── MyVideoEffect.cs             ← VideoEffectBase 実装
└── MyVideoEffectProcessor.cs    ← VideoEffectProcessorBase 実装
```

#### MyEffect.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="YukkuriMovieMaker.Plugin">
      <HintPath>$(YMM4DirPath)\YukkuriMovieMaker.Plugin.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="YukkuriMovieMaker.Controls">
      <HintPath>$(YMM4DirPath)\YukkuriMovieMaker.Controls.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Vortice.Direct2D1">
      <HintPath>$(YMM4DirPath)\Vortice.Direct2D1.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Vortice.DirectX">
      <HintPath>$(YMM4DirPath)\Vortice.DirectX.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="SharpGen.Runtime">
      <HintPath>$(YMM4DirPath)\SharpGen.Runtime.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

#### MyVideoEffect.cs

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Settings;

namespace MyEffect;

[VideoEffect("エフェクト名", new[] { "カテゴリ" }, new string[0], false, false)]
public class MyVideoEffect : VideoEffectBase
{
    public override string Label => "エフェクト名";

    [Display(GroupName = "エフェクト名", Name = "強度", Description = "エフェクトの強度")]
    [AnimationSlider("F1", "%", 0, 100)]
    [DefaultValue(100d)]
    public Animation Strength { get; } = new(100, 0, 100, 0);

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        => new MyVideoEffectProcessor(devices, this);

    public override IEnumerable<string> CreateExoVideoFilters(
        int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => [Strength];
}
```

#### MyVideoEffectProcessor.cs

```csharp
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace MyEffect;

public class MyVideoEffectProcessor : VideoEffectProcessorBase
{
    private readonly MyVideoEffect effect;
    private GaussianBlur? blurEffect;  // 例: ガウシアンブラー

    public MyVideoEffectProcessor(IGraphicsDevicesAndContext devices, MyVideoEffect effect)
        : base(devices)
    {
        this.effect = effect;
    }

    protected override ID2D1Image CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var dc = devices.DeviceContext;
        blurEffect = new GaussianBlur(dc);
        disposer.Collect(blurEffect);
        return blurEffect.Output;
    }

    protected override void setInput(ID2D1Image input)
    {
        blurEffect?.SetInput(0, input, true);
    }

    protected override void ClearEffectChain()
    {
        blurEffect?.SetInput(0, null!, true);
    }

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        double strength = effect.Strength.GetValue(
            effectDescription.ItemPosition.Frame,
            effectDescription.ItemDuration.Frame,
            effectDescription.FPS) / 100.0;

        if (blurEffect != null)
            blurEffect.StandardDeviation = (float)(strength * 10.0);

        return effectDescription.DrawDescription;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
```

